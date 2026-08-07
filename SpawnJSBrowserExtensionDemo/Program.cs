using Microsoft.Extensions.DependencyInjection;
using SpawnDev;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.WebWorkers;
using SpawnJSBrowserExtensionDemo.Services;

// SpawnJSApp is a minimal DI container with SpawnJSRuntime and BackgroundServiceManager.
var builder = SpawnJSAppBuilder.CreateDefault(args, out var JS);

// register WebWorkerService
builder.Services.AddWebWorkerService();

// Additional services
// AppService will autostart when RunAsync is called becuase it implements IAsyncBackgroundService (IBackgroundService)
builder.Services.AddSingleton<AppService>();

// RunAsync auto-starts IBackgroundService and IAsyncBackgroundService services
await builder.Build().RunAsync();
