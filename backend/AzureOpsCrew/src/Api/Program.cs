using AzureOpsCrew.Api.Endpoints;
using AzureOpsCrew.Api.Extensions;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options => 
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AzureOpsCrew HTTP Api",
        Version = "v1"
    });
});

//Add EF Core
builder.Services.AddEFCoreCosmosDb(builder.Configuration, "CosmosDb");

//Enable Application Insights
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

//Map endpoints
app.MapDummyEndpoints();

//await app.Services.RunEnsureEFCoreCosmosDbCreated();

app.Run();