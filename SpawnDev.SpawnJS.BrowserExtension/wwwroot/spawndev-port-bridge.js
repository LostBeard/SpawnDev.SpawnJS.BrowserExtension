// SpawnDev.SpawnJS.BrowserExtension port bridge (ExtensionPortService.ConnectAsync).
// A content script embeds this extension page in a hidden iframe to hand a MessagePort to the extension's background:
// runtime messaging is JSON only, a MessagePort carries transferables (ImageBitmap, ArrayBuffer, VideoFrame...).
// The port travels with a single-use TOKEN the background issued to the content script over runtime.sendMessage, which
// page scripts cannot call - so a page posting its own port into this frame is refused by the background.
(() => {
    const api = globalThis.browser ?? globalThis.chrome;
    const isWorkerBackground = !!api?.runtime?.getManifest?.()?.background?.service_worker;
    addEventListener('message', async (e) => {
        const d = e.data;
        if (e.source !== parent || !d || d.type !== 'spawndev-port' || !e.ports || !e.ports[0]) return;
        try {
            const msg = { type: 'spawndev-port', name: d.name, token: d.token };
            if (isWorkerBackground) {
                // Chrome: the extension's service worker controls this extension page
                const reg = await navigator.serviceWorker.ready;
                reg.active.postMessage(msg, [e.ports[0]]);
            } else {
                // Firefox: the background is a page
                const bg = await api.runtime.getBackgroundPage();
                bg.postMessage(msg, location.origin, [e.ports[0]]);
            }
            parent.postMessage({ type: 'spawndev-port-sent', id: d.id }, '*');
        } catch (err) {
            parent.postMessage({ type: 'spawndev-port-failed', id: d.id, error: String(err) }, '*');
        }
    });
    parent.postMessage({ type: 'spawndev-port-bridge-ready' }, '*');
})();
