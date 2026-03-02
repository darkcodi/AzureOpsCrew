# Backend — Детальный план изменений

## Общий подход

Упрощаем бэкенд максимально: убираем лишние CRUD, email, мульти-провайдеры. Фокус — один endpoint для группового чата агентов с MCP-инструментами.

---

## 1. Убрать/Упростить

### Файлы на УДАЛЕНИЕ:
```
backend/src/Api/Email/                          (весь каталог — Brevo email)
backend/src/Api/Settings/BrevoSettings.cs
backend/src/Api/Settings/EmailVerificationSettings.cs
backend/src/Api/Endpoints/ProviderEndpoints.cs  (провайдер захардкожен)
backend/src/Api/Endpoints/AuthEndpoints.cs      (заменяем на auto-login)
backend/src/Api/Endpoints/Dtos/Auth/            (весь каталог)
backend/src/Api/Endpoints/Dtos/Providers/       (весь каталог)
```

### Файлы на УПРОЩЕНИЕ:
- `ServiceCollectionExtensions.cs` — убрать `AddEmailVerification`, упростить `AddJwtAuthentication` (hardcoded key для demo)
- `Program.cs` — убрать email, упростить pipeline
- `Seeder.cs` — переписать: seed OpenAI provider + 3 агента + 1 канал + 1 demo user

---

## 2. Новая структура backend/src/Api/

```
Api/
├── Program.cs                    (упрощённый)
├── Api.csproj                    (обновлённые зависимости)
├── Dockerfile
├── appsettings.json             (упрощённый конфиг)
├── appsettings.Development.json
│
├── Auth/
│   ├── AuthenticatedUserExtensions.cs  (оставить)
│   └── JwtTokenService.cs              (оставить, упрощённый)
│
├── Endpoints/
│   ├── CrewChatEndpoint.cs       ← НОВЫЙ (главный AG-UI endpoint)
│   ├── AgentEndpoints.cs         (только GET /api/agents — read-only)
│   ├── ChannelEndpoints.cs       (только GET /api/channels — read-only)
│   ├── AuthEndpoints.cs          (упрощённый: только auto-login)
│   ├── TestEndpoints.cs          (оставить)
│   └── Dtos/
│       └── AGUI/                 (оставить как есть)
│
├── Extensions/
│   ├── ServiceCollectionExtensions.cs  (упрощённый)
│   ├── ServiceProviderExtensions.cs    (оставить)
│   ├── AGUIExtensions.cs               (оставить)
│   └── McpExtensions.cs          ← НОВЫЙ (подключение к Azure MCP)
│
├── Settings/
│   ├── JwtSettings.cs            (оставить)
│   ├── SQLiteSettings.cs         (оставить)
│   ├── SqlServerSettings.cs      (оставить)
│   ├── OpenAISettings.cs         ← НОВЫЙ
│   └── McpSettings.cs            ← НОВЫЙ
│
└── Setup/
    └── Seeds/
        ├── Seeder.cs             (переписать)
        ├── SeederOptions.cs      (упростить)
        └── ServiceProviderExtensions.cs (оставить)
```

---

## 3. Новые/Изменённые файлы — ДЕТАЛЬНО

### 3.1 `Settings/OpenAISettings.cs` (НОВЫЙ)

```csharp
namespace AzureOpsCrew.Api.Settings;

public sealed class OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public string? BaseUrl { get; set; } // null = default OpenAI
}
```

### 3.2 `Settings/McpSettings.cs` (НОВЫЙ)

```csharp
namespace AzureOpsCrew.Api.Settings;

public sealed class McpSettings
{
    public string ServerUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    
    // Доступные MCP tools — будут фильтроваться по агенту
    // Если пусто — агент получает ВСЕ tools с MCP сервера
    public string[] AllowedTools { get; set; } = [];
}
```

### 3.3 `Extensions/McpExtensions.cs` (НОВЫЙ)

Этот файл подключает Azure MCP Server как источник tools для агентов.

