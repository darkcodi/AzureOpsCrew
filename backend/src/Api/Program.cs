using System.Text.Json;
using AzureOpsCrew.Api.Background;
using AzureOpsCrew.Api.Endpoints;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Api.Setup.Seeds;
using Microsoft.Extensions.Options;
using Serilog;

#pragma warning disable ASP0013

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting web application");

    var builder = WebApplication.CreateBuilder(args);

    // Configure app settings with environment variables as highest priority
    builder.Host.ConfigureAppConfiguration((context, config) =>
    {
        // Clear default sources
        config.Sources.Clear();

        // Add sources in order of increasing priority (environment variables win)
        config
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                optional: true, reloadOnChange: true)
            .AddUserSecrets(typeof(Program).Assembly)
            .AddEnvironmentVariables();
    });

    // Use Serilog
    builder.Host.UseSerilog();

    // Enable OpenAPI/Swagger
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen();

    // Configure settings and database
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddProviderFacades();
    builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddEmailVerification(builder.Configuration);
    builder.Services.AddAgentFactory(builder.Configuration);
    builder.Services.AddBackgroundTasks();

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
        var provider = builder.Configuration["DatabaseProvider"];
        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var sqlServerSettings = app.Services.GetRequiredService<IOptions<SqlServerSettings>>().Value;
            Log.Information("Database Provider: SqlServer");
            Log.Information("SQL Server Settings: {SqlServerSettings}", JsonSerializer.Serialize(sqlServerSettings));
        }
        else
        {
            Log.Warning("Unknown DatabaseProvider value: {Provider}", provider);
        }
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapSwagger();
        app.UseSwaggerUI();
        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    // Map endpoints
    app.MapAuthEndpoints();
    app.MapUsersEndpoints();
    app.MapTestEndpoints();
    app.MapAgentEndpoints();
    app.MapChannelEndpoints();
    app.MapDmEndpoints();
    app.MapProviderEndpoints();

    await app.Services.RunDbSetup();
    await app.Services.RunLongTermMemorySetup();
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
