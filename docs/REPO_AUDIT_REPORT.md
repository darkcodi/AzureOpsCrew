# AzureOpsCrew Repository Audit Report

**Date:** 2026-03-03  
**Auditor:** Repo Forensics Agent  
**Scope:** Multi-agent orchestration, prompts, MCP integration, evidence pipeline, loop prevention

---

## 1. Repository Map (Key Directories)

```
AzureOpsCrew/
├── backend/
│   ├── src/
│   │   ├── Api/
│   │   │   ├── Endpoints/              # API entry points (AgUiEndpoints.cs)
│   │   │   ├── Orchestration/          # ⭐ CORE: GroupChat managers, context management
│   │   │   │   ├── Engine/             # TaskExecutionEngine, TaskPlanner, TaskExecutor
│   │   │   │   ├── MultiRoundGroupChatManager.cs  # Primary orchestrator
│   │   │   │   ├── ManagerOrchestratorGroupChatManager.cs  # Simpler 1-round version
│   │   │   │   ├── RunContext.cs       # Run state tracking
│   │   │   │   ├── ContextBudgetManager.cs  # Token budget management
│   │   │   │   ├── ToolAuthorizationPolicy.cs  # Role-based tool access
│   │   │   │   └── ApprovalPolicy.cs   # Dangerous ops approval gates
│   │   │   ├── Mcp/                    # MCP server integration
│   │   │   │   ├── McpToolProvider.cs  # ⭐ Tool discovery, invocation, mock fallback
│   │   │   │   └── McpArgumentNormalizer.cs
│   │   │   ├── Settings/               # Configuration classes
│   │   │   │   ├── McpSettings.cs      # 4 MCP servers config
│   │   │   │   ├── OrchestrationSettings.cs  # Rounds, timeouts
│   │   │   │   └── ExecutionEngineSettings.cs  # Budgets, loops
│   │   │   └── Setup/Seeds/
│   │   │       └── Seeder.cs           # ⭐ AGENT PROMPTS DEFINED HERE
│   │   └── Domain/
│   │       └── Execution/              # Task, Run, Artifact entities
│   └── data/
│       └── service-registry.yaml       # Service-to-resource mapping
├── frontend/                           # Next.js + AG-UI
└── docs/                               # Architecture docs
```

---

## 2. Entry Points & Orchestration Flow

### 2.1 Entry Points

