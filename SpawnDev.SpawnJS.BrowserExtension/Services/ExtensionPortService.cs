using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpawnDev.SpawnJS.JSObjects;

namespace SpawnDev.SpawnJS.BrowserExtension.Services
{
    /// <summary>
    /// A <see cref="MessagePort"/> from a content script to the extension's BACKGROUND (Chrome service worker / Firefox
    /// background page). Unlike runtime messaging (JSON only) a MessagePort carries TRANSFERABLES - ImageBitmap,
    /// ArrayBuffer, VideoFrame - so a page can hand frames to the extension without copying or encoding them. The
    /// background can pass the port on (e.g. to an offscreen document) with <see cref="Client.PostMessage(object, object[])"/>.
    /// </summary>
    /// <remarks>
    /// How: the content script asks the background for a single-use token over runtime.sendMessage (which page scripts
    /// cannot call), embeds the library's hidden bridge page (spawndev-port-bridge.html, an extension page), and posts the
    /// port + token into it; the bridge forwards both to the background, which accepts the port only with a valid token.
    /// A hostile page that posts its own port into the bridge has no token and is refused. Asking for the token first also
    /// guarantees the background's .NET runtime - and so this service's message listener - is up before the port arrives
    /// (a service worker drops 'message' events nobody listens to yet).
    /// <para>MEASURED 2026-10-05 (Chrome 151, probe extension): setup ~65 ms once; a 1920x1080 ImageBitmap through such a
    /// port to an offscreen document and back, 0.5 ms median.</para>
    /// Register it in the background AND in content scripts (the same singleton serves both sides).
    /// </remarks>
    public sealed class ExtensionPortService : IAsyncBackgroundService
    {
        /// <summary>The bridge page this library serves at the app root.</summary>
        public const string BridgePage = "spawndev-port-bridge.html";
        const string TokenRequestType = "spawndev-port-token";
        const string PortMessageType = "spawndev-port";
        static readonly TimeSpan TokenLifetime = TimeSpan.FromSeconds(30);

        readonly SpawnJSRuntime JS;
        readonly BrowserExtensionService BES;
        readonly Dictionary<string, (string Name, DateTime Expires)> _tokens = new();
        Task? _ready;

        /// <summary>
        /// Background: a content script connected a port. Arguments: the name it passed to <see cref="ConnectAsync"/> and
        /// the port (yours: keep it, pass it on, or close it).
        /// </summary>
        public event Action<string, MessagePort>? OnPortConnected;

        public ExtensionPortService(SpawnJSRuntime js, BrowserExtensionService bes)
        {
            JS = js;
            BES = bes;
        }

        /// <inheritdoc/>
        public Task Ready => _ready ??= InitAsync();

        Task InitAsync()
        {
            if (BES.ExtensionMode != ExtensionMode.Background) return Task.CompletedTask;
            var runtime = BES.Runtime;
            if (runtime != null) runtime.OnMessage += OnRuntimeMessage;
            if (JS.IsServiceWorkerGlobalScope)
            {
                using var self = JS.Get<ServiceWorkerGlobalScope>("self");
                self.OnMessage += OnWorkerMessage;
            }
            else
            {
                using var window = JS.Get<SpawnJS.JSObjects.Window>("window");
                window.AddEventListener<MessageEvent>("message", OnWindowMessage);
            }
            return Task.CompletedTask;
        }

        // ---- background -------------------------------------------------------------------------------------------

        bool OnRuntimeMessage(SpawnJSObject data, MessageSender sender, Function? sendResponse)
        {
            if (sendResponse == null) return false;
            string? raw;
            try { raw = data.JSRef!.As<string>(); }
            catch { return false; }   // not a string message - not ours
            if (raw == null || !raw.Contains(TokenRequestType, StringComparison.Ordinal)) return false;
            TokenRequest? req;
            try { req = JsonSerializer.Deserialize(raw, PortJson.Default.TokenRequest); }
            catch { return false; }
            if (req == null || req.Type != TokenRequestType) return false;
            var now = DateTime.UtcNow;
            foreach (var stale in _tokens.Where(t => t.Value.Expires < now).Select(t => t.Key).ToList()) _tokens.Remove(stale);
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            _tokens[token] = (req.Name ?? "", now + TokenLifetime);
            try { sendResponse.CallVoid(null, token); }
            finally { sendResponse.Dispose(); }
            return false;
        }

        void OnWorkerMessage(ExtendableMessageEvent e)
        {
            using var data = e.Data;
            using var ports = e.Ports;
            Accept(data, ports);
        }

        void OnWindowMessage(MessageEvent e)
        {
            // Firefox background page: only the extension's own bridge page may deliver (same origin)
            if (e.Origin != ExtensionOrigin) return;
            using var data = e.GetData<SpawnJSObject?>();
            using var ports = e.Ports;
            Accept(data, ports);
        }

