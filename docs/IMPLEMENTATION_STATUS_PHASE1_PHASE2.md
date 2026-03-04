# Implementation Status: Phase 1 & Phase 2

## Overview
This document tracks the implementation of the technical specification fixes for:
- **Defect A**: MCP tool invocation reliability (params vs parameters, missing arguments)
- **Defect B**: Context length exceeded (token budget management)

## Phase 1: MCP Tool Invocation Reliability ✅ COMPLETE

### Implemented Components

#### 1. McpArgumentNormalizer.cs (NEW)
**Location**: `backend/src/Api/Orchestration/McpArgumentNormalizer.cs`
**Size**: ~350 lines
**Purpose**: Schema-aware argument validation and auto-repair

**Key Features**:
- Detects `params` vs `parameters` mismatches
- Wraps flat objects into required root properties
- Parses MCP error messages for repair strategies
- Extracts missing field names via regex
- Supports three repair strategies:
  - `WrapInParametersRoot`: Wraps args in `{ "parameters": {...} }`
  - `InferCommandWrapper`: Wraps args in `{ "command": {...} }`
  - `ProvideMissingFields`: Adds missing fields extracted from error text

**Methods**:
```csharp
public static (Dictionary<string, object?>, string?) NormalizeAndValidate(
    string toolName,
    Dictionary<string, object?> arguments,
    JsonDocument? mcpToolSchema)

public static McpArgumentRepair? ParseErrorAndSuggestRepair(
    string toolName,
    string errorMessage,
    Dictionary<string, object?> originalArgs)

public static Dictionary<string, object?> ApplyRepair(
    McpArgumentRepair repair,
    Dictionary<string, object?> originalArgs)

private static List<string> ExtractMissingFieldsFromError(string errorMessage)
```

#### 2. McpToolProvider.cs (MODIFIED)
**Location**: `backend/src/Api/Mcp/McpToolProvider.cs`
**Modified Lines**: ~638-695 in `CreateAiTool` method

**Changes**:
1. **Pre-invocation normalization** (line ~638):
   - Calls `McpArgumentNormalizer.NormalizeAndValidate` before MCP call
   - Logs normalization warnings if arguments were modified
   - Uses normalized arguments for JSON-RPC invocation