```csharp
using AzureOpsCrew.Api.Settings;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Api.Extensions;

public static class McpExtensions
{
    /// <summary>
    /// Создаёт набор AITool из Azure MCP сервера.
    /// MCP сервер предоставляет tools через стандартный MCP протокол.
    /// Мы вызываем их как обычные HTTP calls к MCP серверу.
    /// </summary>
    public static IServiceCollection AddMcpTools(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.Configure<McpSettings>(configuration.GetSection("Mcp"));
        
        // Регистрируем HttpClient для MCP сервера
        services.AddHttpClient("McpServer", (sp, client) =>
        {
            var settings = configuration.GetSection("Mcp").Get<McpSettings>()!;
            client.BaseAddress = new Uri(settings.ServerUrl);
            if (!string.IsNullOrEmpty(settings.ApiKey))
                client.DefaultRequestHeaders.Add("x-api-key", settings.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Регистрируем MCP tool provider как Singleton
        services.AddSingleton<McpToolProvider>();
        
        return services;
    }
}

/// <summary>
/// Провайдер MCP tools. При старте загружает список tools с MCP сервера,
/// потом вызывает их через HTTP когда агент запрашивает.
/// </summary>
public class McpToolProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly McpSettings _settings;
    private List<AITool>? _cachedTools;

    public McpToolProvider(IHttpClientFactory httpClientFactory, IOptions<McpSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    /// <summary>
    /// Получить все MCP tools (с кешированием).
    /// Каждый tool — это AIFunction, которая при вызове делает HTTP request к MCP серверу.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken ct = default)
    {
        if (_cachedTools is not null) return _cachedTools;
        
        // TODO: Реализовать MCP protocol discovery
        // GET {McpServerUrl}/tools → список доступных tools
        // Для каждого tool создаём AIFunction wrapper
        
        // Пока что создаём hardcoded tools для демо:
        _cachedTools = CreateDemoTools();
        return _cachedTools;
    }

    /// <summary>
    /// Получить tools, отфильтрованные для конкретного агента.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetToolsForAgentAsync(
        string agentRole, 
        CancellationToken ct = default)
    {
        var allTools = await GetToolsAsync(ct);
        
        return agentRole switch
        {
            "devops" => allTools.Where(t => 
                t.Name.StartsWith("appinsights_") || 
                t.Name.StartsWith("pipeline_") ||
                t.Name.StartsWith("resource_")).ToList(),
            
            "developer" => allTools.Where(t => 
                t.Name.StartsWith("repo_") ||
                t.Name.StartsWith("code_")).ToList(),
            
            "manager" => [], // Manager не вызывает MCP tools напрямую
            
            _ => allTools.ToList()
        };
    }

    private List<AITool> CreateDemoTools()
    {
        // Эти tools вызывают Azure MCP Server
        return
        [
            // DevOps tools
            AIFunctionFactory.Create(QueryAppInsightsAsync, "appinsights_query",
                "Query Application Insights for errors, exceptions, and performance data"),
            
            AIFunctionFactory.Create(GetResourceHealthAsync, "resource_health",
                "Check health status of Azure resources"),
            
            AIFunctionFactory.Create(GetPipelineStatusAsync, "pipeline_status",
                "Get status of CI/CD pipelines"),
            
            AIFunctionFactory.Create(RunPipelineAsync, "pipeline_run",
                "Trigger a CI/CD pipeline run"),
            
            // Developer tools
            AIFunctionFactory.Create(ReadRepoFileAsync, "repo_file_read",
                "Read a file from the code repository"),
            
            AIFunctionFactory.Create(SearchRepoAsync, "repo_search",
                "Search for code patterns in the repository"),
            
            AIFunctionFactory.Create(CreatePullRequestAsync, "repo_pr_create",
                "Create a pull request with code changes"),
        ];
    }

    // ============ Tool implementations (call MCP server) ============
    
    private async Task<string> QueryAppInsightsAsync(
        string query, string timeRange = "1h")
    {
        return await CallMcpToolAsync("appinsights/query", new { query, timeRange });
    }

    private async Task<string> GetResourceHealthAsync(string resourceGroup = "")
    {
        return await CallMcpToolAsync("resources/health", new { resourceGroup });
    }

    private async Task<string> GetPipelineStatusAsync(string pipelineName = "")
    {
        return await CallMcpToolAsync("pipelines/status", new { pipelineName });
    }

    private async Task<string> RunPipelineAsync(string pipelineName, string branch = "main")
    {
        return await CallMcpToolAsync("pipelines/run", new { pipelineName, branch });
    }

    private async Task<string> ReadRepoFileAsync(string filePath)
    {
        return await CallMcpToolAsync("repos/file", new { filePath });
    }

    private async Task<string> SearchRepoAsync(string pattern, string filePattern = "*")
    {
        return await CallMcpToolAsync("repos/search", new { pattern, filePattern });
    }

    private async Task<string> CreatePullRequestAsync(
        string title, string description, string branch, string changes)
    {
        return await CallMcpToolAsync("repos/pr/create", 
            new { title, description, branch, changes });
    }

    /// <summary>
    /// Универсальный вызов MCP tool через HTTP.
    /// </summary>
    private async Task<string> CallMcpToolAsync(string toolPath, object parameters)
    {
        var client = _httpClientFactory.CreateClient("McpServer");
        var response = await client.PostAsJsonAsync($"/api/tools/{toolPath}", parameters);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            return $"Error calling MCP tool: {response.StatusCode} - {error}";
        }
        
        return await response.Content.ReadAsStringAsync();
    }
}
```

