using AzureOpsCrew.Api.Endpoints;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Api.Setup.Seeds;
using AzureOpsCrew.Domain.AgentServices;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

#pragma warning disable ASP0013

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
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

        // Map MCP_* env vars from .env to .NET config paths
        // (.env uses MCP_AZURE_URL, MCP_ADO_URL etc.; .NET config expects Mcp:Azure:ServerUrl etc.)
        var mcpOverrides = new Dictionary<string, string?>();

        void MapEnv(string envVar, string configPath)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(value))
                mcpOverrides[configPath] = value;
        }

        // Azure MCP Server
        MapEnv("MCP_AZURE_URL", "Mcp:Azure:ServerUrl");
        MapEnv("MCP_AZURE_TENANT_ID", "Mcp:Azure:TenantId");
        MapEnv("MCP_AZURE_CLIENT_ID", "Mcp:Azure:ClientId");
        MapEnv("MCP_AZURE_CLIENT_SECRET", "Mcp:Azure:ClientSecret");
        MapEnv("MCP_AZURE_TOKEN_URL", "Mcp:Azure:TokenUrl");
        MapEnv("MCP_AZURE_SCOPE", "Mcp:Azure:Scope");

        // Azure DevOps MCP Server
        MapEnv("MCP_ADO_URL", "Mcp:AzureDevOps:ServerUrl");
        MapEnv("MCP_ADO_TENANT_ID", "Mcp:AzureDevOps:TenantId");
        MapEnv("MCP_ADO_CLIENT_ID", "Mcp:AzureDevOps:ClientId");
        MapEnv("MCP_ADO_CLIENT_SECRET", "Mcp:AzureDevOps:ClientSecret");
        MapEnv("MCP_ADO_TOKEN_URL", "Mcp:AzureDevOps:TokenUrl");
        MapEnv("MCP_ADO_SCOPE", "Mcp:AzureDevOps:Scope");

        // Platform MCP Server
        MapEnv("MCP_PLATFORM_URL", "Mcp:Platform:ServerUrl");
        MapEnv("MCP_PLATFORM_TENANT_ID", "Mcp:Platform:TenantId");
        MapEnv("MCP_PLATFORM_CLIENT_ID", "Mcp:Platform:ClientId");
        MapEnv("MCP_PLATFORM_CLIENT_SECRET", "Mcp:Platform:ClientSecret");
        MapEnv("MCP_PLATFORM_TOKEN_URL", "Mcp:Platform:TokenUrl");
        MapEnv("MCP_PLATFORM_SCOPE", "Mcp:Platform:Scope");

        // GitOps MCP Server (Azure DevOps GitOps — code write operations)
        MapEnv("MCP_GITOPS_URL", "Mcp:GitOps:ServerUrl");
        MapEnv("MCP_GITOPS_TENANT_ID", "Mcp:GitOps:TenantId");
        MapEnv("MCP_GITOPS_CLIENT_ID", "Mcp:GitOps:ClientId");
        MapEnv("MCP_GITOPS_CLIENT_SECRET", "Mcp:GitOps:ClientSecret");
        MapEnv("MCP_GITOPS_TOKEN_URL", "Mcp:GitOps:TokenUrl");
        MapEnv("MCP_GITOPS_SCOPE", "Mcp:GitOps:Scope");

        // JWT & OpenAI mappings from .env (SCREAMING_SNAKE_CASE → .NET config paths)
        MapEnv("JWT_SIGNING_KEY", "Jwt:SigningKey");
        MapEnv("OPENAI_API_KEY", "OpenAI:ApiKey");

        if (mcpOverrides.Count > 0)
            config.AddInMemoryCollection(mcpOverrides);
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
    builder.Services.AddOpenAIAndMcp(builder.Configuration);
    builder.Services.AddOrchestration(builder.Configuration);
    builder.Services.AddAgentFactory(builder.Configuration);
    builder.Services.AddExecutionEngine(builder.Configuration);

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
        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sqliteSettings = app.Services.GetRequiredService<IOptions<SQLiteSettings>>().Value;
            Log.Information("Database Provider: Sqlite");
            Log.Information("SQLite Settings: {SqliteSettings}", JsonConvert.SerializeObject(sqliteSettings));
        }
        else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var sqlServerSettings = app.Services.GetRequiredService<IOptions<SqlServerSettings>>().Value;
            Log.Information("Database Provider: SqlServer");
            Log.Information("SQL Server Settings: {SqlServerSettings}", JsonConvert.SerializeObject(sqlServerSettings));
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
    app.MapAgentEndpoints();
    app.MapChannelEndpoints();
    app.MapProviderEndpoints();

    app.MapAllAgUi();
    app.MapRunEndpoints();

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
