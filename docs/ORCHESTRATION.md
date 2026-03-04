# Multi-Agent Orchestration Implementation

This document describes the implementation of the robust multi-agent orchestration system for AzureOpsCrew, following the technical specification in [Stabilize_Modernize_Multi_Agent_Orchestration.md](../tasks/Stabilize_Modernize_Multi_Agent_Orchestration.md).

## Overview

The orchestration system has been modernized from a fragile text-based delegation model to a robust, tool-enforced, structured delegation architecture inspired by GitHub Copilot's patterns.

### Key Components

```
┌─────────────────────────────────────────────────────────────────────┐
│                           API Endpoints                              │
│              AgUiEndpoints.cs (with Direct Addressing)              │
└────────────────────────────┬────────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                    MultiRoundGroupChatManager                        │
│            (Structured delegation + Tool enforcement)               │
└────────────────────────────┬────────────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────────────┐
│                         RunContext                                   │
│         (Task queue, metrics, direct addressing state)              │
└────────────────────────────┬────────────────────────────────────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
┌────────▼────────┐ ┌───────▼────────┐ ┌───────▼────────┐
│ OrchestratorTool│ │  McpToolProvider│ │  ArtifactSystem │
│    Provider     │ │  (Azure, Platform│ │  (Large results) │
│ (delegate, inv) │ │   ADO, GitOps)   │ │                  │
└─────────────────┘ └─────────────────┘ └──────────────────┘
```

## Feature Flags (OrchestrationSettings)

All new features are controlled by feature flags for safe rollout:

| Flag | Default | Description |
|------|---------|-------------|
| `EnableStructuredDelegation` | `true` | Manager delegates via `orchestrator_delegate_tasks` tool |
| `EnableDirectAddressing` | `true` | Users can use `@DevOps` / `@Developer` syntax |
| `EnableCompositeInventoryTool` | `true` | `inventory_list_all_resources` queries both MCP servers |
| `EnableArtifactFirst` | `true` | Large tool results saved as artifacts |
| `EnableToolEnforcement` | `true` | Workers must use tools when `requires_tools=true` |
| `MaxMissingToolRetries` | `2` | Retry count before failing on missing tools |
| `ToolInlineThresholdChars` | `6000` | Results > threshold stored as artifacts |
| `MaxInventoryPages` | `50` | Pagination limit for inventory queries |

## 1. Structured Delegation

### Problem Solved
The old system used text parsing (`Contains("DevOps")`) to detect delegation, which was fragile and unreliable.

### Solution
Manager now delegates using the `orchestrator_delegate_tasks` tool with a structured payload:

```json
{
  "tasks": [
    {
      "assignee": "DevOps",
      "intent": "inventory",
      "goal": "List ALL Azure resources across subscription",
      "requires_tools": true,
      "required_tools": ["arg_query_resources", "azure_list_resources"],
      "definition_of_done": "Merged inventory with counts; no missing pages"
    }
  ]
}
```

### Task Intents
- `inventory` - List/enumerate resources
- `diagnostic` - Investigate issues
- `remediation` - Fix infrastructure
- `verification` - Verify changes
- `code_analysis` - Analyze code (Developer)
- `code_fix` - Make code changes (Developer)
- `generic` - Fallback for text-based delegation

### Files Changed
- [DelegationModels.cs](../backend/src/Api/Orchestration/DelegationModels.cs) - Task contracts
- [OrchestratorToolProvider.cs](../backend/src/Api/Orchestration/OrchestratorToolProvider.cs) - Tool implementations
- [MultiRoundGroupChatManager.cs](../backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs) - Delegation parsing

## 2. Tool Enforcement with Retry

### Problem Solved
Workers could respond with text-only answers even when tools were required, wasting context and producing unreliable results.

### Solution
When `requires_tools = true` on a delegated task:
1. Worker response is checked for tool calls
2. If no tools were called, response is **rejected**
3. Worker gets up to `MaxMissingToolRetries` (default: 2) retries
4. After max retries, task fails and run may terminate

### Flow
```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Manager   │───▶│   Worker    │───▶│   Check     │
│  delegates  │    │  responds   │    │  tools?     │
└─────────────┘    └─────────────┘    └──────┬──────┘
                                             │
                        ┌────────────────────┼────────────────────┐
                        │ tools used         │ no tools           │
                        ▼                    ▼                    │
               ┌─────────────┐      ┌─────────────┐              │
               │   Accept    │      │   Retry <   │──yes──┐      │
               │  response   │      │   max?      │       │      │
               └─────────────┘      └──────┬──────┘       │      │
                                           │ no           │      │
                                           ▼              │      │
                                  ┌─────────────┐         │      │
                                  │    Fail     │◀────────┘      │
                                  │   task      │                │
                                  └─────────────┘                │
                                            ▲                    │
                                            └────────────────────┘
```

### Files Changed
- [MultiRoundGroupChatManager.cs](../backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs) - `CheckToolEnforcement()`
- [RunContext.cs](../backend/src/Api/Orchestration/RunContext.cs) - `RecordMissingToolRetry()`

## 3. Composite Inventory Tool

### Problem Solved
Single MCP tool calls returned incomplete resource lists. DevOps had to manually call tools from both Azure MCP and Platform MCP.

### Solution
New `inventory_list_all_resources` tool:
1. Calls Platform MCP ARG query (most comprehensive)
2. Calls Azure MCP list resources (additional details)
3. Merges results, deduplicating by resource ID
4. Handles pagination up to `MaxInventoryPages`
5. Stores large results (> `ToolInlineThresholdChars`) as artifacts

