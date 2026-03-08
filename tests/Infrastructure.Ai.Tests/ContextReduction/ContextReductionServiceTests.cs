using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Infrastructure.Ai.ContextReduction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai.Tests.ContextReduction;

public class ContextReductionServiceTests
{
    private static ContextReductionService CreateService(ContextReductionSettings? settings = null)
    {
        settings ??= new ContextReductionSettings();
        return new ContextReductionService(Options.Create(settings));
    }

    private static List<ChatMessage> CreateMessages(int userCount, int toolPairCount)
    {
        var messages = new List<ChatMessage>();
        var baseTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < userCount; i++)
        {
            messages.Add(new ChatMessage(ChatRole.User, $"User message {i}")
            {
                CreatedAt = baseTime.AddMinutes(i * 3),
            });
        }

        for (var i = 0; i < toolPairCount; i++)
        {
            var callId = $"call_{i}";
            var offset = (userCount * 3) + (i * 2);

            messages.Add(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent(callId, $"tool_{i}", null)])
            {
                CreatedAt = baseTime.AddMinutes(offset),
            });

            messages.Add(new ChatMessage(ChatRole.Tool,
                [new FunctionResultContent(callId, $"{{\"result\":{i}}}")])
            {
                CreatedAt = baseTime.AddMinutes(offset + 1),
            });
        }

        messages.Sort((a, b) => Nullable.Compare(a.CreatedAt, b.CreatedAt));
        return messages;
    }

    [Fact]
    public void ReduceIfNeeded_UnderBudget_ReturnsOriginal()
    {
        var service = CreateService();
        var messages = CreateMessages(userCount: 2, toolPairCount: 1);

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "gpt-4o");

        Assert.False(result.Stage1Applied);
        Assert.Equal(messages.Count, result.Messages.Count);
        Assert.Equal(0, result.RemovedMessageCount);
    }

    [Fact]
    public void ReduceIfNeeded_OverSoftThreshold_RemovesOlderToolMessages()
    {
        // Use a very small context window to force reduction
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 200,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 50,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);

        // Create enough messages to exceed the tiny budget
        var messages = CreateMessages(userCount: 3, toolPairCount: 10);

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        Assert.True(result.Stage1Applied);
        Assert.True(result.RemovedMessageCount > 0);
        Assert.True(result.ReducedMessageTokens <= result.OriginalMessageTokens);
    }

    [Fact]
    public void ReduceIfNeeded_KeepsNewestToolMessagesWithinBudget()
    {
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 500,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 100,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);
        var messages = CreateMessages(userCount: 2, toolPairCount: 10);

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        if (result.Stage1Applied)
        {
            // The newest tool messages should be preserved
            var lastToolCall = messages.Last(m =>
                m.Contents.Any(c => c is FunctionCallContent));
            Assert.Contains(result.Messages, m => ReferenceEquals(m, lastToolCall));
        }
    }

    [Fact]
    public void ReduceIfNeeded_PreservesChronologicalOrder()
    {
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 500,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 50,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);
        var messages = CreateMessages(userCount: 3, toolPairCount: 10);

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        for (var i = 1; i < result.Messages.Count; i++)
        {
            Assert.True(result.Messages[i].CreatedAt >= result.Messages[i - 1].CreatedAt,
                $"Messages not in chronological order at index {i}");
        }
    }

    [Fact]
    public void ReduceIfNeeded_NoToolMessages_ReturnsOriginal()
    {
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 200,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 50,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);

        // Only user/assistant text messages — no tool calls
        var baseTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello") { CreatedAt = baseTime },
            new(ChatRole.Assistant, "Hi there!") { CreatedAt = baseTime.AddMinutes(1) },
            new(ChatRole.User, "How are you?") { CreatedAt = baseTime.AddMinutes(2) },
        };

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        // Even if over budget, with no tool messages to remove, all messages are preserved
        Assert.Equal(messages.Count, result.Messages.Count);
    }

    [Fact]
    public void ReduceIfNeeded_MixedMessages_ArePreserved()
    {
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 500,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 50,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);

        var baseTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var mixedMessage = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("Let me check."),
            new FunctionCallContent("call_mixed", "deploy", null)
        ])
        {
            CreatedAt = baseTime.AddMinutes(1),
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Deploy it") { CreatedAt = baseTime },
            mixedMessage,
            new(ChatRole.Tool, [new FunctionResultContent("call_mixed", "{\"ok\":true}")]) { CreatedAt = baseTime.AddMinutes(2) },
        };

        // Add more tool messages to potentially trigger reduction
        for (var i = 0; i < 20; i++)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"call_{i}", $"tool_{i}", null)])
            {
                CreatedAt = baseTime.AddMinutes(3 + i * 2),
            });
            messages.Add(new ChatMessage(ChatRole.Tool,
                [new FunctionResultContent($"call_{i}", $"{{\"r\":{i}}}")])
            {
                CreatedAt = baseTime.AddMinutes(4 + i * 2),
            });
        }

        messages.Sort((a, b) => Nullable.Compare(a.CreatedAt, b.CreatedAt));

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        // Mixed message should always be preserved (classified as conversation)
        Assert.Contains(result.Messages, m => ReferenceEquals(m, mixedMessage));
    }

    [Fact]
    public void ReduceIfNeeded_UnknownModel_UsesFallbackContextSize()
    {
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 1000,
            SoftThresholdPercent = 0.85,
            MinReservedOutputTokens = 100,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);
        var messages = CreateMessages(userCount: 2, toolPairCount: 1);

        var result = service.ReduceIfNeeded(messages, null, [], "completely-unknown-model-xyz");

        // Should not throw; should use fallback
        Assert.NotNull(result);
        Assert.True(result.MaxInputBudget > 0);
    }

    [Fact]
    public void ReduceIfNeeded_AtLeastOneToolMessageIsAlwaysKept()
    {
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 300,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 1, // Very tiny budget — but at least one should be kept
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);
        var messages = CreateMessages(userCount: 2, toolPairCount: 5);

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        if (result.Stage1Applied)
        {
            // At least one tool-related message should remain
            var hasToolMessage = result.Messages.Any(m =>
                m.Contents.Any(c => c is FunctionCallContent or FunctionResultContent));
            Assert.True(hasToolMessage, "At least one tool message should be kept");
        }
    }
}
