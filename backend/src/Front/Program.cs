using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Front;
using Front.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient with API base URL
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:5282";
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

// Register application services
builder.Services.AddSingleton<ChatState>();
builder.Services.AddSingleton<SignalRService>();
builder.Services.AddScoped<ChannelService>();
builder.Services.AddScoped<DmService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<AgentService>();

await builder.Build().RunAsync();
