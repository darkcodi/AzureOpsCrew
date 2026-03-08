using AzureOpsCrew.Infrastructure.Ai.ContextReduction;
using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.Tests.ContextReduction;

public class ChatMessageClassifierTests
{
    [Fact]
    public void IsToolRelatedMessage_FunctionCallOnly_ReturnsTrue()
    {
        var message = new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_1", "deploy", null)]);

        Assert.True(ChatMessageClassifier.IsToolRelatedMessage(message));
    }

    [Fact]
    public void IsToolRelatedMessage_FunctionResultToolRole_ReturnsTrue()
    {
        var message = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_1", "{\"status\":\"ok\"}")]);

        Assert.True(ChatMessageClassifier.IsToolRelatedMessage(message));
    }

    [Fact]
    public void IsToolRelatedMessage_TextContentUserMessage_ReturnsFalse()
    {
        var message = new ChatMessage(ChatRole.User, "Hello");

        Assert.False(ChatMessageClassifier.IsToolRelatedMessage(message));
    }

    [Fact]
    public void IsToolRelatedMessage_MixedTextAndFunctionCall_ReturnsFalse()
    {
        // Mixed messages (text + tool call) are classified as conversation (v1 limitation)
        var message = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("Let me check that."),
            new FunctionCallContent("call_1", "deploy", null)
        ]);

        Assert.False(ChatMessageClassifier.IsToolRelatedMessage(message));
    }

    [Fact]
    public void IsToolRelatedMessage_ReasoningOnly_ReturnsTrue()
    {
        var message = new ChatMessage(ChatRole.Assistant,
            [new TextReasoningContent("thinking about this...")]);

        Assert.True(ChatMessageClassifier.IsToolRelatedMessage(message));
    }

    [Fact]
    public void IsToolRelatedMessage_SystemMessage_ReturnsFalse()
    {
        var message = new ChatMessage(ChatRole.System, "You are helpful.");

        Assert.False(ChatMessageClassifier.IsToolRelatedMessage(message));
    }

    [Fact]
    public void IsToolRelatedMessage_AssistantTextOnly_ReturnsFalse()
    {
        var message = new ChatMessage(ChatRole.Assistant, "Here is the answer.");

        Assert.False(ChatMessageClassifier.IsToolRelatedMessage(message));
    }

    [Fact]
    public void ClassifyMessages_SplitsMixedListCorrectly()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Deploy it"),
            new(ChatRole.Assistant, [new FunctionCallContent("call_1", "deploy", null)]),
            new(ChatRole.Tool, [new FunctionResultContent("call_1", "{\"ok\":true}")]),
            new(ChatRole.Assistant, "Done!"),
        };

        var (conversation, toolRelated) = ChatMessageClassifier.ClassifyMessages(messages);

        Assert.Equal(2, conversation.Count); // User + Assistant text
        Assert.Equal(2, toolRelated.Count);  // FunctionCall + FunctionResult
    }

    [Fact]
    public void ClassifyMessages_EmptyList_ReturnsBothEmpty()
    {
        var (conversation, toolRelated) = ChatMessageClassifier.ClassifyMessages([]);

        Assert.Empty(conversation);
        Assert.Empty(toolRelated);
    }

    // --- New tests for ClassifyAndGroupMessages ---

    [Fact]
    public void ClassifyAndGroupMessages_PairsAssistantToolCallWithResults()
    {
        var assistantMsg = new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_1", "deploy", null)]);
        var toolResult = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_1", "{\"ok\":true}")]);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Deploy it"),
            assistantMsg,
            toolResult,
            new(ChatRole.Assistant, "Done!"),
        };

        var result = ChatMessageClassifier.ClassifyAndGroupMessages(messages);

        Assert.Equal(2, result.Conversation.Count); // User + "Done!"
        Assert.Single(result.ToolCallGroups);
        Assert.Same(assistantMsg, result.ToolCallGroups[0].AssistantMessage);
        Assert.Single(result.ToolCallGroups[0].ToolResultMessages);
        Assert.Same(toolResult, result.ToolCallGroups[0].ToolResultMessages[0]);
    }

    [Fact]
    public void ClassifyAndGroupMessages_MultipleToolCallsInOneMessage_AllResultsGrouped()
    {
        var assistantMsg = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call_a", "tool_a", null),
            new FunctionCallContent("call_b", "tool_b", null),
        ]);
        var resultA = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_a", "{\"a\":1}")]);
        var resultB = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_b", "{\"b\":2}")]);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Do two things"),
            assistantMsg,
            resultA,
            resultB,
        };

        var result = ChatMessageClassifier.ClassifyAndGroupMessages(messages);

        Assert.Single(result.ToolCallGroups);
        var group = result.ToolCallGroups[0];
        Assert.Same(assistantMsg, group.AssistantMessage);
        Assert.Equal(2, group.ToolResultMessages.Count);
        Assert.Contains(group.ToolResultMessages, m => ReferenceEquals(m, resultA));
        Assert.Contains(group.ToolResultMessages, m => ReferenceEquals(m, resultB));
    }

    [Fact]
    public void ClassifyAndGroupMessages_MixedMessage_ToolResultGoesToConversation()
    {
        var mixedMsg = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("Let me check."),
            new FunctionCallContent("call_mixed", "check", null),
        ]);
        var toolResult = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_mixed", "{\"status\":\"ok\"}")]);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Check it"),
            mixedMsg,
            toolResult,
        };

        var result = ChatMessageClassifier.ClassifyAndGroupMessages(messages);

        // Mixed message and its tool result should both be in conversation
        Assert.Contains(result.Conversation, m => ReferenceEquals(m, mixedMsg));
        Assert.Contains(result.Conversation, m => ReferenceEquals(m, toolResult));
        // No tool-call groups since the only assistant tool_call was mixed
        Assert.Empty(result.ToolCallGroups);
    }

    [Fact]
    public void ClassifyAndGroupMessages_EmptyList_ReturnsBothEmpty()
    {
        var result = ChatMessageClassifier.ClassifyAndGroupMessages([]);

        Assert.Empty(result.Conversation);
        Assert.Empty(result.ToolCallGroups);
    }

    [Fact]
    public void ClassifyAndGroupMessages_OrphanedToolResult_GoesToConversation()
    {
        // A tool result with no matching assistant tool_call should be preserved in conversation
        var orphanedResult = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_orphan", "{\"orphan\":true}")]);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello"),
            orphanedResult,
        };

        var result = ChatMessageClassifier.ClassifyAndGroupMessages(messages);

        Assert.Contains(result.Conversation, m => ReferenceEquals(m, orphanedResult));
        Assert.Empty(result.ToolCallGroups);
    }

    [Fact]
    public void ClassifyAndGroupMessages_MultipleGroups_AreCreatedSeparately()
    {
        var assistant1 = new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_1", "tool_1", null)]);
        var result1 = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_1", "{\"r\":1}")]);
        var assistant2 = new ChatMessage(ChatRole.Assistant,
            [new FunctionCallContent("call_2", "tool_2", null)]);
        var result2 = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_2", "{\"r\":2}")]);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Start"),
            assistant1, result1,
            new(ChatRole.Assistant, "Intermediate text"),
            assistant2, result2,
        };

        var result = ChatMessageClassifier.ClassifyAndGroupMessages(messages);

        Assert.Equal(2, result.Conversation.Count); // User + "Intermediate text"
        Assert.Equal(2, result.ToolCallGroups.Count);

        Assert.Same(assistant1, result.ToolCallGroups[0].AssistantMessage);
        Assert.Single(result.ToolCallGroups[0].ToolResultMessages);
        Assert.Same(result1, result.ToolCallGroups[0].ToolResultMessages[0]);

        Assert.Same(assistant2, result.ToolCallGroups[1].AssistantMessage);
        Assert.Single(result.ToolCallGroups[1].ToolResultMessages);
        Assert.Same(result2, result.ToolCallGroups[1].ToolResultMessages[0]);
    }
}