| Endpoint | File | Purpose |
|----------|------|---------|
| `POST /api/channels/{id}/agui` | [AgUiEndpoints.cs](backend/src/Api/Endpoints/AgUiEndpoints.cs#L109) | **Main entry** — Channel-level multi-agent chat |
| `POST /api/agents/{id}/agui` | [AgUiEndpoints.cs](backend/src/Api/Endpoints/AgUiEndpoints.cs#L40) | Single-agent chat (not multi-agent) |

### 2.2 Orchestration Flow (Multi-Agent)

```
User Message → POST /api/channels/{id}/agui
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ AgUiEndpoints.MapAllAgUi (line 109)                             │
│   1. ContextBudgetManager.CompactMessages() — trim history      │
│   2. Load Channel → Agents → Providers from DB                  │
│   3. Create AIAgents:                                           │
│      ├─ Manager: NO TOOLS, injected with ServiceRegistry +      │
│      │           MCP diagnostics in prompt                      │
│      └─ Workers: MCP tools via McpToolProvider.GetToolsForAgent │
│   4. Build Workflow with MultiRoundGroupChatManager             │
│   5. Run streaming, wrap with termination guard                 │
└─────────────────────────────────────────────────────────────────┘
      │
      ▼
┌─────────────────────────────────────────────────────────────────┐
│ MultiRoundGroupChatManager (lines 1-240)                        │
│                                                                 │
│ Per Round:                                                      │
│   1. Manager speaks first (analyzes, creates TRIAGE+PLAN)       │
│   2. ParseDelegation() — detect agent names in Manager text     │
│   3. Queue mentioned workers (DevOps, Developer)                │
│   4. Each worker speaks with tool access                        │
│   5. Loop until:                                                │
│      - [RESOLVED] or [APPROVAL REQUIRED] detected               │
│      - No workers mentioned (Manager answered directly)         │
│      - MaxRoundsPerRun reached (default: 5)                     │
│      - MaxConsecutiveNonToolTurns reached (default: 3)          │
└─────────────────────────────────────────────────────────────────┘
```

### 2.3 Key Sequence (Code Paths)

1. **Request Received:** [AgUiEndpoints.cs#L109](backend/src/Api/Endpoints/AgUiEndpoints.cs#L109)
2. **Budget Compaction:** [AgUiEndpoints.cs#L151](backend/src/Api/Endpoints/AgUiEndpoints.cs#L151) → `ContextBudgetManager.CompactMessages()`
3. **Manager Creation:** [AgUiEndpoints.cs#L181](backend/src/Api/Endpoints/AgUiEndpoints.cs#L181) → `ChannelAgUiFactory.CreateManagerAgent()`
4. **Worker Creation:** [AgUiEndpoints.cs#L193](backend/src/Api/Endpoints/AgUiEndpoints.cs#L193) → `McpToolProvider.GetToolsForAgentAsync()`
5. **Workflow Build:** [AgUiEndpoints.cs#L227](backend/src/Api/Endpoints/AgUiEndpoints.cs#L227) → `ChannelAgUiFactory.BuildWorkflow()`
6. **Round Selection:** [MultiRoundGroupChatManager.cs#L52](backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs#L52) → `SelectNextAgentAsync()`
7. **Termination Check:** [MultiRoundGroupChatManager.cs#L89](backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs#L89) → `ShouldTerminateAsync()`
8. **Delegation Parse:** [MultiRoundGroupChatManager.cs#L166](backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs#L166) → `ParseDelegation()`

---

## 3. Agents & Prompts Inventory

All agent prompts are defined in **[Seeder.cs](backend/src/Api/Setup/Seeds/Seeder.cs)** lines 24-314.

| Agent | Prompt Location | Key Rules | Critical Sections |
|-------|-----------------|-----------|-------------------|
| **Manager** | [Seeder.cs#L27-L113](backend/src/Api/Setup/Seeds/Seeder.cs#L27) | Read-only oversight, NO tools, delegates by mentioning names | `[TRIAGE]`, `[PLAN]`, `[RESOLVED]`, `[APPROVAL REQUIRED]` |
| **DevOps** | [Seeder.cs#L116-L213](backend/src/Api/Setup/Seeds/Seeder.cs#L116) | 3 MCP servers (Azure, Platform, ADO read-only), evidence-first | `[EVIDENCE]`, `[INTERPRETATION]`, `[HYPOTHESIS]`, `[RECOMMENDED ACTION]` |
| **Developer** | [Seeder.cs#L216-L314](backend/src/Api/Setup/Seeds/Seeder.cs#L216) | ADO (rw) + GitOps (rw), no Azure/Platform access | `[EVIDENCE]`, `[ROOT CAUSE]`, `[FIX PROPOSAL]`, `[VERIFICATION PLAN]` |

### 3.1 Manager Prompt (Critical Excerpts)

```
═══ YOUR ROLE ═══
You are an incident commander / planner / coordinator. You:
- Accept tasks from the user
- Build an initial plan
- Decompose into steps
- Assign tasks to DevOps and Developer
- Monitor progress and evidence quality
- Stop at approval checkpoints
- Produce the final summary

═══ YOUR MCP ACCESS ═══
You have READ-ONLY access to:
- Azure MCP (read-only), Platform MCP (read-only), Azure DevOps MCP (read-only)
You CANNOT call any write/modify/create/delete/deploy operations.

═══ ROUTING POLICY ═══
• Infrastructure / runtime / config / network / secrets → delegate to DevOps
• Code analysis / fix / branch / PR / pipeline / deploy flow → delegate to Developer
• Mixed (infra + code) → First DevOps, then Developer

═══ CRITICAL RULES ═══
• Your FIRST response MUST contain [TRIAGE] + [PLAN] + delegation.
• NEVER respond with ONLY triage. Always include plan and delegation.
• Never say 'I'll wait' or 'Let me know' — ACT by delegating NOW.
```

### 3.2 DevOps Prompt (Critical Excerpts)

```
═══ YOUR MCP ACCESS — YOU HAVE 3 MCP SERVERS ═══

1️⃣ Azure MCP — Resource listing, management, diagnostics
2️⃣ Platform MCP — ARG queries (comprehensive inventory), Container Apps, Key Vault, App Insights
3️⃣ Azure DevOps MCP — READ-ONLY for pipelines, repos, work items

❌ GitOps MCP — NO ACCESS

═══ CRITICAL: USE ALL MCP SERVERS ═══
When you need to gather comprehensive data:
• For listing ALL Azure resources: use tools from BOTH Azure MCP AND Platform MCP.
  - Platform MCP's ARG query tool gives the most complete picture (all resource types).
  - Azure MCP's list resources tool may have additional details.
  - ALWAYS cross-reference results from both servers to ensure completeness.

═══ EVIDENCE-FIRST PROTOCOL ═══
1. READ the Manager's instruction carefully.
2. IMMEDIATELY call your MCP tools. Your FIRST action must be tool calls.
3. Call tools from ALL relevant MCP servers, not just one.
4. Structure response:
   **[EVIDENCE]** tool results with key data points
   **[INTERPRETATION]** what the data means
   **[HYPOTHESIS]** suspected root cause
   **[RECOMMENDED ACTION]** what should be done next
```

### 3.3 Message Format Templates

The structured output formats are **embedded in the prompts** (not separate template files):

| Format | Used By | Purpose |
|--------|---------|---------|
| `[TRIAGE]` | Manager | Service, Environment, Severity, Goal classification |
| `[PLAN]` | Manager | Numbered task assignments |
| `[EVIDENCE]` | DevOps, Developer | Tool-based facts |
| `[INTERPRETATION]` | DevOps, Developer | What data means |
| `[HYPOTHESIS]` | DevOps | Suspected root cause with confidence |
| `[RECOMMENDED ACTION]` | DevOps, Developer | Next steps |
| `[HANDOFF → Developer]` | DevOps | Structured package for code issues |
| `[VERIFICATION RESULT]` | DevOps | Post-fix validation |
| `[RESOLVED]` | Manager | Final summary (triggers termination) |
| `[APPROVAL REQUIRED]` | Manager | Approval gate (triggers wait state) |

---

## 4. MCP Inventory & Routing

### 4.1 MCP Servers Configuration

**Config Location:** [McpSettings.cs](backend/src/Api/Settings/McpSettings.cs)

| Server | Purpose | Auth | Env Vars | Tools Available |
|--------|---------|------|----------|-----------------|
| **Azure MCP** | Resource listing, health, management | OAuth2 client_credentials | `MCP_AZURE_*` | `azure_list_resources`, `azure_get_resource_details`, `azure_list_resource_groups` |
| **Platform MCP** | ARG queries, Container Apps, Key Vault, App Insights | OAuth2 client_credentials | `MCP_PLATFORM_*` | `platform_arg_query_resources`, `platform_containerapp_*`, `platform_keyvault_*` |
| **Azure DevOps MCP** | Pipelines, Repos, Work Items | OAuth2 client_credentials | `MCP_ADO_*` | `ado_list_pipelines`, `ado_get_pipeline_runs`, `ado_list_repos`, `ado_list_work_items` |
| **GitOps MCP** | Branches, Commits, PRs | OAuth2 client_credentials | `MCP_GITOPS_*` | `gitops_commit`, `gitops_create_branch`, `gitops_create_pr` |

### 4.2 Role-Based Tool Routing

**Routing Logic:** [McpToolProvider.GetToolsForAgentAsync()](backend/src/Api/Mcp/McpToolProvider.cs#L248)  
**Authorization Enforcement:** [ToolAuthorizationPolicy.cs](backend/src/Api/Orchestration/ToolAuthorizationPolicy.cs)

```
┌───────────┬───────────────┬─────────────┬──────────────┬──────────────┐
│  Agent    │ Azure MCP     │ Platform MCP│ ADO MCP      │ GitOps MCP   │
├───────────┼───────────────┼─────────────┼──────────────┼──────────────┤
│ Manager   │ read-only     │ read-only   │ read-only    │ NO ACCESS    │
│ DevOps    │ read+write    │ read+write  │ read-only    │ NO ACCESS    │
│ Developer │ NO ACCESS     │ NO ACCESS   │ read+ops     │ read+write   │
└───────────┴───────────────┴─────────────┴──────────────┴──────────────┘
```

### 4.3 Why "List All Azure Resources" May Be Incomplete

**Problem Location:** [McpToolProvider.cs#L872-L913](backend/src/Api/Mcp/McpToolProvider.cs#L872)

#### Expected Behavior
DevOps should call tools from **BOTH** Azure MCP and Platform MCP:
1. `azure_list_resources` — basic resource list
2. `platform_arg_query_resources` — comprehensive ARG query

#### Actual Current Behavior

1. **Mock Tools Active:** If MCP servers aren't configured, mock tools return hardcoded subset ([McpToolProvider.cs#L930](backend/src/Api/Mcp/McpToolProvider.cs#L930))

2. **No Automatic Cross-Server Aggregation:** The system relies on **agent prompts** to instruct calling both servers. There's no code-level enforcement.

3. **Result Truncation:** Large results are truncated at 8000 chars ([McpToolProvider.cs#L836](backend/src/Api/Mcp/McpToolProvider.cs#L836)):
   ```csharp
   private const int MaxToolResultChars = 8000;
   ```

4. **No Pagination Logic:** Neither mock tools nor real MCP invocation handle pagination. If Azure has 100+ resources, only partial results return.

5. **Prompt Gap:** The DevOps prompt mentions using both servers, but doesn't **force** it. The agent may call only one.

---

## 5. Evidence Pipeline

### 5.1 Evidence Schema

**Entity:** [Artifact.cs](backend/src/Domain/Execution/Artifact.cs)

```csharp
public class Artifact
{
    public Guid Id { get; }
    public Guid RunId { get; }           // Links to ExecutionRun
    public Guid? TaskId { get; }         // Links to ExecutionTask
    public ArtifactType ArtifactType { get; }  // ToolOutput, LogSnippet, KqlResult, etc.
    public string? Source { get; }       // Where the artifact came from
    public string? CreatedBy { get; }    // Agent name
    public string Content { get; }       // Actual data or reference
    public string? Summary { get; }      // Human-readable summary
    public string? Tags { get; }         // Comma-separated for filtering
}

public enum ArtifactType
{
    ToolOutput = 10,
    LogSnippet = 20,
    KqlResult = 25,
    HealthSnapshot = 30,
    HandoffPackage = 100,
    ApprovalPackage = 110,
    // ... more types
}
```

### 5.2 Evidence Collection Flow

```
TaskExecutor.ExecuteTaskAsync()
      │
      ├─ Call MCP tools via InvokeMcpToolAsync()
      │
      ├─ Parse tool results
      │
      ├─ Store as Artifact (if significant):
      │     artifact = Artifact.Create(runId, type, content, agentName, taskId);
      │     _db.Artifacts.Add(artifact);
      │
      └─ Record in JournalEntry:
            JournalEntry.Create(runId, JournalEntryType.EvidenceAdded, evidence, agentName, taskId);
```

### 5.3 Evidence Rendering (in Messages)

Evidence is **embedded directly in agent text responses** using the structured format:
```
**[EVIDENCE]**
- Tool azure_list_resources returned: {...}
- Tool platform_arg_query returned: {...}
```

The UI renders this as markdown with the tool results inline.

---

## 6. Loop Prevention & Stopping Conditions

### 6.1 Multi-Layer Protection

| Layer | Mechanism | Location | Default Value |
|-------|-----------|----------|---------------|
| **Orchestration** | MaxRoundsPerRun | [OrchestrationSettings.cs#L11](backend/src/Api/Settings/OrchestrationSettings.cs#L11) | 5 |
| **Orchestration** | MaxConsecutiveNonToolTurns | [OrchestrationSettings.cs#L14](backend/src/Api/Settings/OrchestrationSettings.cs#L14) | 3 |
| **Orchestration** | IdleTimeoutSeconds | [OrchestrationSettings.cs#L17](backend/src/Api/Settings/OrchestrationSettings.cs#L17) | 45 |
| **Orchestration** | TotalTimeoutMinutes | [OrchestrationSettings.cs#L20](backend/src/Api/Settings/OrchestrationSettings.cs#L20) | 5 |
| **Engine** | MaxStepsPerRun | [ExecutionEngineSettings.cs#L11](backend/src/Api/Settings/ExecutionEngineSettings.cs#L11) | 50 |
| **Engine** | MaxStepsPerTask | [ExecutionEngineSettings.cs#L14](backend/src/Api/Settings/ExecutionEngineSettings.cs#L14) | 15 |
| **Engine** | MaxReplans | [ExecutionEngineSettings.cs#L17](backend/src/Api/Settings/ExecutionEngineSettings.cs#L17) | 5 |
| **Engine** | MaxConsecutiveNonProgressSteps | [ExecutionEngineSettings.cs#L20](backend/src/Api/Settings/ExecutionEngineSettings.cs#L20) | 5 |
| **Engine** | MaxRetriesPerTask | [ExecutionEngineSettings.cs#L32](backend/src/Api/Settings/ExecutionEngineSettings.cs#L32) | 2 |
| **Workflow** | MaximumIterationCount | [MultiRoundGroupChatManager.cs#L50](backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs#L50) | (agents.Count + 1) * MaxRoundsPerRun + 3 |

### 6.2 Termination Conditions (Code)

**Location:** [MultiRoundGroupChatManager.ShouldTerminateAsync()](backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs#L89)

```csharp
// Termination triggers:
// 1. Manager says [RESOLVED]
if (managerText.Contains("[RESOLVED]", StringComparison.OrdinalIgnoreCase))
    _terminated = true;

// 2. Manager says [APPROVAL REQUIRED]
if (managerText.Contains("[APPROVAL REQUIRED]", StringComparison.OrdinalIgnoreCase))
    _terminated = true;

// 3. No workers delegated (Manager answered directly)
if (_delegationQueue.Count == 0)
    _terminated = true;

// 4. Max non-tool turns reached
if (_consecutiveNonToolTurns >= _settings.MaxConsecutiveNonToolTurns)
    _terminated = true;
```

### 6.3 Context Budget Management (Anti-Overflow)

**Location:** [ContextBudgetManager.cs](backend/src/Api/Orchestration/ContextBudgetManager.cs)

```csharp
// Defaults
private const int DefaultMaxContextTokens = 128000;
private const int DefaultTargetBudget = 32000;
private const int DefaultMaxToolResultTokens = 2000;
private const int DefaultMaxMessagesInWindow = 40;

// Compaction Strategy:
// 1. Keep all system messages
// 2. Apply sliding window (keep last 40 messages)
// 3. Truncate large tool results to 2000 tokens
// 4. Summarize old messages
// 5. Emergency: remove oldest turns if still over budget
```

### 6.4 Tool Result Truncation

**Location:** [McpToolProvider.FormatToolResult()](backend/src/Api/Mcp/McpToolProvider.cs#L836)

```csharp
private const int MaxToolResultChars = 8000;

// Truncates at natural boundary (newline) with notice:
// "[... result truncated from {original} to {truncated} chars to fit context budget.
//  If you need more data, use more specific filters or query parameters.]"
```

### 6.5 Missing: Pagination Handling

**GAP IDENTIFIED:** There is **no pagination logic** for tool results. If a tool returns truncated data (e.g., "showing 50 of 200 resources"), the system does not:
1. Detect the truncation
2. Automatically request the next page
3. Merge paginated results

This is a key reason why "list all resources" may return incomplete results.

---

## 7. Case Study: "List All Azure Resources"

### 7.1 Expected Behavior

User asks: "List all Azure resources"

1. Manager triages, delegates to DevOps
2. DevOps calls:
   - `azure_list_resources` (Azure MCP)
   - `platform_arg_query_resources` with query `resources | project name, type, location, resourceGroup` (Platform MCP)
3. DevOps merges results, presents complete list
4. Manager synthesizes and concludes

### 7.2 Actual Behavior (Issues Identified)

#### Issue 1: Single MCP Call
DevOps may call only **one** MCP server instead of both. The prompt encourages using both, but doesn't enforce it.

**Evidence:** DevOps prompt says "ALWAYS cross-reference results from both servers" but the agent may still call only one.

#### Issue 2: Mock Data Returns Subset
When MCP servers aren't configured, mock tools return hardcoded 9-15 resources:

[McpToolProvider.cs#L930-L964](backend/src/Api/Mcp/McpToolProvider.cs#L930):
```csharp
private static string MockAzureListResources(IDictionary<string, object?>? args)
{
    return """
    {
      "resources": [
        {"name": "ca-azure-mcp-server", ...},
        {"name": "ca-azuredevops-mcp-server", ...},
        // ... only 9 resources hardcoded
      ],
      "totalCount": 9
    }
    """;
}
```

#### Issue 3: No Real Pagination
Neither mock nor real MCP invocation handles pagination. Real Azure subscriptions with 100+ resources would get truncated.

#### Issue 4: Result Size Limit
[McpToolProvider.cs#L836](backend/src/Api/Mcp/McpToolProvider.cs#L836):
```csharp
private const int MaxToolResultChars = 8000;
```
Large resource lists get truncated with a notice, but there's no follow-up to get remaining data.

#### Issue 5: Agent Doesn't Always Follow Protocol
Even with good prompts, the LLM may:
- Skip the second MCP call
- Summarize instead of listing all
- Stop after getting "enough" data

### 7.3 Recommended Fixes

| Fix | Location | Change |
|-----|----------|--------|
| **1. Add tool-level aggregation** | `McpToolProvider` | Create `GetComprehensiveResourceList()` that automatically calls both Azure and Platform MCP, merges results |
| **2. Implement pagination** | `McpToolProvider.InvokeToolAsync()` | Detect truncation markers, loop to get all pages, merge results |
| **3. Increase tool result limit** | `McpToolProvider.cs#L836` | Increase `MaxToolResultChars` to 16000 for resource listing tools |
| **4. Strengthen prompt** | `Seeder.cs` DevOps prompt | Change "ALWAYS cross-reference" to "YOU MUST call platform_arg_query_resources AND azure_list_resources. Incomplete data is a failure." |
| **5. Add workflow validation** | `MultiRoundGroupChatManager` | Before terminating, check if DevOps called both MCP servers for inventory tasks |

### 7.4 Quick Win: Prompt Fix

In [Seeder.cs#L171-L180](backend/src/Api/Setup/Seeds/Seeder.cs#L171), change:

```csharp
// FROM:
"═══ RESOURCE INVENTORY PROTOCOL ═══
When asked to list resources or do an inventory:
1. Call Platform MCP's ARG query tool..."

// TO:
"═══ RESOURCE INVENTORY PROTOCOL — MANDATORY ═══
When asked to list resources or do an inventory, you MUST:
1. FIRST call platform_arg_query_resources with: resources | project name, type, location, resourceGroup, tags
2. THEN call azure_list_resources for cross-reference
3. MERGE both results — the combined list is your answer
4. If either call fails, report the failure explicitly
5. NEVER report results from only one MCP server as complete

FAILURE TO CALL BOTH SERVERS = INCOMPLETE ANSWER = TASK FAILURE"
```

---

## 8. Root Cause Analysis: Why Agents "Turn Into a Circus"

### 8.1 Identified Issues

| Issue | Severity | Evidence | Root Cause |
|-------|----------|----------|------------|
| **Manager doesn't delegate properly** | High | Fall-through to force-delegate DevOps (line 211) | Prompt allows Manager to answer directly without mentioning worker names |
| **Workers don't use all MCP servers** | High | Incomplete resource lists | Prompt encourages but doesn't enforce multi-server calls |
| **Non-tool turns accumulate** | Medium | `MaxConsecutiveNonToolTurns = 3` kills run | Workers discuss instead of calling tools |
| **Context budget exceeded** | Medium | History compaction kicks in | Long conversations lose early evidence |
| **No worker-to-worker handoff** | Medium | Manager must relay everything | Adds latency, loses context |
| **Delegation detection is text-based** | Low | Regex matching agent names | Brittle — Manager must say exact name |

### 8.2 The "Circus" Pattern

```
User: "Check Azure resources"
      │
      ▼
Manager: [TRIAGE]                    ← Good
         Service: all
         Severity: low
         Goal: inventory
         
         [PLAN]                      ← Good
         1. DevOps — list resources
         
         "DevOps, please check..."   ← Delegation detected ✓
      │
      ▼
DevOps: "Sure, I'll check Azure."   ← ❌ NO TOOL CALL!
        "Let me analyze..."          ← Text-only response
      │
      ▼
Manager: "DevOps, what did you find?" ← ❌ Another delegation
      │
      ▼  
DevOps: "I'm checking now..."        ← ❌ Still no tool call!
      │
      ▼
[MaxConsecutiveNonToolTurns = 3 reached]
      │
      ▼
TERMINATED — Run killed for lack of tool usage
```

### 8.3 Fixes for Coordination

#### Fix 1: Force Tool Calls in First Worker Response

In `MultiRoundGroupChatManager`, after a worker speaks, check:
```csharp
if (lastWorkerMessage.Contents.None(c => c is FunctionCallContent))
{
    // Inject a system message: "You MUST call your MCP tools. Try again."
    // Or: force-retry the worker turn
}
```

#### Fix 2: Require Evidence Markers

Modify `ShouldTerminateAsync` to require `[EVIDENCE]` in worker responses before allowing termination.

#### Fix 3: Add Structured Handoff Format

Instead of free-text delegation, require Manager to use:
```
**[DELEGATE]**
To: DevOps
Task: List all Azure resources
Expected Evidence: Full resource inventory from platform_arg_query_resources AND azure_list_resources
```

Then parse this structured block instead of looking for agent names in text.

#### Fix 4: Reduce Manager Temperature

[OrchestrationSettings.cs#L27](backend/src/Api/Settings/OrchestrationSettings.cs#L27):
```csharp
["manager"] = new() { Temperature = 0.1f },  // Already low
```
Consider reducing to `0.0f` for maximum determinism.

#### Fix 5: Add Worker "I'm Done" Signal

Require workers to say `[WORKER COMPLETE]` when they've executed all assigned tasks. Manager waits for this before synthesizing.

---

## 9. Appendix: Key Code Fragments

### 9.1 Delegation Parsing Logic

[MultiRoundGroupChatManager.cs#L166-L210](backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs#L166):
```csharp
private void ParseDelegation(IReadOnlyList<ChatMessage> history)
{
    var managerText = GetLastManagerMessage(history);
    
    // Sort workers by name length (longest first) to avoid substring issues
    var workers = _agents
        .Where(a => !ReferenceEquals(a, _manager))
        .OrderByDescending(a => a.Name.Length)
        .ToList();

    var remainingText = managerText!;
    foreach (var agent in workers)
    {
        if (agent.Name is not null && 
            remainingText.Contains(agent.Name, StringComparison.OrdinalIgnoreCase))
        {
            _delegationQueue.Enqueue(agent);
            // Remove matched name to prevent double-matching
            remainingText = Regex.Replace(
                remainingText,
                Regex.Escape(agent.Name),
                "",
                RegexOptions.IgnoreCase);
        }
    }

    // Fallback: if Manager didn't delegate in Round 1, force-delegate to DevOps
    if (_delegationQueue.Count == 0 && _round == 1)
    {
        var devOpsAgent = workers.FirstOrDefault(a =>
            a.Name?.Contains("DevOps", StringComparison.OrdinalIgnoreCase) == true);
        if (devOpsAgent is not null)
        {
            _delegationQueue.Enqueue(devOpsAgent);
            Log.Information("Force-delegating to DevOps (Manager failed to delegate)");
        }
    }
}
```

### 9.2 Tool Authorization Matrix

[ToolAuthorizationPolicy.cs#L113-L149](backend/src/Api/Orchestration/ToolAuthorizationPolicy.cs#L113):
```csharp
public static (bool Authorized, string? Reason) ValidateToolAccess(string agentRole, string toolName)
{
    var server = GetServerType(toolName);
    var isWrite = IsWriteTool(toolName);

    switch (role)
    {
        case "manager":
            if (server == McpServerType.GitOps)
                return (false, "Manager has no GitOps access");
            if (isWrite)
                return (false, "Manager cannot execute write operations");
            return (true, null);

        case "devops":
            if (server == McpServerType.GitOps)
                return (false, "DevOps has no GitOps access");
            if (server == McpServerType.Ado && isWrite)
                return (false, "DevOps has read-only ADO access");
            return (true, null);

        case "developer":
            if (server == McpServerType.Azure || server == McpServerType.Platform)
                return (false, "Developer has no Azure/Platform access");
            return (true, null);
    }
}
```

### 9.3 MCP Tool Discovery

[McpToolProvider.cs#L388-L425](backend/src/Api/Mcp/McpToolProvider.cs#L388):
```csharp
private async Task<List<McpToolDefinition>> DiscoverToolsAsync(
    string serverUrl, string token, CancellationToken ct)
{
    var client = _httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // 1. Initialize MCP session
    var initRequest = new JsonRpcRequest
    {
        Method = "initialize",
        Params = { protocolVersion = "2025-03-26", ... }
    };
    var initResponse = await SendJsonRpcAsync(client, serverUrl, initRequest, ct);
    var sessionId = initResponse.SessionId;

    // 2. Send initialized notification
    await SendJsonRpcAsync(client, serverUrl, 
        new JsonRpcRequest { Method = "notifications/initialized" }, ct, sessionId);

    // 3. List tools
    var listRequest = new JsonRpcRequest { Method = "tools/list" };
    var listResponse = await SendJsonRpcAsync(client, serverUrl, listRequest, ct, sessionId);

    // 4. Parse tool definitions
    var tools = new List<McpToolDefinition>();
    foreach (var tool in listResponse.Body["result"]["tools"].EnumerateArray())
    {
        tools.Add(new McpToolDefinition
        {
            Name = tool.GetProperty("name").GetString(),
            Description = tool.TryGetProperty("description", out var desc) ? desc.GetString() : "",
            InputSchema = tool.TryGetProperty("inputSchema", out var schema) ? schema : default
        });
    }
    return tools;
}
```

---

## 10. Summary of Findings

### What Works Well
1. ✅ Clean separation: Orchestration → MCP → Tools
2. ✅ Role-based tool authorization with code-level enforcement
3. ✅ Context budget management prevents overflow
4. ✅ Approval gates for dangerous operations
5. ✅ Structured evidence/artifact persistence

### What Needs Improvement
1. ❌ Delegation is text-based (fragile)
2. ❌ No enforcement of multi-server MCP calls
3. ❌ Workers can respond without tool calls
4. ❌ No pagination for large result sets
5. ❌ Prompts "encourage" but don't "enforce" behavior

### Priority Fixes
1. **HIGH:** Add pagination to MCP tool invocation
2. **HIGH:** Enforce tool calls in worker first response
3. **MEDIUM:** Strengthen DevOps prompt for resource inventory
4. **MEDIUM:** Add structured delegation format
5. **LOW:** Reduce Manager temperature to 0.0

---

*Report generated by Repo Forensics Agent*
