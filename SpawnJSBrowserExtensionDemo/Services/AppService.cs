using SpawnDev;
using SpawnDev.SpawnJS;

namespace SpawnJSBrowserExtensionDemo.Services
{
    public class AppService(SpawnJSRuntime JS) : IAsyncBackgroundService
    {
        Task? _ready = null;
        public Task Ready => _ready ??= InitAsync();

        async Task InitAsync()
        {
            Console.WriteLine($"TestService.InitAsync() {JS.GlobalScopeName} {JS.InstanceId}");
        }
    }
}
