using Microsoft.Extensions.AI;
using Serilog;
using System.Text;
using System.Text.Json;

namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// Token budget management for multi-agent orchestration.
/// Prevents context_length_exceeded by:
/// - Estimating token count before LLM call
/// - Applying sliding window + summarization to message history
/// - Truncating large tool results
/// - Prioritizing recent and relevant messages
/// </summary>
public class ContextBudgetManager
{
    // OpenAI GPT-4 / GPT-4 Turbo context limits
    private const int DefaultMaxContextTokens = 128000;
    private const int DefaultTargetBudget = 32000;  // Conservative target to leave room for response
    private const int DefaultMaxToolResultTokens = 2000;
    private const int DefaultMaxMessagesInWindow = 40;

    private readonly int _maxContextTokens;
    private readonly int _targetBudget;
    private readonly int _maxToolResultTokens;
    private readonly int _maxMessagesInWindow;

    public ContextBudgetManager(
        int? maxContextTokens = null,
        int? targetBudget = null,
        int? maxToolResultTokens = null,
        int? maxMessagesInWindow = null)
    {
        _maxContextTokens = maxContextTokens ?? DefaultMaxContextTokens;
        _targetBudget = targetBudget ?? DefaultTargetBudget;
        _maxToolResultTokens = maxToolResultTokens ?? DefaultMaxToolResultTokens;
        _maxMessagesInWindow = maxMessagesInWindow ?? DefaultMaxMessagesInWindow;
    }

    /// <summary>
    /// Compacts message history to fit within target budget.
    /// Strategy:
    /// 1. Always keep system messages
    /// 2. Summarize oldest messages (beyond sliding window)
    /// 3. Keep recent messages (within sliding window) with truncated tool results
    /// 4. If still over budget, remove oldest turns until under budget
    /// </summary>
    public List<ChatMessage> CompactMessages(IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count == 0)
            return new List<ChatMessage>();

        var estimatedTotal = EstimateTokenCount(messages);
        Log.Debug("[ContextBudget] Original message count: {Count}, estimated tokens: {Tokens}",
            messages.Count, estimatedTotal);

        if (estimatedTotal <= _targetBudget)
        {
            Log.Debug("[ContextBudget] Messages within budget, no compaction needed");
            return messages.ToList();
        }

        // Step 1: Separate system messages from conversation
        var systemMessages = messages.Where(m => m.Role == ChatRole.System).ToList();
        var conversationMessages = messages.Where(m => m.Role != ChatRole.System).ToList();

        // Step 2: Apply sliding window — keep only recent messages
        var recentMessages = conversationMessages.Count > _maxMessagesInWindow
            ? conversationMessages.Skip(conversationMessages.Count - _maxMessagesInWindow).ToList()
            : conversationMessages.ToList();

        // Step 3: Truncate tool results in recent messages
        var compactedRecent = recentMessages.Select(TruncateToolResults).ToList();

        // Step 4: Summarize old messages (before sliding window)
        var oldMessages = conversationMessages.Count > _maxMessagesInWindow
            ? conversationMessages.Take(conversationMessages.Count - _maxMessagesInWindow).ToList()
            : new List<ChatMessage>();

        var summaryMessage = oldMessages.Count > 0
            ? CreateSummaryMessage(oldMessages)
            : null;

        // Step 5: Assemble compacted history
        var compacted = new List<ChatMessage>();
        compacted.AddRange(systemMessages);
        if (summaryMessage is not null)
            compacted.Add(summaryMessage);
        compacted.AddRange(compactedRecent);

        var compactedTokens = EstimateTokenCount(compacted);
        Log.Information("[ContextBudget] Compaction: {OrigCount} → {CompactCount} messages, {OrigTokens} → {CompactTokens} tokens (target: {Target})",
            messages.Count, compacted.Count, estimatedTotal, compactedTokens, _targetBudget);

        // Step 6: Emergency fallback — if still over budget, remove oldest conversation turns
        while (compacted.Count > systemMessages.Count + 5 && EstimateTokenCount(compacted) > _targetBudget)
        {
            // Remove oldest non-system message
            var firstNonSystem = compacted.FindIndex(m => m.Role != ChatRole.System && m != summaryMessage);
            if (firstNonSystem >= 0)
            {
                Log.Warning("[ContextBudget] Emergency: removing message at index {Index} to fit budget", firstNonSystem);
                compacted.RemoveAt(firstNonSystem);
            }
            else
            {
                break;
            }
        }

        var finalTokens = EstimateTokenCount(compacted);
        Log.Information("[ContextBudget] Final message count: {Count}, estimated tokens: {Tokens}",
            compacted.Count, finalTokens);

