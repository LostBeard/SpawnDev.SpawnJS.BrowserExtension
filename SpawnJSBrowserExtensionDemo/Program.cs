using Microsoft.Extensions.DependencyInjection;
using SpawnDev;
using SpawnDev.SpawnJS;
using SpawnDev.SpawnJS.WebWorkers;
using SpawnJSBrowserExtensionDemo.Services;

// .Net Wasm, unlike Blazor, does not come with a built-in dependency injection container.
// SpawnJSApp is a very minimal DI container that can be used when not using something else.
var builder = SpawnJSAppBuilder.CreateDefault(args);

// register SpawnJSRuntime
builder.Services.AddSpawnJSRuntime(out var JS);

Console.WriteLine($"{AppDomain.CurrentDomain.FriendlyName} {JS.GlobalScopeName} {JS.AppBaseUri}");

// register WebWorkerService
builder.Services.AddWebWorkerService();

// Additional services
// AppService will autostart when RunAsync is called becuase it implements IAsyncBackgroundService (IBackgroundService)
builder.Services.AddSingleton<AppService>();

// HTTPClient set to the app's base address 
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(JS.AppBaseUri) });

// RunAsync autostarts IBackgroundService and IAsyncBackgroundService services
await builder.Build().RunAsync();