2. **Auto-repair in retry loop** (lines ~660-695):
   - On MCP error, parses response with `ParseErrorAndSuggestRepair`
   - If repair strategy found, applies it with `ApplyRepair`
   - Retries immediately without backoff (format errors don't need backoff)
   - Logs repair attempt and strategy used
   - Falls back to original retry logic if no repair possible

**Integration Points**:
```csharp
// Before MCP invocation
var (normalizedArgs, warning) = McpArgumentNormalizer.NormalizeAndValidate(
    mcpToolFunc.Name, arguments, toolSchema);
if (!string.IsNullOrEmpty(warning))
{
    Log.Warning("MCP argument normalized for {Tool}: {Warning}", 
        mcpToolFunc.Name, warning);
}

// In retry loop after error
var repair = McpArgumentNormalizer.ParseErrorAndSuggestRepair(
    mcpToolFunc.Name, mcpErrorMessage, originalArgs);
if (repair != null)
{
    Log.Information("Attempting MCP auto-repair for {Tool} with strategy {Strategy}", 
        mcpToolFunc.Name, repair.Strategy);
    arguments = McpArgumentNormalizer.ApplyRepair(repair, originalArgs);
    // immediate retry with repaired arguments
}
```

### Expected Behavior
1. **Proactive normalization**: Arguments are normalized before every MCP call
2. **Reactive repair**: If MCP returns format error, system parses error, applies repair, retries
3. **Zero user intervention**: Format errors are transparent to the user
4. **Fallback**: If repair fails, original error is returned (no infinite loops)

### Status
- ✅ Code implemented and integrated
- ✅ Compiles without errors
- ⏳ Not yet tested with real MCP servers
- 📋 Needs validation with test scenarios from spec

---

## Phase 2: Context Budget Management ✅ COMPLETE

### Implemented Components

#### 1. ContextBudgetManager.cs (NEW)
**Location**: `backend/src/Api/Orchestration/ContextBudgetManager.cs`
**Size**: ~400 lines
**Purpose**: Token estimation and message compaction to prevent context_length_exceeded

**Key Features**:
- Token estimation using 4 chars ≈ 1 token heuristic
- Sliding window (keeps last 40 messages by default)
- Tool result truncation (max 2000 tokens per result)
- Structured summarization of old messages
- Emergency fallback if still over budget
- Separate tool schema token estimation
- Budget diagnostics reporting

**Configuration**:
```csharp
public class ContextBudgetManager
{
    private readonly int MaxTotalTokens = 128_000;      // Model limit
    private readonly int TargetBudgetTokens = 32_000;   // Conservative target
    private readonly int SlidingWindowSize = 40;        // Keep last N messages
    private readonly int MaxToolResultTokens = 2_000;   // Truncate per result
    private readonly int TokensPerMessage = 4;          // Overhead per message
    // ... additional constants
}
```

**Methods**:
```csharp
public List<ChatMessage> CompactMessages(List<ChatMessage> messages)
// Main entry point: applies sliding window + summarization

public List<ChatMessage> TruncateToolResults(List<ChatMessage> messages)
// Truncates large tool results to max 2000 tokens

private ChatMessage CreateSummaryMessage(List<ChatMessage> oldMessages)
// Creates structured summary with key info, delegation, results

private int EstimateTokenCount(List<ChatMessage> messages)
// Estimates total tokens using 4:1 char ratio

public int EstimateToolSchemaTokens(List<AITool> tools)
// Estimates token overhead from tool schemas

public string GetBudgetDiagnostics(List<ChatMessage> messages)
// Returns human-readable budget status
```

**Compaction Strategy**:
1. **Check if over budget**: Estimate total tokens (target: 32K, max: 128K)
2. **Apply sliding window**: Keep last 40 messages, summarize older ones
3. **Truncate tool results**: Limit each result to 2000 tokens
4. **Emergency fallback**: If still over budget, remove oldest messages one by one
5. **Preserve structure**: Always keep system message, never break tool call/result pairs

**Summarization Format**:
```json
{
  "type": "summary",
  "messageCount": 45,
  "timeRange": "2025-01-15 10:23:45 - 10:45:12",
  "userMessages": 12,
  "assistantMessages": 15,
  "toolCalls": 18,
  "keyTopics": ["Azure deployment", "database setup", "monitoring"],
  "delegationChain": ["Manager → DevOps", "DevOps → Developer"],
  "importantResults": ["Deployment successful", "Database configured"]
}
```

#### 2. AgUiEndpoints.cs (MODIFIED)
**Location**: `backend/src/Api/Endpoints/AgUiEndpoints.cs`
**Modified Lines**: ~148-158 (after message conversion, before agent creation)

**Changes**:
```csharp
// Convert messages from frontend format
var messages = input.Messages.AsChatMessages(jsonSerializerOptions).ToList();
var clientTools = input.Tools?.AsAITools().ToList();

// Apply context budget management BEFORE creating agents
// This prevents context_length_exceeded by compacting message history
var budgetManager = new ContextBudgetManager();
var originalMessageCount = messages.Count;
messages = budgetManager.CompactMessages(messages);
if (messages.Count < originalMessageCount)
{
    Log.Information("[ContextBudget] Compacted messages from {Original} to {Compacted} to fit budget",
        originalMessageCount, messages.Count);
}

// Continue with agent and workflow creation (uses compacted messages)
```

**Integration Points**:
- **Entry point**: After `AsChatMessages` converts frontend DTOs to ChatMessage objects
- **Before**: Agent creation, workflow assembly, LLM calls
- **Effect**: All downstream processing uses compacted message history
- **Logging**: Reports compaction statistics to Serilog

### Expected Behavior
1. **Automatic compaction**: Every channel message triggers budget check
2. **Transparent to agents**: Agents receive compacted history, don't know about truncation
3. **Preserves context**: Important info (delegations, results) preserved in summary
4. **Stay under budget**: System targets 32K tokens (25% of 128K limit) for safety margin
5. **No frontend changes**: Frontend still sends full history, backend manages budget

### Status
- ✅ Code implemented and integrated
- ✅ Compiles without errors
- ✅ Fixed FunctionResultContent constructor issue
- ✅ Application starts successfully with ./start.sh
- ⏳ Not yet tested with real MCP servers
- 📋 Needs validation with test scenarios from spec

---

## Testing Requirements

### Phase 1 Test Scenarios (MCP Reliability)

#### Scenario 1: params→parameters Normalization
**Setup**: Agent calls Azure monitor tool with args in `params` format  
**Expected**: System detects mismatch, wraps in `parameters`, succeeds  
**Validation**: Check logs for normalization warning, tool call succeeds

#### Scenario 2: Missing Required Options
**Setup**: Agent calls tool without required `--resource-group`, `--workspace` args  
**Expected**: MCP returns error, system parses error, adds missing fields, retries  
**Validation**: Check logs for auto-repair with `ProvideMissingFields` strategy

#### Scenario 3: Flat Object Wrapping
**Setup**: Agent provides flat args when MCP expects nested structure  
**Expected**: System wraps args in required root property, succeeds  
**Validation**: Tool call succeeds without user intervention

#### Scenario 4: Unparseable Error
**Setup**: MCP returns error that doesn't match known patterns  
**Expected**: System attempts normalization, fails gracefully, returns original error  
**Validation**: No infinite retry loop, error propagated to user

### Phase 2 Test Scenarios (Context Budget)

#### Scenario 5: Long Conversation Compaction
**Setup**: Create channel with 50+ messages including tool calls  
**Expected**: Message history compacted to ~40 messages, old messages summarized  
**Validation**: Check logs for compaction report, verify summary structure

#### Scenario 6: Large Tool Result Truncation
**Setup**: Tool returns 10KB JSON result  
**Expected**: Result truncated to ~2000 tokens with "..." indicator  
**Validation**: Verify result is truncated, conversation continues normally

#### Scenario 7: Multi-Round with Tool Schemas
**Setup**: 5-round Manager→Workers conversation with ~50 tools loaded  
**Expected**: System stays under 32K token budget throughout  
**Validation**: Monitor logs for token estimates, no context_length_exceeded errors

#### Scenario 8: Emergency Fallback
**Setup**: Create scenario that exceeds budget even after compaction  
**Expected**: System removes oldest messages one by one until under budget  
**Validation**: Conversation continues, verify oldest non-essential messages removed

---

## Remaining Work

### High Priority
1. **Test Phase 1**: Validate MCP auto-repair with real Azure/Platform/ADO tools
2. **Test Phase 2**: Validate compaction with long multi-agent conversations
3. **Monitor production**: Watch for normalization warnings and compaction events

### Medium Priority (From Spec)
4. **Tool filtering optimization**: Reduce tool count per agent beyond role-based filtering
5. **Reduce orchestration chatter**: Analyze MultiRoundGroupChatManager for redundancy
6. **Adjust sliding window**: Tune window size based on production data (currently 40)
7. **Tune truncation limits**: Adjust max tool result tokens based on usage (currently 2000)

### Low Priority
8. **Documentation**: Write user-facing guide on context management
9. **Metrics**: Add Prometheus/Application Insights metrics for compaction rate
10. **Configuration**: Move hardcoded constants to appsettings.json for runtime tuning

---

## Architecture Notes

### Token Estimation Strategy
- **4:1 character-to-token ratio**: Simple heuristic, no tokenizer dependency
- **Conservative target**: 32K tokens (25% of 128K limit) provides safety margin
- **Separate tool schema estimation**: ~100-500 tokens per tool based on complexity
- **Overhead accounting**: 4 tokens per message for role/metadata

### Why Sliding Window + Summarization?
- **Sliding window alone**: Loses important early context (goals, decisions)
- **Summarization alone**: Difficult to extract structured info from arbitrary history
- **Combined approach**: Keeps recent details + high-level summary of old context

### Why Not Truncate Earlier?
- **Frontend owns history**: Frontend maintains full conversation state for UI
- **Backend manages budget**: Backend compacts on-demand for LLM calls
- **Clean separation**: UI shows full history, agents work with compacted view

### Why 40 Message Window?
- **Typical conversation**: 10-20 user messages with 2-3 agent turns each = 30-60 total
- **Multi-round orchestration**: Manager + 2 workers = 3 messages per decision round
- **Balance**: Large enough for context, small enough to stay within budget

---

## Code Quality Checklist

- ✅ No compilation errors
- ✅ Follows existing code style (C# 10, Microsoft.Extensions.AI patterns)
- ✅ Proper logging with Serilog (Information, Warning levels)
- ✅ JSON serialization uses System.Text.Json and Newtonsoft.Json appropriately
- ✅ Error handling with try-catch in critical paths
- ✅ No breaking changes to existing APIs
- ✅ Minimal dependencies (no new NuGet packages required)
- ⏳ Unit tests (not yet created)
- ⏳ Integration tests (not yet created)

---

## Risk Assessment

### Low Risk
- **No breaking changes**: Existing code paths unmodified except for normalization/compaction
- **Fail-safe design**: If normalization fails, original args used; if compaction fails, full history used
- **Backward compatible**: Old conversations work without migration

### Medium Risk
- **Token estimation accuracy**: 4:1 ratio is heuristic, may not match exact tokenizer
- **Summarization quality**: Summary may lose important context in edge cases
- **Tool filtering**: Still loading all tools per role (~50-120 tools), may exceed budget with many tools

### Mitigation Strategies
- Monitor logs for normalization/compaction events
- Adjust window size and truncation limits based on production data
- Implement tool schema caching to reduce repeated overhead
- Add metrics to track compaction effectiveness

---

## Next Steps

1. **Test MCP auto-repair**: Run Agent system with Azure MCP tools, trigger format errors
2. **Test context compaction**: Create long conversation (50+ messages), verify compaction works
3. **Monitor production**: Deploy and watch for issues in real usage
4. **Iterate based on data**: Adjust sliding window size, truncation limits, summarization strategy

---

## Open Questions

1. Should `ContextBudgetManager` be a singleton or created per-request? (Currently per-request, lightweight)
2. Should sliding window size be configurable per channel? (Currently global constant)
3. Should summarization be LLM-powered or rule-based? (Currently rule-based for performance)
4. Should tool schemas be cached or re-estimated every turn? (Currently re-estimated, could optimize)

---

**Implementation Date**: 2025-01-15  
**Status**: Phase 1 & Phase 2 code complete, integration complete, testing pending  
**Next Milestone**: Validation with real MCP servers and long conversations