        return compacted;
    }

    /// <summary>
    /// Truncates tool results (FunctionResultContent) in a message to stay within token budget.
    /// </summary>
    private ChatMessage TruncateToolResults(ChatMessage message)
    {
        if (message.Contents == null || message.Contents.Count == 0)
            return message;

        bool hasTruncated = false;
        var newContents = new List<AIContent>();

        foreach (var content in message.Contents)
        {
            if (content is FunctionResultContent funcResult)
            {
                var resultStr = funcResult.Result?.ToString() ?? "";
                var estimatedTokens = EstimateTokenCount(resultStr);

                if (estimatedTokens > _maxToolResultTokens)
                {
                    var truncatedResult = TruncateString(resultStr, _maxToolResultTokens);
                    Log.Debug("[ContextBudget] Truncating tool result from {OrigTokens} to {MaxTokens} tokens",
                        estimatedTokens, _maxToolResultTokens);

                    newContents.Add(new FunctionResultContent(
                        funcResult.CallId,
                        truncatedResult + $"\n\n[... truncated from {resultStr.Length} to {truncatedResult.Length} chars to save context]"));

                    hasTruncated = true;
                }
                else
                {
                    newContents.Add(content);
                }
            }
            else if (content is TextContent textContent)
            {
                var textStr = textContent.Text ?? "";
                var estimatedTokens = EstimateTokenCount(textStr);

                // Only truncate extremely long text messages (not tool results)
                if (estimatedTokens > _maxToolResultTokens * 2)
                {
                    var truncatedText = TruncateString(textStr, _maxToolResultTokens * 2);
                    Log.Debug("[ContextBudget] Truncating text content from {OrigTokens} to {MaxTokens} tokens",
                        estimatedTokens, _maxToolResultTokens * 2);

                    newContents.Add(new TextContent(
                        truncatedText + $"\n\n[... truncated to save context]"));

                    hasTruncated = true;
                }
                else
                {
                    newContents.Add(content);
                }
            }
            else
            {
                newContents.Add(content);
            }
        }

        return hasTruncated
            ? new ChatMessage(message.Role, newContents)
            {
                MessageId = message.MessageId,
                AuthorName = message.AuthorName,
                RawRepresentation = message.RawRepresentation,
                AdditionalProperties = message.AdditionalProperties
            }
            : message;
    }

    /// <summary>
    /// Creates a summary message from old messages that are being removed from context.
    /// Preserves key information: tools used, findings, and agent participation.
    /// </summary>
    private ChatMessage CreateSummaryMessage(List<ChatMessage> oldMessages)
    {
        var summary = new StringBuilder();
        summary.AppendLine("📋 **Summary of earlier conversation (compacted to manage context budget):**");
        summary.AppendLine();

        // Count tool calls and extract tool names
        var toolCallsByName = new Dictionary<string, int>();
        var agentTurns = new Dictionary<string, int>();
        var keyFindings = new List<string>();

        foreach (var msg in oldMessages)
        {
            if (msg.Contents != null)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is FunctionCallContent funcCall && !string.IsNullOrEmpty(funcCall.Name))
                    {
                        toolCallsByName.TryGetValue(funcCall.Name, out var count);
                        toolCallsByName[funcCall.Name] = count + 1;
                    }
                    else if (content is FunctionResultContent funcResult && funcResult.Result is not null)
                    {
                        // Extract key findings from tool results (first 100 chars)
                        var resultStr = funcResult.Result.ToString() ?? "";
                        if (resultStr.Length > 50 && !resultStr.StartsWith("⚠️") && !resultStr.StartsWith("🚫"))
                        {
                            var snippet = resultStr.Length > 150 ? resultStr[..150] + "..." : resultStr;
                            // Only keep first few findings
                            if (keyFindings.Count < 5)
                            {
                                keyFindings.Add(snippet.Split('\n')[0]); // Just first line
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(msg.AuthorName))
            {
                agentTurns.TryGetValue(msg.AuthorName, out var count);
                agentTurns[msg.AuthorName] = count + 1;
            }
        }

        // Write structured summary
        summary.AppendLine($"**Messages covered:** {oldMessages.Count}");

        if (agentTurns.Count > 0)
        {
            summary.AppendLine("\n**Agent participation:**");
            foreach (var kvp in agentTurns.OrderByDescending(x => x.Value))
            {
                summary.AppendLine($"  - {kvp.Key}: {kvp.Value} turns");
            }
        }

        if (toolCallsByName.Count > 0)
        {
            summary.AppendLine($"\n**Tools used:** {toolCallsByName.Values.Sum()} calls total");
            foreach (var kvp in toolCallsByName.OrderByDescending(x => x.Value).Take(10))
            {
                summary.AppendLine($"  - {kvp.Key}: {kvp.Value}x");
            }
        }

        if (keyFindings.Count > 0)
        {
            summary.AppendLine("\n**Key data points from earlier:**");
            foreach (var finding in keyFindings.Take(5))
            {
                summary.AppendLine($"  • {finding}");
            }
        }

        summary.AppendLine();
        summary.AppendLine("*Note: Detailed earlier context was compacted. Agents should rely on recent messages and make fresh tool calls if needed.*");

        return new ChatMessage(ChatRole.System, summary.ToString());
    }

    /// <summary>
    /// Estimates token count for a message list.
    /// Uses rough heuristic: 1 token ≈ 4 characters for English text.
    /// This is conservative and will overestimate for non-English or code.
    /// </summary>
    public int EstimateTokenCount(IReadOnlyList<ChatMessage> messages)
    {
        int total = 0;
        foreach (var msg in messages)
        {
            total += EstimateTokenCount(msg);
        }
        return total;
    }

    /// <summary>
    /// Estimates token count for a single message.
    /// </summary>
    private int EstimateTokenCount(ChatMessage message)
    {
        int tokens = 0;

        // Role + metadata overhead (~10 tokens)
        tokens += 10;

        // Author name
        if (!string.IsNullOrEmpty(message.AuthorName))
            tokens += EstimateTokenCount(message.AuthorName);

        // Contents
        if (message.Contents != null)
        {
            foreach (var content in message.Contents)
            {
                tokens += content switch
                {
                    TextContent text => EstimateTokenCount(text.Text ?? ""),
                    FunctionCallContent funcCall => EstimateTokenCount(funcCall.Name) + 
                                                     EstimateTokenCount(JsonSerializer.Serialize(funcCall.Arguments ?? new Dictionary<string, object?>())),
                    FunctionResultContent funcResult => EstimateTokenCount(funcResult.Result?.ToString() ?? ""),
                    DataContent data => data.Data.Length / 4,  // Rough estimate for binary/JSON data
                    _ => 50  // Unknown content type overhead
                };
            }
        }

        return tokens;
    }

    /// <summary>
    /// Estimates token count for a string.
    /// Heuristic: 1 token ≈ 4 characters (conservative for English/code).
    /// </summary>
    private int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Tokenization heuristic: ~4 chars per token for English
        // More accurate would be to use tiktoken library, but this is sufficient for budget management
        return (text.Length / 4) + 1;
    }

    /// <summary>
    /// Truncates a string to approximately maxTokens tokens.
    /// </summary>
    private string TruncateString(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var maxChars = maxTokens * 4;  // Inverse of token estimation heuristic
        if (text.Length <= maxChars)
            return text;

        return text[..maxChars];
    }

    /// <summary>
    /// Estimates token count for tool schemas.
    /// Tool schemas can be significant — ~100-500 tokens per tool.
    /// </summary>
    public int EstimateToolSchemaTokens(IReadOnlyList<AITool> tools)
    {
        if (tools == null || tools.Count == 0)
            return 0;

        int total = 0;
        foreach (var tool in tools)
        {
            // Name + description
            total += EstimateTokenCount(tool.Name ?? "");
            total += EstimateTokenCount(tool.Description ?? "");

            // Schema (JSON)
            if (tool is AIFunction func)
            {
                var schemaJson = func.JsonSchema.ToString();
                total += EstimateTokenCount(schemaJson);
            }
            else
            {
                // Generic tool overhead
                total += 100;
            }
        }

        return total;
    }

    /// <summary>
    /// Returns diagnostic info about current context usage.
    /// </summary>
    public string GetBudgetDiagnostics(IReadOnlyList<ChatMessage> messages, IReadOnlyList<AITool> tools)
    {
        var msgTokens = EstimateTokenCount(messages);
        var toolTokens = EstimateToolSchemaTokens(tools);
        var total = msgTokens + toolTokens;

        var percentUsed = (total * 100.0) / _maxContextTokens;

        return $"""
            Context Budget Status:
              Messages: {messages.Count} ({msgTokens:N0} tokens)
              Tools: {tools.Count} ({toolTokens:N0} tokens)
              Total: {total:N0} / {_maxContextTokens:N0} tokens ({percentUsed:F1}%)
              Target budget: {_targetBudget:N0} tokens
              Status: {(total > _targetBudget ? "⚠️ OVER BUDGET" : "✅ Within budget")}
            """;
    }
}