> **⚠️ ВАЖНО**: Реализация `McpToolProvider` — это заглушка. Фактический протокол MCP может отличаться (SSE, JSON-RPC). Адаптируем под реальный Azure MCP сервер, когда получим его URL и документацию.

### 3.4 `Endpoints/CrewChatEndpoint.cs` (НОВЫЙ — главный endpoint)

```csharp
using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.AGUI;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Domain.AgentServices;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using System.Text.Json;

namespace AzureOpsCrew.Api.Endpoints;

public static class CrewChatEndpoint
{
    public static void MapCrewChat(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/crew/chat", async (
            [FromBody] RunAgentInput? input,
            IProviderFacadeResolver providerFactory,
            AzureOpsCrewContext dbContext,
            IAiAgentFactory agentFactory,
            McpToolProvider mcpToolProvider,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (input is null) return Results.BadRequest();
            
            var userId = http.User.GetRequiredUserId();
            Log.Information("Crew chat request. ThreadId={ThreadId}, RunId={RunId}", 
                input.ThreadId, input.RunId);

            var jsonOptions = http.RequestServices
                .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var messages = input.Messages.AsChatMessages(jsonSerializerOptions);
            var clientTools = input.Tools?.AsAITools().ToList();

            // 1) Загрузить канал "Ops Room" (единственный)
            var channel = await dbContext.Channels
                .FirstOrDefaultAsync(c => c.ClientId == userId, cancellationToken);
            if (channel is null)
                return Results.BadRequest("No channel found");

            // 2) Загрузить агентов канала
            var agentIds = channel.AgentIds.Select(Guid.Parse).ToList();
            var agents = await dbContext.Agents
                .Where(a => agentIds.Contains(a.Id) && a.ClientId == userId)
                .ToListAsync(cancellationToken);

            if (agents.Count == 0)
                return Results.BadRequest("No agents in channel");

            // 3) Загрузить провайдеров
            var providerIds = agents.Select(a => a.ProviderId).Distinct().ToList();
            var providers = await dbContext.Providers
                .Where(p => providerIds.Contains(p.Id) && p.ClientId == userId)
                .ToListAsync(cancellationToken);

            // 4) Создать AI агентов с MCP tools
            var internalAgents = new List<AIAgent>();
            foreach (var agent in agents)
            {
                var provider = providers.Single(p => p.Id == agent.ProviderId);
                var providerService = providerFactory.GetService(provider.ProviderType);
                var chatClient = providerService.CreateChatClient(
                    provider, agent.Info.Model, cancellationToken);

                // Получаем MCP tools для этого агента на основе его роли
                var agentRole = DetermineAgentRole(agent.Info.Name);
                var mcpTools = await mcpToolProvider.GetToolsForAgentAsync(
                    agentRole, cancellationToken);

                // Объединяем client tools + MCP tools
                var allTools = new List<AITool>();
                if (clientTools is not null) allTools.AddRange(clientTools);
                allTools.AddRange(mcpTools);

                var additionalProps = new AdditionalPropertiesDictionary
                {
                    ["ag_ui_state"] = input.State,
                    ["ag_ui_context"] = input.Context?
                        .Select(c => new KeyValuePair<string, string>(c.Description, c.Value))
                        .ToArray(),
                    ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
                    ["ag_ui_thread_id"] = input.ThreadId,
                    ["ag_ui_run_id"] = input.RunId,
                    ["agent_role"] = agentRole
                };

                var aiAgent = agentFactory.Create(
                    chatClient, agent, allTools, additionalProps);
                internalAgents.Add(aiAgent);
            }

            // 5) Построить GroupChat workflow
            //    Manager — первый агент, он координирует остальных
            var workflow = AgentWorkflowBuilder
                .CreateGroupChatBuilderWith(chatAgents => 
                    new RoundRobinGroupChatManager(internalAgents)
                    {
                        MaximumIterationCount = 6 // 2 раунда на 3 агента
                    })
                .AddParticipants(internalAgents)
                .Build();

            var workflowAgent = workflow.AsAgent(
                id: channel.Id.ToString(),
                name: "ops-crew",
                includeExceptionDetails: false);

            var session = await workflowAgent.CreateSessionAsync();

            // 6) Стримить SSE ответ
            var updates = workflowAgent
                .RunStreamingAsync(
                    messages, 
                    session: session, 
                    cancellationToken: cancellationToken)
                .AsChatResponseUpdatesAsync()
                .FilterServerToolsFromMixedToolInvocationsAsync(
                    clientTools, cancellationToken);

            var aguiEvents = updates.AsAGUIEventStreamAsync(
                input.ThreadId, input.RunId, 
                jsonSerializerOptions, cancellationToken);

            var sseLogger = http.RequestServices
                .GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
            return new AGUIServerSentEventsResult(
                aguiEvents, sseLogger, jsonSerializerOptions);
        })
        .WithTags("Crew Chat")
        .RequireAuthorization();
    }

    private static string DetermineAgentRole(string agentName)
    {
        return agentName.ToLowerInvariant() switch
        {
            var n when n.Contains("devops") || n.Contains("ops") => "devops",
            var n when n.Contains("dev") || n.Contains("developer") => "developer",
            var n when n.Contains("manager") => "manager",
            _ => "general"
        };
    }
}
```

