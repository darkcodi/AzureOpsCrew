using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Front;
using Front.Handlers;
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

    // Configure HttpClient with API base URL and Authorization header handler
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000";
    builder.Services.AddScoped<AuthHeaderHandler>();
    builder.Services.AddScoped(sp =>
    {
        var handler = sp.GetRequiredService<AuthHeaderHandler>();
        handler.InnerHandler = new HttpClientHandler();
        return new HttpClient(handler)
        {
            BaseAddress = new Uri(apiBaseUrl)
        };
    });

    // Register application services
    builder.Services.AddSingleton<ChatState>();
    builder.Services.AddSingleton<AuthState>();
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
