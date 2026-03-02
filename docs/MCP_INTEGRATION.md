# Интеграция с Azure MCP Server

## Что такое MCP

MCP (Model Context Protocol) — стандартный протокол для подключения AI агентов к внешним инструментам и данным. Azure MCP Server предоставляет tools для работы с Azure-ресурсами.

## Архитектура подключения

```
Agent (в .NET backend)
  │
  ├─ DevOps Agent      → MCP tools: appinsights, pipelines, resources
  ├─ Developer Agent   → MCP tools: repos, code search, PRs
  └─ Manager           → Нет MCP tools (только оркестрация)
      │
      ▼
  McpToolProvider (.NET)
      │
      ▼ HTTP/SSE (MCP protocol)
  Azure MCP Server (внешний)
      │
      ├─ Application Insights
      ├─ Azure DevOps (Repos, Pipelines, Boards)
      ├─ Azure Resource Manager
      └─ Azure Monitor
```

## Варианты подключения к MCP

### Вариант A: HTTP REST (самый простой)

Если Azure MCP Server предоставляет REST API:

```csharp
// Вызов MCP tool через HTTP
var response = await httpClient.PostAsJsonAsync(
    $"{mcpServerUrl}/tools/{toolName}/call",
    new { arguments = toolArguments });
var result = await response.Content.ReadAsStringAsync();
```

### Вариант B: MCP SDK for .NET (если есть)

Microsoft может предоставить NuGet пакет:
```xml
<PackageReference Include="Microsoft.Mcp.Client" Version="..." />
```

```csharp
var mcpClient = new McpClient(mcpServerUrl, apiKey);
var tools = await mcpClient.ListToolsAsync();
var result = await mcpClient.CallToolAsync("appinsights_query", arguments);
```

### Вариант C: SSE-based MCP (стандарт)

MCP протокол использует SSE для streaming:
```csharp
// 1. Discover tools
GET {mcpServerUrl}/sse → SSE stream с доступными tools

// 2. Call tool  
POST {mcpServerUrl}/messages
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "appinsights_query",
    "arguments": { "query": "exceptions | take 10" }
  }
}
```

## Реализация в нашем бэкенде

### `McpToolProvider.cs` — ключевой класс

Этот класс:
1. При старте подключается к MCP серверу и получает список доступных tools
2. Создаёт `AITool` wrapper для каждого MCP tool
3. Фильтрует tools по роли агента
4. Вызывает MCP tool через HTTP когда агент его использует

```csharp
public class McpToolProvider
{
    // Инициализация:
    // 1. GET /tools → список tools со схемами
    // 2. Для каждого tool создать AIFunction:
    //    AIFunctionFactory.Create(
    //      (args) => CallMcpTool(toolName, args),
    //      name: tool.name,
    //      description: tool.description,
    //      parameters: tool.inputSchema
    //    )

    // Вызов:
    // POST /tools/{name}/call { arguments: {...} }
    // → возвращает string result
}
```

### Фильтрация tools по агенту

```csharp
public IReadOnlyList<AITool> GetToolsForAgent(string role)
{
    // DevOps Agent получает:
    // - appinsights_* (мониторинг, ошибки)
    // - pipeline_* (CI/CD)
    // - resource_* (Azure ресурсы)
    
    // Developer Agent получает:
    // - repo_* (код, файлы)
    // - code_* (анализ)
    // - pr_* (pull requests)
    
    // Manager: никаких MCP tools
    // (может только читать результаты других агентов в чате)
}
```

## Конфигурация

### .env
```env
MCP_SERVER_URL=https://your-azure-mcp-server.azurewebsites.net
MCP_API_KEY=your-api-key-if-needed
```

### appsettings.json
```json
{
  "Mcp": {
    "ServerUrl": "",
    "ApiKey": "",
    "AllowedTools": []
  }
}
```

## Реальные MCP tools от Azure

Когда подключишь реальный Azure MCP Server, типичные tools будут:

| Tool | Description | Агент |
|------|-------------|-------|
| `azure_monitor_query_logs` | KQL query against Log Analytics / App Insights | DevOps |
| `azure_monitor_get_metrics` | Get resource metrics | DevOps |
| `azure_resource_list` | List resources in subscription | DevOps |
| `azure_resource_get` | Get resource details | DevOps |
| `azure_devops_pipeline_list` | List pipelines | DevOps |
| `azure_devops_pipeline_run` | Trigger pipeline | DevOps |
| `azure_devops_repo_files` | Browse repo files | Developer |
| `azure_devops_repo_search` | Search code | Developer |
| `azure_devops_pr_create` | Create pull request | Developer |
| `azure_devops_workitems_query` | Query work items | Manager |

## Фоллбэк (если MCP недоступен)

Для демо на хакатоне можно сделать mock-ответы:

```csharp
private async Task<string> CallMcpToolAsync(string tool, object args)
{
    try 
    {
        // Реальный вызов MCP
        return await httpClient.PostAsJsonAsync(...);
    }
    catch (Exception ex)
    {
        // Фоллбэк: mock data для демо
        return GenerateMockResponse(tool, args);
    }
}

private string GenerateMockResponse(string tool, object args)
{
    return tool switch
    {
        "appinsights_query" => """
        {
            "errors": [
                {"type": "NullReferenceException", "count": 43, 
                 "location": "OrderController.cs:142", "lastSeen": "5 min ago"},
                {"type": "TimeoutException", "count": 12,
                 "location": "PaymentService.cs:89", "lastSeen": "15 min ago"}
            ]
        }
        """,
        "resource_health" => """
        {
            "resources": [
                {"name": "api-prod", "type": "App Service", "status": "Healthy", "region": "westeurope"},
                {"name": "db-prod", "type": "SQL Database", "status": "Degraded", "region": "westeurope"}
            ]
        }
        """,
        _ => """{"message": "Tool executed successfully", "mock": true}"""
    };
}
```

Это позволит демонстрировать workflow даже без реального MCP сервера.
