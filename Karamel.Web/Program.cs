using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Karamel.Web;
using Karamel.Web.Services;
using Fluxor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Allow overriding the backend base address via environment/configuration for development.
// If `KARAMEL_BACKEND_BASE` (or configuration key `BackendBase`) is set, use it; otherwise
// fall back to the host environment base address so relative API calls work in production.
var backendBaseFromConfig = builder.Configuration["BackendBase"] ?? Environment.GetEnvironmentVariable("KARAMEL_BACKEND_BASE");
Uri baseAddress;
if (!string.IsNullOrWhiteSpace(backendBaseFromConfig))
{
    baseAddress = new Uri(backendBaseFromConfig);
}
else
{
    baseAddress = new Uri(builder.HostEnvironment.BaseAddress);
}

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = baseAddress });

// Add Fluxor state management
builder.Services.AddFluxor(options =>
{
    options.ScanAssemblies(typeof(Program).Assembly);
});

// Add single-responsibility services (SessionService refactoring completed)
builder.Services.AddScoped<ISessionStorageService, SessionStorageService>();
builder.Services.AddScoped<ISessionApiClient, SessionApiClient>();
builder.Services.AddScoped<ISignalRPlaylistBridge, SignalRPlaylistBridge>();
// SignalRConnectionManager needs backend base address (config value, not HttpClient)
var backendBaseAddress = baseAddress.ToString().TrimEnd('/');
builder.Services.AddSingleton<ISignalRConnectionManager>(sp => 
    new SignalRConnectionManager(
        sp.GetRequiredService<IJSRuntime>(),
        backendBaseAddress,
        sp.GetRequiredService<ILogger<SignalRConnectionManager>>()));
builder.Services.AddScoped<ISongEnrichmentService, SongEnrichmentService>();
builder.Services.AddScoped<IPlaylistStateSynchronizer, PlaylistStateSynchronizer>();

await builder.Build().RunAsync();
