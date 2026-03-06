# Blueprint: Архитектура для AzureOpsCrew на базе паттернов Claude Code

## Цель

Воспроизвести проверенные архитектурные паттерны Claude Code в системе AzureOpsCrew, адаптировав их под .NET 10 backend + Next.js frontend.

## Минимальная целевая архитектура (MVP)

```
┌──────────────────────────────────────────────────────────────────┐
│                    AZUREOPS AGENT RUNTIME                         │
│                                                                  │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐     │
│  │ Config Manager │  │ Agent Registry │  │ MCP Client     │     │
│  │ (2-level:      │  │ (YAML/MD agent │  │ (existing MCP  │     │
│  │  user+project) │  │  definitions)  │  │  integration)  │     │
│  └───────┬────────┘  └───────┬────────┘  └───────┬────────┘     │
│          │                   │                    │              │
│          └───────────────────┼────────────────────┘              │
│                              │                                   │
│                     ┌────────▼────────┐                          │
│                     │  ORCHESTRATOR   │                          │
│                     │  (Main Agent    │                          │
│                     │   Loop)         │                          │
│                     └────────┬────────┘                          │
│                              │                                   │
│          ┌───────────────────┼───────────────────┐               │
│          │                   │                   │               │
│    ┌─────▼─────┐      ┌─────▼─────┐      ┌─────▼─────┐        │
│    │ Permission│      │ Hook      │      │ Context   │        │
│    │ Check     │      │ Pipeline  │      │ Manager   │        │
│    │ (allow/   │      │ (Pre/Post │      │ (compact, │        │
│    │  deny/ask)│      │  ToolUse) │      │  resume)  │        │
│    └───────────┘      └───────────┘      └───────────┘        │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                    TOOL ROUTER                              │  │
│  │  Built-in tools │ MCP tools │ Agent tool (subagent spawn) │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                    MEMORY LAYER                             │  │
│  │  Project Instructions │ Auto-Memory │ Todo State │ History │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

## Компоненты и маппинг Claude Code → AzureOpsCrew

### Phase 1: Core (2-3 недели)

| # | Claude Code | AzureOpsCrew | Реализация |
|---|---|---|---|
| 1 | Main Agent Loop | `Orchestration/AgentLoop.cs` | While loop: prompt → LLM → parse → execute → loop |
| 2 | Tool Router | `Orchestration/ToolRouter.cs` | Switch по tool name → built-in или MCP dispatch |
| 3 | Permission Model | `Auth/PermissionManager.cs` | YAML config: allow/deny per tool + per agent |
| 4 | Agent Tool | `Orchestration/SubagentSpawner.cs` | Spawn субагента с ограниченным контекстом и tools |
| 5 | Agent Definitions | `data/agents/*.md` | Markdown с YAML frontmatter (name, tools, description) |

### Phase 2: Stability (1-2 недели)

| # | Claude Code | AzureOpsCrew | Реализация |
|---|---|---|---|
| 6 | Compaction | `Orchestration/ContextCompactor.cs` | Summary generation при 80% threshold |
| 7 | Todo Pinning | `Orchestration/PinnedState.cs` | Todo list выживает компакцию |
| 8 | Process Cleanup | `Orchestration/ProcessManager.cs` | Track all child processes, cleanup on exit |
| 9 | Timeout Enforcement | `Orchestration/TimeoutManager.cs` | Per-tool timeouts + kill |
| 10 | Session Resume | `Orchestration/SessionStore.cs` | Persist state to disk, resume on startup |

### Phase 3: Extensibility (2-3 недели)

| # | Claude Code | AzureOpsCrew | Реализация |
|---|---|---|---|
| 11 | Hook System | `Hooks/HookPipeline.cs` | PreToolUse/PostToolUse events + external scripts |
| 12 | Project Instructions | `data/INSTRUCTIONS.md` | Аналог CLAUDE.md для проектных правил |
| 13 | Skills | `data/skills/` | SKILL.md с auto-loading по intent |
| 14 | Auto-Memory | `Memory/AutoMemory.cs` | Persistent learnings между сессиями |
| 15 | Plugin System | `Plugins/PluginLoader.cs` | Plugin manifest + components registration |

## Формат Agent Definition (для AzureOpsCrew)

```yaml
# data/agents/azure-cost-optimizer.md
---
name: azure-cost-optimizer
description: |
  Use this agent when the user asks about Azure cost optimization,
  finding unused resources, or reducing cloud spending.
  <example>
    Context: User wants to reduce Azure costs
    user: Analyze my Azure subscription for cost savings
    assistant: [calls Agent tool with azure-cost-optimizer]
  </example>
model: inherit
tools:
  - AzureResourceGraph
  - AzureCostManagement
  - Read
  - Grep
---

You are an Azure cost optimization specialist.

## Responsibilities
- Query Azure Resource Graph for resource inventories
- Analyze Azure Cost Management data for spending patterns
- Identify underutilized and orphaned resources
- Calculate potential savings with specific recommendations

## Process
1. Query current resource inventory
2. Analyze utilization metrics (last 30 days)
3. Identify optimization opportunities
4. Calculate potential monthly savings
5. Generate prioritized recommendations

## Output Format
Return a markdown report with:
- Executive summary
- Top 10 savings opportunities (sorted by $ impact)
- Implementation steps for each
- Estimated total monthly savings
```

## Формат Hook Definition

```json
// hooks/azure-safety.json
{
  "hooks": [
    {
      "event": "PreToolUse",
      "matcher": "AzureResourceGraph",
      "hooks": [
        {
          "type": "command",
          "command": "dotnet run --project scripts/ValidateAzureQuery.csproj"
        }
      ]
    },
    {
      "event": "PreToolUse",
      "matcher": "Bash(az group delete:*)",
      "hooks": [
        {
          "type": "command",
          "command": "python3 scripts/block_destructive_azure.py"
        }
      ]
    }
  ]
}
```

## Формат Permission Config

```yaml
# config/permissions.yaml
permissions:
  allow:
    - AzureResourceGraph
    - AzureCostManagement
    - Read
    - Grep
    - Glob
    - LS
    - Bash(az account show:*)
    - Bash(az group list:*)
  deny:
    - Bash(az group delete:*)
    - Bash(az resource delete:*)
    - Bash(rm -rf:*)
  ask:
    - Write
    - Edit
    - Bash(az deployment:*)
```

## Интерфейсы компонентов

### IAgentLoop
```csharp
public interface IAgentLoop
{
    Task<AgentResult> RunAsync(AgentRequest request, CancellationToken ct);
    event Action<ToolCall> OnToolCall;
    event Action<CompactionEvent> OnCompaction;
}
```

### IToolRouter
```csharp
public interface IToolRouter
{
    Task<ToolResult> ExecuteAsync(ToolCall call, AgentContext context, CancellationToken ct);
    IReadOnlyList<ToolDefinition> GetAvailableTools(string? agentName = null);
}
```

### IPermissionManager
```csharp
public interface IPermissionManager
{
    PermissionDecision Check(ToolCall call, AgentContext context);
    // Returns: Allow, Deny, Ask
}
```

### IHookPipeline
```csharp
public interface IHookPipeline
{
    Task<HookResult> ExecutePreToolUseAsync(ToolCall call, CancellationToken ct);
    Task ExecutePostToolUseAsync(ToolCall call, ToolResult result, CancellationToken ct);
    Task ExecuteSessionStartAsync(SessionContext context, CancellationToken ct);
}
```

### IContextCompactor
```csharp
public interface IContextCompactor
{
    Task<CompactedContext> CompactAsync(ConversationContext context, CancellationToken ct);
    bool ShouldCompact(ConversationContext context); // true when ~80% full
}
```

### ISubagentSpawner
```csharp
public interface ISubagentSpawner
{
    Task<SubagentResult> SpawnAsync(
        string agentName,
        string task,
        AgentContext parentContext,
        CancellationToken ct);
}
```

## Anti-patterns (чего НЕ делать)

| # | Антипаттерн | Правильно |
|---|---|---|
| 1 | Полный контекст субагенту | Только task description |
| 2 | Вложенные субагенты | Max depth = 1 |
| 3 | P2P между субагентами | Всё через main agent |
| 4 | Unbounded buffers | CircularBuffer / compaction |
| 5 | Shared mutable state | Isolated agent contexts |
| 6 | No timeout on tools | Always timeout + kill |
| 7 | Inline LLM validation | Deterministic hooks (scripts) |
| 8 | Monolithic permissions | Per-tool, per-agent granularity |

## Порядок реализации

```
Week 1-2: Core Loop
  └── AgentLoop + ToolRouter + PermissionManager
  └── 3 built-in tools (Read, Bash, AzureResourceGraph)
  └── Basic prompt assembly

Week 3: Subagents
  └── SubagentSpawner
  └── Agent definitions in Markdown
  └── Tool restrictions per agent

Week 4: Stability
  └── ContextCompactor
  └── Todo pinning
  └── Process cleanup
  └── Timeout enforcement

Week 5-6: Extensibility
  └── HookPipeline (PreToolUse, PostToolUse)
  └── Project instructions loading
  └── Auto-memory
  └── Session resume

Week 7-8: Polish
  └── Plugin system (if needed)
  └── Skills auto-loading
  └── MCP tool integration improvements
  └── Testing & hardening
```

---

*Blueprint основан на реверс-инжиниринге Claude Code v2.1.x*
*Все файлы анализа: [SUMMARY.md](SUMMARY.md)*