### Usage
Manager can call directly or delegate to DevOps:
```json
// Manager calling directly
inventory_list_all_resources({
  "subscription_id": "optional-filter",
  "resource_group": "optional-filter",
  "resource_type_filter": "optional-filter"
})

// Or delegate to DevOps with inventory intent
orchestrator_delegate_tasks({
  "tasks": [{
    "assignee": "DevOps",
    "intent": "inventory",
    "goal": "List all resources in subscription",
    "requires_tools": true
  }]
})
```

### Files Changed
- [OrchestratorToolProvider.cs](../backend/src/Api/Orchestration/OrchestratorToolProvider.cs) - `inventory_list_all_resources` tool

## 4. Artifact-First Pattern

### Problem Solved
Large tool outputs were truncated, losing critical data like full resource inventories.

### Solution
When tool results exceed `ToolInlineThresholdChars`:
1. Full result saved as artifact in database
2. Response includes `artifact_id` for later retrieval
3. `artifact_fetch` tool enables paginated retrieval

### Files Changed
- [OrchestratorToolProvider.cs](../backend/src/Api/Orchestration/OrchestratorToolProvider.cs) - Artifact creation
- [DelegationModels.cs](../backend/src/Api/Orchestration/DelegationModels.cs) - `ArtifactFetchRequest`/`Result`

## 5. Direct Addressing

### Problem Solved
Users had to go through Manager even for simple queries to a specific agent.

### Solution
Users can now address agents directly using `@AgentName` syntax:
- `@DevOps check container health` - Routes directly to DevOps
- `@Developer analyze error in api.ts` - Routes directly to Developer
- `@Manager investigate production issue` - Routes to Manager (normal flow)

### Flow
```
User: "@DevOps check container health"
         │
         ▼
┌─────────────────────────────┐
│ DirectAddressingHelper.Parse │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ RunContext.SetDirectAddress │
└──────────────┬──────────────┘
               │
               ▼
┌─────────────────────────────┐
│ MultiRoundGroupChatManager  │
│ (skips Manager, routes to   │
│  DevOps directly)           │
└─────────────────────────────┘
```

### Files Changed
- [AgUiEndpoints.cs](../backend/src/Api/Endpoints/AgUiEndpoints.cs) - `DirectAddressingHelper`
- [RunContext.cs](../backend/src/Api/Orchestration/RunContext.cs) - `SetDirectAddress()`
- [MultiRoundGroupChatManager.cs](../backend/src/Api/Orchestration/MultiRoundGroupChatManager.cs) - Direct addressing mode

## 6. Updated Agent Prompts

### Changes
Agent prompts in [Seeder.cs](../backend/src/Api/Setup/Seeds/Seeder.cs) have been updated:

**Manager:**
- Must use `orchestrator_delegate_tasks` for delegation
- Can use `inventory_list_all_resources` for comprehensive inventory
- Text-based delegation is deprecated

**DevOps:**
- Must use tools when required (enforced with retries)
- Clear guidance on using all 3 MCP servers
- Evidence-first protocol emphasized

**Developer:**
- Must use tools when required (enforced with retries)
- Evidence-first protocol emphasized

## Metrics

New metrics tracked in `RunContext`:

| Metric | Description |
|--------|-------------|
| `MissingToolRetryCount` | Total retries due to missing tool calls |
| `InventorySourceCount` | Number of inventory calls made |
| `ArtifactsSaved` | Count of artifacts created for large results |
| `TruncationCount` | Times results were truncated |

Metrics are included in run summary logs:
```
[Run xyz] Status=Resolved, Duration=45s, Turns=7, ToolCalls=12, 
          InventorySources=2, ArtifactsSaved=1, MissingToolRetries=0
```

## Testing

Integration tests in [OrchestrationTests.cs](../backend/tests/Api.IntegrationTests/OrchestrationTests.cs):

- DelegationModels serialization
- RunContext delegation queue operations
- Tool enforcement retry counting
- Direct addressing state management
- Feature flag defaults
- Metrics tracking

## Configuration

In `appsettings.json`:
```json
{
  "OrchestrationSettings": {
    "EnableStructuredDelegation": true,
    "EnableDirectAddressing": true,
    "EnableCompositeInventoryTool": true,
    "EnableArtifactFirst": true,
    "EnableToolEnforcement": true,
    "MaxMissingToolRetries": 2,
    "ToolInlineThresholdChars": 6000,
    "MaxInventoryPages": 50,
    "MaxRoundsPerRun": 4,
    "IdleTimeoutSeconds": 30,
    "TotalTimeoutMinutes": 10,
    "MaxConsecutiveNonToolTurns": 4
  }
}
```

## Migration Notes

### Backward Compatibility
- Text-based delegation still works as fallback when `EnableStructuredDelegation = false`
- Existing agent prompts continue to work (but are less reliable)
- Feature flags allow gradual rollout

### Breaking Changes
- Manager now gets `orchestrator_delegate_tasks` tool injected
- Workers may have responses rejected if they don't call tools
- Large inventory results return artifact IDs instead of full data

## Future Work

1. **Subtask Protocol** - Agents creating subtasks for other agents
2. **Memory Integration** - Long-term context with RAG
3. **Auto-Recovery** - Automatic retry strategies for failed runs
4. **Metrics Dashboard** - Visualization of orchestration health
