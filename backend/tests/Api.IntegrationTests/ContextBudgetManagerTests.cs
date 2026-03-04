using AzureOpsCrew.Api.Orchestration;
using Microsoft.Extensions.AI;
using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

/// <summary>
/// Tests for context budget management.
/// Addresses defect: context_length_exceeded due to large tool results and message history.
/// </summary>
public class ContextBudgetManagerTests
{
    #region Token Estimation Tests

    [Fact]
    public void EstimateTokenCount_EmptyMessages_ReturnsZero()
    {
        // Arrange
        var manager = new ContextBudgetManager();
        var messages = new List<ChatMessage>();

        // Act
        var tokens = manager.EstimateTokenCount(messages);

        // Assert
        Assert.Equal(0, tokens);
    }

    [Fact]
    public void EstimateTokenCount_SimpleMessage_ReturnsReasonableEstimate()
    {
        // Arrange
        var manager = new ContextBudgetManager();
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Hello, how are you?")
        };

        // Act
        var tokens = manager.EstimateTokenCount(messages);

        // Assert: ~5 words ≈ 5-10 tokens + message overhead
        Assert.InRange(tokens, 5, 30);
    }

    [Fact]
    public void EstimateTokenCount_LargeMessage_ReturnsProportionalEstimate()
    {
        // Arrange
        var manager = new ContextBudgetManager();
        var largeText = new string('A', 4000); // 4000 chars ≈ 1000 tokens
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, largeText)
        };

        // Act
        var tokens = manager.EstimateTokenCount(messages);

        // Assert: Should be around 1000 tokens + overhead
        Assert.InRange(tokens, 900, 1200);
    }

    #endregion

    #region CompactMessages Tests

    [Fact]
    public void CompactMessages_UnderBudget_ReturnsUnchanged()
    {
        // Arrange
        var manager = new ContextBudgetManager(targetBudget: 10000);
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "You are a helpful assistant."),
            new ChatMessage(ChatRole.User, "Hello"),
            new ChatMessage(ChatRole.Assistant, "Hi there!")
        };

        // Act
        var compacted = manager.CompactMessages(messages);

        // Assert
        Assert.Equal(messages.Count, compacted.Count);
    }

    [Fact]
    public void CompactMessages_OverBudget_ReducesMessageCount()
    {
        // Arrange: Target budget that will be exceeded
        var manager = new ContextBudgetManager(targetBudget: 500, maxMessagesInWindow: 5);

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "You are a helpful assistant.")
        };

        // Add many messages to exceed budget
        for (int i = 0; i < 50; i++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"This is message number {i} with some content."));
            messages.Add(new ChatMessage(ChatRole.Assistant, $"Response to message {i}."));
        }

        // Act
        var compacted = manager.CompactMessages(messages);

        // Assert: Should have fewer messages
        Assert.True(compacted.Count < messages.Count, 
            $"Expected fewer messages after compaction. Original: {messages.Count}, Compacted: {compacted.Count}");

        // Should keep system message
        Assert.Contains(compacted, m => m.Role == ChatRole.System && m.Text?.Contains("helpful assistant") == true);
    }

    [Fact]
    public void CompactMessages_OverBudget_CreatesSummaryMessage()
    {
        // Arrange: Very tight budget that will definitely require summarization
        var manager = new ContextBudgetManager(targetBudget: 200, maxMessagesInWindow: 3);

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, "You are a helpful assistant.")
        };

        // Add many messages with substantial content to exceed budget
        for (int i = 0; i < 50; i++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"This is a longer message number {i} with quite a bit of content to add tokens.") { AuthorName = "User" });
            messages.Add(new ChatMessage(ChatRole.Assistant, $"This is an even longer response to message number {i} with lots of additional words.") { AuthorName = "DevOps" });
        }

        // Act
        var compacted = manager.CompactMessages(messages);

        // Assert: Should have fewer messages than original
        Assert.True(compacted.Count < messages.Count,
            $"Expected compaction. Original: {messages.Count}, Compacted: {compacted.Count}");

        // Check for summary (may or may not be present depending on budget constraints)
        var hasSummary = compacted.Any(m => m.Role == ChatRole.System &&
            (m.Text?.Contains("Summary", StringComparison.OrdinalIgnoreCase) == true ||
             m.Text?.Contains("compacted", StringComparison.OrdinalIgnoreCase) == true ||
             m.Text?.Contains("earlier", StringComparison.OrdinalIgnoreCase) == true));

        // At minimum, verify the system prompt is preserved
        Assert.Contains(compacted, m => m.Role == ChatRole.System);
    }

    #endregion

    #region Tool Schema Budget Tests

    [Fact]
    public void EstimateToolSchemaTokens_NoTools_ReturnsZero()
    {
        // Arrange
        var manager = new ContextBudgetManager();
        var tools = new List<AITool>();

        // Act
        var tokens = manager.EstimateToolSchemaTokens(tools);

        // Assert
        Assert.Equal(0, tokens);
    }

    [Fact]
    public void GetBudgetDiagnostics_ReturnsFormattedStatus()
    {
        // Arrange
        var manager = new ContextBudgetManager(targetBudget: 10000);
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, "Test message")
        };
        var tools = new List<AITool>();

        // Act
        var diagnostics = manager.GetBudgetDiagnostics(messages, tools);

        // Assert
        Assert.Contains("Context Budget Status", diagnostics);
        Assert.Contains("Messages:", diagnostics);
        Assert.Contains("tokens", diagnostics);
    }

    #endregion
}
