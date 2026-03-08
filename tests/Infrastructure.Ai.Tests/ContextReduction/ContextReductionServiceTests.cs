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

    /// <summary>
    /// Asserts that every assistant message with FunctionCallContent has all its matching
    /// tool-result messages present in the reduced message list, and vice versa.
    /// This is the key invariant that prevents the DeepSeek "insufficient tool messages" error.
    /// </summary>
    private static void AssertToolCallResultPairingIntegrity(IReadOnlyList<ChatMessage> messages)
    {
        // Collect all CallIds from assistant messages with FunctionCallContent
        var expectedCallIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.Assistant)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is FunctionCallContent fcc && !string.IsNullOrEmpty(fcc.CallId))
                        expectedCallIds.Add(fcc.CallId);
                }
            }
        }

        // Collect all CallIds from tool-result messages
        var actualResultCallIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.Tool)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is FunctionResultContent frc && !string.IsNullOrEmpty(frc.CallId))
                        actualResultCallIds.Add(frc.CallId);
                }
            }
        }

        // Every tool_call must have a matching tool result
        foreach (var callId in expectedCallIds)
        {
            Assert.True(actualResultCallIds.Contains(callId),
                $"Assistant message has tool_call '{callId}' but no matching tool-result message was found. " +
                "This would cause an 'insufficient tool messages following tool_calls message' API error.");
        }

        // Every tool result should have a matching tool_call (no orphaned results)
        foreach (var callId in actualResultCallIds)
        {
            Assert.True(expectedCallIds.Contains(callId),
                $"Tool-result message with CallId '{callId}' has no matching assistant tool_call message.");
        }
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
        AssertToolCallResultPairingIntegrity(result.Messages);
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
        AssertToolCallResultPairingIntegrity(result.Messages);
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

        AssertToolCallResultPairingIntegrity(result.Messages);
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

        AssertToolCallResultPairingIntegrity(result.Messages);
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
        AssertToolCallResultPairingIntegrity(result.Messages);
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
            AssertToolCallResultPairingIntegrity(result.Messages);
        }
    }

    [Fact]
    public void ReduceIfNeeded_ToolCallResultPairingIsNeverBroken()
    {
        // This test specifically targets the DeepSeek error:
        // "An assistant message with 'tool_calls' must be followed by tool messages responding to each 'tool_call_id'"
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 300,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 30,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);
        var messages = CreateMessages(userCount: 3, toolPairCount: 15);

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        Assert.True(result.Stage1Applied, "Reduction should have been triggered");
        Assert.True(result.RemovedMessageCount > 0, "Some messages should have been removed");
        AssertToolCallResultPairingIntegrity(result.Messages);
    }

    [Fact]
    public void ReduceIfNeeded_MixedMessageToolResultsArePreserved()
    {
        // When an assistant message has both text and tool_calls (mixed),
        // it's classified as conversation. Its tool results must also be preserved.
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 300,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 20,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);

        var baseTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var mixedMessage = new ChatMessage(ChatRole.Assistant,
        [
            new TextContent("Let me check that for you."),
            new FunctionCallContent("call_mixed_1", "check_status", null)
        ])
        {
            CreatedAt = baseTime.AddMinutes(1),
        };

        var mixedToolResult = new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_mixed_1", "{\"status\":\"running\"}")])
        {
            CreatedAt = baseTime.AddMinutes(2),
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Check status") { CreatedAt = baseTime },
            mixedMessage,
            mixedToolResult,
        };

        // Add many pure tool pairs to trigger reduction
        for (var i = 0; i < 20; i++)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"call_pure_{i}", $"tool_{i}", null)])
            {
                CreatedAt = baseTime.AddMinutes(3 + i * 2),
            });
            messages.Add(new ChatMessage(ChatRole.Tool,
                [new FunctionResultContent($"call_pure_{i}", $"{{\"r\":{i}}}")])
            {
                CreatedAt = baseTime.AddMinutes(4 + i * 2),
            });
        }

        messages.Sort((a, b) => Nullable.Compare(a.CreatedAt, b.CreatedAt));

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        // Both the mixed message and its tool result should be preserved
        Assert.Contains(result.Messages, m => ReferenceEquals(m, mixedMessage));
        Assert.Contains(result.Messages, m => ReferenceEquals(m, mixedToolResult));
        AssertToolCallResultPairingIntegrity(result.Messages);
    }

    [Fact]
    public void ReduceIfNeeded_MultipleToolCallsInSingleMessage_AllResultsKeptOrRemoved()
    {
        // An assistant message can have multiple tool_calls — all results must be kept together
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 400,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 50,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);

        var baseTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var messages = new List<ChatMessage>();

        messages.Add(new ChatMessage(ChatRole.User, "Do multiple things")
        {
            CreatedAt = baseTime,
        });

        // Assistant makes 3 tool calls in one message
        var multiCallMessage = new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call_a", "tool_a", null),
            new FunctionCallContent("call_b", "tool_b", null),
            new FunctionCallContent("call_c", "tool_c", null),
        ])
        {
            CreatedAt = baseTime.AddMinutes(1),
        };
        messages.Add(multiCallMessage);

        // Three separate tool result messages
        messages.Add(new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_a", "{\"a\":1}")])
        {
            CreatedAt = baseTime.AddMinutes(2),
        });
        messages.Add(new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_b", "{\"b\":2}")])
        {
            CreatedAt = baseTime.AddMinutes(3),
        });
        messages.Add(new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent("call_c", "{\"c\":3}")])
        {
            CreatedAt = baseTime.AddMinutes(4),
        });

        // Add more tool pairs to potentially push the multi-call group out
        for (var i = 0; i < 15; i++)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"call_later_{i}", $"tool_later_{i}", null)])
            {
                CreatedAt = baseTime.AddMinutes(5 + i * 2),
            });
            messages.Add(new ChatMessage(ChatRole.Tool,
                [new FunctionResultContent($"call_later_{i}", $"{{\"r\":{i}}}")])
            {
                CreatedAt = baseTime.AddMinutes(6 + i * 2),
            });
        }

        messages.Sort((a, b) => Nullable.Compare(a.CreatedAt, b.CreatedAt));

        var result = service.ReduceIfNeeded(messages, "system prompt", [], "unknown-model");

        // The critical check: pairing must never be broken
        AssertToolCallResultPairingIntegrity(result.Messages);

        // If the multi-call message was removed, all 3 results must also be removed
        if (!result.Messages.Any(m => ReferenceEquals(m, multiCallMessage)))
        {
            Assert.DoesNotContain(result.Messages, m =>
                m.Role == ChatRole.Tool && m.Contents.Any(c =>
                    c is FunctionResultContent frc && (frc.CallId == "call_a" || frc.CallId == "call_b" || frc.CallId == "call_c")));
        }
    }

    [Fact]
    public void ReduceIfNeeded_GroupsAreRemovedAtomically_NeverHalfKept()
    {
        // Verify that tool-call groups (assistant + results) are removed as atomic units.
        // We should never see a tool result without its assistant message, or vice versa.
        var settings = new ContextReductionSettings
        {
            FallbackContextWindowSize = 200,
            SoftThresholdPercent = 0.85,
            RecentToolBudgetTokens = 20,
            MinReservedOutputTokens = 50,
            SafetyMargin = 1.0,
            CharsPerToken = 4.0,
        };

        var service = CreateService(settings);

        // Create many tool pairs to force aggressive reduction
        var messages = CreateMessages(userCount: 2, toolPairCount: 20);

        var result = service.ReduceIfNeeded(messages, "system prompt with some extra content to eat budget", [], "unknown-model");

        Assert.True(result.Stage1Applied);
        AssertToolCallResultPairingIntegrity(result.Messages);
    }
}
