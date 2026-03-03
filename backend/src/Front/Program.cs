using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Front;
using Front.Services;
using Serilog;

// Configure Serilog for Blazor WASM
Log.Logger = new LoggerConfiguration()
    .WriteTo.BrowserConsole()
    .CreateLogger();

try
{
    Log.Information("Starting Blazor WebAssembly application");

    var builder = WebAssemblyHostBuilder.CreateDefault(args);
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");

    // Use Serilog
    builder.Services.AddLogging(loggingBuilder =>
    {
        loggingBuilder.AddSerilog(dispose: true);
    });

    // Configure HttpClient with API base URL
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
    builder.Services.AddScoped(sp => new HttpClient
    {
        BaseAddress = new Uri(apiBaseUrl)
    });

    // Register application services
    builder.Services.AddSingleton<ChatState>();
    builder.Services.AddSingleton<AuthState>();
    builder.Services.AddScoped<AppInitializationService>();
    builder.Services.AddScoped<SignalRService>();
    builder.Services.AddScoped<ChannelService>();
    builder.Services.AddScoped<DmService>();
    builder.Services.AddScoped<UserService>();
    builder.Services.AddScoped<AgentService>();
    builder.Services.AddScoped<AuthenticationService>();

    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
