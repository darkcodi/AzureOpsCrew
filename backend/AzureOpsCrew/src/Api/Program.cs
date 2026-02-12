using AzureOpsCrew.Api.Endpoints;
using AzureOpsCrew.Api.Extensions;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddCosmosSettings(builder.Configuration, "CosmosDb");
builder.Services.AddAiSettings(builder.Configuration, "AzureOpenAI");

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

// await app.Services.TestAgents();
// await app.Services.RunEnsureEFCoreCosmosDbCreated();

app.Run();