### 3.5 `Endpoints/AuthEndpoints.cs` (ПЕРЕПИСАТЬ — упрощённый auto-login)

```csharp
using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Auto-login: возвращает JWT для demo user (clientId=1)
        group.MapPost("/login", async (
            AzureOpsCrewContext db,
            JwtTokenService jwtService) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == 1);
            if (user is null)
                return Results.Problem("Demo user not found. Wait for seeding.");

            user.MarkLogin();
            await db.SaveChangesAsync();

            var token = jwtService.CreateToken(user);
            return Results.Ok(new
            {
                accessToken = token.AccessToken,
                expiresAtUtc = token.ExpiresAtUtc,
                user = new { user.Id, user.Email, user.DisplayName }
            });
        });

        // Кто я
        group.MapGet("/me", async (HttpContext http, AzureOpsCrewContext db) =>
        {
            var userId = http.User.GetRequiredUserId();
            var user = await db.Users.FindAsync(userId);
            if (user is null) return Results.Unauthorized();
            return Results.Ok(new { user.Id, user.Email, user.DisplayName });
        }).RequireAuthorization();
    }
}
```

### 3.6 `Setup/Seeds/Seeder.cs` (ПЕРЕПИСАТЬ)

```csharp
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Setup.Seeds;

public class Seeder
{
    private readonly AzureOpsCrewContext _context;
    private readonly OpenAISettings _openAISettings;

    public Seeder(AzureOpsCrewContext context, OpenAISettings openAISettings)
    {
        _context = context;
        _openAISettings = openAISettings;
    }

    public async Task Seed()
    {
        const int clientId = 1;

        // 1. Demo User
        await SeedDemoUser(clientId);

        // 2. OpenAI Provider
        var providerId = Guid.Parse("5f4e3d10-0123-4000-9abc-def123456789");
        var provider = new Provider(providerId, clientId,
            name: "OpenAI", ProviderType.OpenAI,
            apiKey: _openAISettings.ApiKey,
            apiEndpoint: _openAISettings.BaseUrl ?? "https://api.openai.com",
            selectedModels: $"[\"{_openAISettings.Model}\"]",
            defaultModel: _openAISettings.Model);
        await AddIfNotExists(provider);

        // 3. Агенты
        var managerId   = Guid.Parse("6a5d8a20-1234-4000-a1b2-c3d4e5f6a7b8");
        var devopsId    = Guid.Parse("7b6e9b30-2345-4111-b2c3-d4e5f6a7b8c9");
        var devId       = Guid.Parse("8c7f0c40-3456-4222-c3d4-e5f6a7b8c9d0");
        var channelId   = Guid.Parse("a5d8a20a-1234-4000-a1b2-c3d4e5f6a7b9");

        var agents = new[]
        {
            CreateManagerAgent(managerId, clientId, providerId),
            CreateDevOpsAgent(devopsId, clientId, providerId),
            CreateDeveloperAgent(devId, clientId, providerId),
        };

        foreach (var agent in agents)
            await AddIfNotExists(agent);

        // 4. Канал "Ops Room"
        var channel = new Channel(channelId, clientId, "Ops Room")
        {
            Description = "Infrastructure operations crew",
            AgentIds = agents.Select(a => a.Id.ToString()).ToArray(),
        };
        await AddIfNotExists(channel);

        await _context.SaveChangesAsync();
    }

    private async Task SeedDemoUser(int clientId)
    {
        var exists = await _context.Users.AnyAsync(u => u.Id == clientId);
        if (!exists)
        {
            var user = new User(
                email: "demo@azureopscrew.com",
                displayName: "Demo User",
                passwordHash: new PasswordHasher<User>().HashPassword(null!, "demo123!"));
            _context.Users.Add(user);
        }
    }

    private Agent CreateManagerAgent(Guid id, int clientId, Guid providerId)
    {
        var prompt = @"You are the Manager of an Azure operations crew.

YOUR ROLE:
- You receive tasks from the human user
- You decompose complex tasks into smaller sub-tasks
- You delegate work to DevOps Agent and Developer Agent
- You collect results and report back to the user
- You ask the user for confirmation on critical actions (deployments, code changes)

COMMUNICATION STYLE:
- Be brief and action-oriented
- When delegating, clearly state what you need from which agent
- Summarize findings for the user
- Ask for human approval before destructive or deployment actions

When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), 
use them proactively to present information visually.";

        return new Agent(id, clientId,
            new AgentInfo("Manager", prompt, _openAISettings.Model)
            {
                Description = "Orchestrates the crew, delegates tasks, reports to user",
            },
            providerId, "manager", "#9b59b6");
    }

    private Agent CreateDevOpsAgent(Guid id, int clientId, Guid providerId)
    {
        var prompt = @"You are the DevOps Agent in an Azure operations crew.

YOUR ROLE:
- Monitor Azure infrastructure health
- Investigate errors and exceptions in Application Insights
- Check and manage CI/CD pipelines
- Report infrastructure issues to the team

AVAILABLE TOOLS (use them!):
- appinsights_query: Query Application Insights for errors, exceptions, performance
- resource_health: Check Azure resource health status
- pipeline_status: Get CI/CD pipeline status
- pipeline_run: Trigger a pipeline run

COMMUNICATION STYLE:
- Always start by querying real data before making assumptions
- Present findings with specific numbers and details
- Use visual cards (showMetrics, showPipelineStatus, showResourceInfo) when presenting data
- Clearly state what you found and recommend next steps";

        return new Agent(id, clientId,
            new AgentInfo("DevOps Agent", prompt, _openAISettings.Model)
            {
                Description = "Monitors infrastructure, diagnoses issues via App Insights and pipelines",
                AvailableTools = [AgentTool.AzureMcp]
            },
            providerId, "devops", "#0078d4");
    }

    private Agent CreateDeveloperAgent(Guid id, int clientId, Guid providerId)
    {
        var prompt = @"You are the Developer Agent in an Azure operations crew.

YOUR ROLE:
- Analyze code in repositories when errors are reported
- Read stack traces and identify root causes in code
- Propose and implement fixes
- Create pull requests with changes

AVAILABLE TOOLS (use them!):
- repo_file_read: Read files from code repositories
- repo_search: Search for code patterns
- repo_pr_create: Create pull requests with fixes

COMMUNICATION STYLE:
- Show relevant code snippets when discussing issues
- Be specific about file names, line numbers, and exact changes
- Always explain WHY a fix works, not just WHAT to change
- Ask for human confirmation before creating PRs";

        return new Agent(id, clientId,
            new AgentInfo("Developer", prompt, _openAISettings.Model)
            {
                Description = "Analyzes code, identifies bugs, proposes and implements fixes",
                AvailableTools = [AgentTool.AzureMcp]
            },
            providerId, "developer", "#43b581");
    }

    private async Task AddIfNotExists<T>(T entity) where T : class
    {
        // Generic check — работает для всех entity с Id
        var entityType = _context.Model.FindEntityType(typeof(T))!;
        var primaryKey = entityType.FindPrimaryKey()!;
        var keyValues = primaryKey.Properties
            .Select(p => p.PropertyInfo!.GetValue(entity))
            .ToArray();
        
        var existing = await _context.FindAsync<T>(keyValues);
        if (existing is null)
            _context.Add(entity);
    }
}
```