        void Accept(SpawnJSObject? data, Array<MessagePort> ports)
        {
            if (data == null || ports.Length < 1) return;
            string? type = data.JSRef!.Get<string?>("type");
            if (type != PortMessageType) return;
            string? token = data.JSRef!.Get<string?>("token");
            if (token == null || !_tokens.Remove(token, out var issued) || issued.Expires < DateTime.UtcNow)
            {
                JS.Log("SpawnDev.BrowserExtension: refused a port without a valid token.");
                ports[0].Close();
                return;
            }
            var port = ports[0];
            var handler = OnPortConnected;
            if (handler == null) { port.Close(); return; }
            handler(issued.Name, port);
        }

        // ---- content script ---------------------------------------------------------------------------------------

        string ExtensionOrigin => new Uri(BES.GetURL("/")).GetLeftPart(UriPartial.Authority);

        /// <summary>
        /// Content script: connects a <see cref="MessagePort"/> to the extension's background, which receives it in
        /// <see cref="OnPortConnected"/> with <paramref name="name"/>.
        /// </summary>
        public async Task<MessagePort> ConnectAsync(string name, TimeSpan? timeout = null)
        {
            if (BES.ExtensionMode != ExtensionMode.Content) throw new InvalidOperationException("ConnectAsync is for content scripts.");
            var runtime = BES.Runtime ?? throw new InvalidOperationException("No runtime: the extension context is gone.");
            var limit = timeout ?? TimeSpan.FromSeconds(15);
            // 1) the token: proves this request comes from the extension's own content script (and wakes the background)
            var token = await runtime.SendMessage<string>(JsonSerializer.Serialize(new TokenRequest { Type = TokenRequestType, Name = name }, PortJson.Default.TokenRequest));
            if (string.IsNullOrEmpty(token)) throw new InvalidOperationException("The background issued no port token (is ExtensionPortService registered there?).");
            // 2) the bridge frame
            using var window = JS.Get<SpawnJS.JSObjects.Window>("window");
            using var document = JS.Get<Document>("document");
            var iframe = document.CreateElement<HTMLIFrameElement>("iframe");
            iframe.SetAttribute("style", "display: none !important;");
            iframe.Src = BES.GetURL(BridgePage);
            var origin = ExtensionOrigin;
            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
            var ready = new TaskCompletionSource();
            var sent = new TaskCompletionSource();
            Action<MessageEvent> onMessage = e =>
            {
                if (e.Origin != origin) return;   // only the bridge page can carry the extension's origin
                using var d = e.GetData<SpawnJSObject?>();
                if (d == null) return;
                var type = d.JSRef!.Get<string?>("type");
                if (type == "spawndev-port-bridge-ready") ready.TrySetResult();
                else if (type == "spawndev-port-sent" && d.JSRef!.Get<string?>("id") == id) sent.TrySetResult();
                else if (type == "spawndev-port-failed" && d.JSRef!.Get<string?>("id") == id)
                    sent.TrySetException(new InvalidOperationException($"port bridge: {d.JSRef!.Get<string?>("error")}"));
            };
            window.AddEventListener<MessageEvent>("message", onMessage);
            var channel = new MessageChannel();
            try
            {
                using (var root = document.DocumentElement) root!.AppendChild(iframe);
                await WaitAsync(ready.Task, limit, "the port bridge page did not load");
                using var frameWindow = iframe.ContentWindow;
                using var port2 = channel.Port2;
                frameWindow.PostMessage(new PortMessage { Type = PortMessageType, Name = name, Token = token, Id = id }, origin, new object[] { port2 });
                await WaitAsync(sent.Task, limit, "the port bridge did not forward the port");
                return channel.Port1;
            }
            finally
            {
                window.RemoveEventListener<MessageEvent>("message", onMessage);
                // the port is now entangled with the background directly: the bridge frame is not needed any more
                try { iframe.Remove(); } catch { }
                iframe.Dispose();
                channel.Dispose();
            }
        }

        static async Task WaitAsync(Task task, TimeSpan limit, string what)
        {
            if (await Task.WhenAny(task, Task.Delay(limit)) != task) throw new TimeoutException($"{what} within {limit.TotalSeconds:0} s.");
            await task;
        }

        internal sealed class TokenRequest
        {
            [JsonPropertyName("type")] public string Type { get; set; } = "";
            [JsonPropertyName("name")] public string? Name { get; set; }
        }

        /// <summary>The message posted into the bridge page with the port.</summary>
        public sealed class PortMessage
        {
            /// <summary>Always "spawndev-port".</summary>
            public string Type { get; set; } = "";
            /// <summary>The connection's name.</summary>
            public string Name { get; set; } = "";
            /// <summary>The background-issued single-use token.</summary>
            public string Token { get; set; } = "";
            /// <summary>Correlates the bridge's reply.</summary>
            public string Id { get; set; } = "";
        }
    }

    [JsonSerializable(typeof(ExtensionPortService.TokenRequest))]
    internal partial class PortJson : JsonSerializerContext { }
}
