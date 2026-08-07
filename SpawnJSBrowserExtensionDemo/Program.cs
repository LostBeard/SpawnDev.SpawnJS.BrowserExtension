using Microsoft.Extensions.DependencyInjection;
using SpawnDev;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.WebWorkers;
using SpawnJSBrowserExtensionDemo.Services;

// .Net Wasm, unlike Blazor, does not come with a built-in dependency injection container.
// SpawnJSApp is a very minimal DI container that can be used when not using something else.
var builder = SpawnJSAppBuilder.CreateDefault(args, out var JS);

// register WebWorkerService
builder.Services.AddWebWorkerService();

// Additional services
// AppService will autostart when RunAsync is called becuase it implements IAsyncBackgroundService (IBackgroundService)
builder.Services.AddSingleton<AppService>();

// RunAsync autostarts IBackgroundService and IAsyncBackgroundService services
await builder.Build().RunAsync();