### 3.7 `Program.cs` (УПРОЩЁННЫЙ)

```csharp
using AzureOpsCrew.Api.Endpoints;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Api.Setup.Seeds;
using AzureOpsCrew.Domain.AgentServices;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting AzureOpsCrew API");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.ConfigureAppConfiguration((context, config) =>
    {
        config.Sources.Clear();
        config
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    });

    builder.Host.UseSerilog();

    // OpenAPI / Swagger 
    builder.Services.AddOpenApi();
    builder.Services.AddSwaggerGen();

    // Core services
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddProviderFacades();
    builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddAgentFactory(builder.Configuration);
    builder.Services.AddMcpTools(builder.Configuration);     // ← MCP tools
    builder.Services.Configure<OpenAISettings>(builder.Configuration.GetSection("OpenAI"));

    builder.Services.AddHttpClient();
    builder.Services.AddAGUI();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapSwagger();
        app.UseSwaggerUI();
        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    // Endpoints
    app.MapAuthEndpoints();      // упрощённый auto-login
    app.MapTestEndpoints();      // ping/pong
    app.MapAgentEndpoints();     // GET only
    app.MapChannelEndpoints();   // GET only
    app.MapCrewChat();           // ← ГЛАВНЫЙ endpoint
    app.MapAllAgUi();            // legacy AG-UI (оставляем для совместимости)

    // Setup
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
```

