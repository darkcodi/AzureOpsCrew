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
}
