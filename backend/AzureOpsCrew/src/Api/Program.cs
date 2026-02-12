using AzureOpsCrew.Api.Endpoints;
using AzureOpsCrew.Api.Extensions;
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

    // Configure settings
    var cosmosSettings = builder.Services.AddCosmosSettings(builder.Configuration, "CosmosDb");
    var aiSettings = builder.Services.AddAiSettings(builder.Configuration, "AzureOpenAI");

    // Configure EF Core with Cosmos DB
    builder.Services.AddEFCoreCosmosDb();
    builder.Services.AddIChatClient();

    // Configure AG-UI
    builder.Services.AddHttpClient().AddLogging();
    builder.Services.AddAGUI();

    // Enable Application Insights
    if (bool.TryParse(builder.Configuration["ApplicationInsights:Enable"], out var enableApplicationInsights)
        && enableApplicationInsights)
        builder.Services.AddApplicationInsightsTelemetry();

    var app = builder.Build();

    // Log configuration settings at startup
    if (app.Environment.IsDevelopment())
    {
        Log.Information("Cosmos DB Settings: {CosmosSettings}", JsonConvert.SerializeObject(cosmosSettings));
        Log.Information("AI Settings: {AiSettings}", JsonConvert.SerializeObject(aiSettings));
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // Map endpoints
    app.MapDummyEndpoints();
    app.MapTestEndpoints();
    app.MapAgents();

    // await app.Services.RunEnsureEFCoreCosmosDbCreated();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