### 3.8 `appsettings.json` (ОБНОВЛЁННЫЙ)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  
  "DatabaseProvider": "Sqlite",
  "Sqlite": {
    "DataSource": "Data Source=azureopscrew.db"
  },
  
  "Jwt": {
    "Issuer": "AzureOpsCrew",
    "Audience": "AzureOpsCrewFrontend",
    "SigningKey": "",
    "AccessTokenMinutes": 480
  },
  
  "OpenAI": {
    "ApiKey": "",
    "Model": "gpt-4o-mini",
    "BaseUrl": null
  },
  
  "Mcp": {
    "ServerUrl": "",
    "ApiKey": "",
    "AllowedTools": []
  },
  
  "LongTermMemory": {
    "Type": "InMemory",
    "Neo4j": {
      "Uri": "bolt://localhost:7687",
      "Username": "neo4j",
      "Password": "password"
    }
  },
  
  "Seeding": {
    "IsEnabled": true
  }
}
```

---

## 4. Обновление AiAgentFactory

Текущий `AiAgentFactory` нужно доработать — он должен получать роль агента и подстраивать промпт:

```csharp
// В AiAgentFactory.Create() заменить SystemPrompt на:
var prompt = $@"
You are {agent.Info.Name}, part of the Azure Ops Crew — a team of AI agents
working together to manage Azure infrastructure.

{agent.Info.Prompt}

IMPORTANT RULES:
1. You are in a group chat with other agents and a human user
2. Address other agents by name when you need their help
3. Address the human as 'User' when you need their confirmation
4. Always use your tools when you need real data — don't guess
5. Be concise — this is a chat, not an essay
6. When presenting data, use visual cards (showPipelineStatus, showMetrics, etc.)
";
```

---

## 5. Зависимости (Api.csproj) — что ДОБАВИТЬ

```xml
<!-- MCP Client SDK (если есть NuGet пакет) -->
<!-- Иначе работаем через HTTP напрямую -->
```

Основные зависимости уже подключены:
- `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` ✅
- `Microsoft.Extensions.AI.OpenAI` ✅
- `Azure.AI.OpenAI` ✅
- `Microsoft.Agents.AI` (через Infrastructure.Ai) ✅

Что **убрать** из зависимостей:
- Ничего убирать не нужно — лишние просто не будут использоваться

---

## 6. ServiceCollectionExtensions.cs — изменения

### Убрать:
- `AddEmailVerification()` метод целиком
- В `AddJwtAuthentication()` — убрать проверку "ChangeThisDevelopmentOnly" (для демо это ок)

### Добавить:
- `AddMcpTools()` вызов в `Program.cs`
- `Configure<OpenAISettings>` в `Program.cs`

### Упростить `AddJwtAuthentication`:
Для демо достаточно не валидировать ключ так строго. Оставить минимум — 16+ символов.
