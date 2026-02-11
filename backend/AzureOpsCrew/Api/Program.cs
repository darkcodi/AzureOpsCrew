using AzureOpsCrew.Api.Endpoints;
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

app.MapDummyEndpoints();

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
