using AzureOpsCrew.Api.Endpoints;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Api.Setup.Seeds;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Newtonsoft.Json;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Enable OpenAPI/Swagger
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "AzureOpsCrew HTTP Api",
            Version = "v1"
        });
    });

    // Configure settings and database
    builder.Services.AddAiSettings(builder.Configuration, "AzureOpenAI");
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddAgentManagements();
    builder.Services.AddIChatClient();

    // Configure AG-UI
    builder.Services.AddHttpClient();
    builder.Services.AddAGUI();

    // Enable Application Insights
    if (bool.TryParse(builder.Configuration["ApplicationInsights:Enable"], out var enableApplicationInsights)
        && enableApplicationInsights)
        builder.Services.AddApplicationInsightsTelemetry();

    var app = builder.Build();

    // Log configuration settings at startup
    if (app.Environment.IsDevelopment())
    {
        var aiSettings = app.Services.GetRequiredService<IOptions<AiSettings>>().Value;
        Log.Information("AI Settings: {AiSettings}", JsonConvert.SerializeObject(aiSettings));

        var provider = builder.Configuration["DatabaseProvider"];
        switch (provider?.Trim().ToLowerInvariant())
        {
            case "sqlite":
                {
                    var sqliteSettings = app.Services.GetRequiredService<IOptions<SQLiteSettings>>().Value;
                    Log.Information("Database Provider: Sqlite");
                    Log.Information("SQLite Settings: {SqliteSettings}", JsonConvert.SerializeObject(sqliteSettings));
                    break;
                }

            case "cosmosdb":
                {
                    var cosmosSettings = app.Services.GetRequiredService<IOptions<CosmosSettings>>().Value;
                    Log.Information("Database Provider: Cosmos");
                    Log.Information("Cosmos DB Settings: {CosmosSettings}", JsonConvert.SerializeObject(cosmosSettings));
                    break;
                }

            default:
                {
                    Log.Warning("Unknown DatabaseProvider value: {Provider}", provider);
                    break;
                }
        }
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapSwagger();
        app.UseSwaggerUI();
        app.MapGet("/", () => Results.Redirect("/swagger"));
    }

    app.UseHttpsRedirection();

    // Map endpoints
    app.MapTestEndpoints();
    app.MapAgentEndpoints();
    app.MapChannelEndpoints();

    app.MapAllAgUi();

    await app.Services.RunDbSetup(builder.Configuration);
    await app.Services.RunSeeding(builder.Configuration);

    app.Run();
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
