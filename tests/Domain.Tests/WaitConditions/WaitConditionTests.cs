using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Triggers;
using AzureOpsCrew.Domain.WaitConditions;
using FluentAssertions;

namespace Domain.Tests.WaitConditions;

public class WaitConditionTests
{
    [Fact]
    public void MessageWaitCondition_ShouldInitializeWithDefaultValues()
    {
        // Act
        var waitCondition = new MessageWaitCondition();

        // Assert
        waitCondition.Id.Should().BeEmpty();
        waitCondition.AgentId.Should().BeEmpty();
        waitCondition.ChatId.Should().BeEmpty();
        waitCondition.CreatedAt.Should().Be(default);
        waitCondition.CompletedAt.Should().BeNull();
        waitCondition.SatisfiedByTriggerId.Should().BeNull();
        waitCondition.MessageAfterDateTime.Should().Be(default);
    }

    [Fact]
    public void MessageWaitCondition_ShouldSetAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var messageAfter = DateTime.UtcNow.AddMinutes(-5);

        // Act
        var waitCondition = new MessageWaitCondition
        {
            Id = id,
            AgentId = agentId,
            ChatId = chatId,
            CreatedAt = createdAt,
            MessageAfterDateTime = messageAfter
        };

        // Assert
        waitCondition.Id.Should().Be(id);
        waitCondition.AgentId.Should().Be(agentId);
        waitCondition.ChatId.Should().Be(chatId);
        waitCondition.CreatedAt.Should().Be(createdAt);
        waitCondition.MessageAfterDateTime.Should().Be(messageAfter);
    }

    [Fact]
    public void MessageWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnFalse_WhenTriggerIsNotMessageTrigger()
    {
        // Arrange
        var waitCondition = new MessageWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageAfterDateTime = DateTime.UtcNow.AddMinutes(-5)
        };
        var toolApprovalTrigger = new ToolApprovalTrigger
        {
            ChatId = waitCondition.ChatId
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(toolApprovalTrigger);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MessageWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnFalse_WhenChatIdDoesNotMatch()
    {
        // Arrange
        var waitCondition = new MessageWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageAfterDateTime = DateTime.UtcNow.AddMinutes(-5)
        };
        var messageTrigger = new MessageTrigger
        {
            ChatId = Guid.NewGuid(), // Different chat ID
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(messageTrigger);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MessageWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnFalse_WhenMessageIsTooOld()
    {
        // Arrange
        var waitCondition = new MessageWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageAfterDateTime = DateTime.UtcNow.AddMinutes(-5)
        };
        var messageTrigger = new MessageTrigger
        {
            ChatId = waitCondition.ChatId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10) // Too old
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(messageTrigger);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MessageWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnTrue_WhenMessageIsNewEnough()
    {
        // Arrange
        var waitCondition = new MessageWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageAfterDateTime = DateTime.UtcNow.AddMinutes(-5)
        };
        var messageTrigger = new MessageTrigger
        {
            ChatId = waitCondition.ChatId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1) // New enough
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(messageTrigger);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MessageWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnTrue_WhenMessageIsExactlyAtThreshold()
    {
        // Arrange
        var threshold = DateTime.UtcNow.AddMinutes(-5);
        var waitCondition = new MessageWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageAfterDateTime = threshold
        };
        var messageTrigger = new MessageTrigger
        {
            ChatId = waitCondition.ChatId,
            CreatedAt = threshold // Exactly at threshold
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(messageTrigger);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ToolApprovalWaitCondition_ShouldInitializeWithDefaultValues()
    {
        // Act
        var waitCondition = new ToolApprovalWaitCondition();

        // Assert
        waitCondition.Id.Should().BeEmpty();
        waitCondition.AgentId.Should().BeEmpty();
        waitCondition.ChatId.Should().BeEmpty();
        waitCondition.CreatedAt.Should().Be(default);
        waitCondition.CompletedAt.Should().BeNull();
        waitCondition.SatisfiedByTriggerId.Should().BeNull();
        waitCondition.ToolCallId.Should().BeEmpty();
    }

    [Fact]
    public void ToolApprovalWaitCondition_ShouldSetAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var toolCallId = "call_123";

        // Act
        var waitCondition = new ToolApprovalWaitCondition
        {
            Id = id,
            AgentId = agentId,
            ChatId = chatId,
            CreatedAt = createdAt,
            ToolCallId = toolCallId
        };

        // Assert
        waitCondition.Id.Should().Be(id);
        waitCondition.AgentId.Should().Be(agentId);
        waitCondition.ChatId.Should().Be(chatId);
        waitCondition.CreatedAt.Should().Be(createdAt);
        waitCondition.ToolCallId.Should().Be(toolCallId);
    }

    [Fact]
    public void ToolApprovalWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnFalse_WhenTriggerIsNotToolApprovalTrigger()
    {
        // Arrange
        var waitCondition = new ToolApprovalWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ToolCallId = "call_123"
        };
        var messageTrigger = new MessageTrigger
        {
            ChatId = waitCondition.ChatId
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(messageTrigger);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ToolApprovalWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnFalse_WhenChatIdDoesNotMatch()
    {
        // Arrange
        var waitCondition = new ToolApprovalWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ToolCallId = "call_123"
        };
        var toolApprovalTrigger = new ToolApprovalTrigger
        {
            ChatId = Guid.NewGuid(), // Different chat ID
            CallId = "call_123"
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(toolApprovalTrigger);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ToolApprovalWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnFalse_WhenToolCallIdDoesNotMatch()
    {
        // Arrange
        var waitCondition = new ToolApprovalWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ToolCallId = "call_123"
        };
        var toolApprovalTrigger = new ToolApprovalTrigger
        {
            ChatId = waitCondition.ChatId,
            CallId = "call_456" // Different call ID
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(toolApprovalTrigger);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ToolApprovalWaitCondition_CanBeSatisfiedByTrigger_ShouldReturnTrue_WhenToolCallIdMatches()
    {
        // Arrange
        var waitCondition = new ToolApprovalWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ToolCallId = "call_123"
        };
        var toolApprovalTrigger = new ToolApprovalTrigger
        {
            ChatId = waitCondition.ChatId,
            CallId = "call_123"
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(toolApprovalTrigger);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("call_abc", "CALL_ABC")]
    [InlineData("CALL_123", "call_123")]
    [InlineData("Call_Xyz", "cALL_xYZ")]
    public void ToolApprovalWaitCondition_CanBeSatisfiedByTrigger_ShouldBeCaseInsensitive(string waitConditionCallId, string triggerCallId)
    {
        // Arrange
        var waitCondition = new ToolApprovalWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ToolCallId = waitConditionCallId
        };
        var toolApprovalTrigger = new ToolApprovalTrigger
        {
            ChatId = waitCondition.ChatId,
            CallId = triggerCallId
        };

        // Act
        var result = waitCondition.CanBeSatisfiedByTrigger(toolApprovalTrigger);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void WaitConditionBase_ShouldAllowPolymorphicUsage()
    {
        // Arrange
        var messageWaitCondition = new MessageWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageAfterDateTime = DateTime.UtcNow.AddMinutes(-5)
        };
        var toolApprovalWaitCondition = new ToolApprovalWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ToolCallId = "call_123"
        };

        // Act - Use base class type
        List<WaitCondition> waitConditions = [messageWaitCondition, toolApprovalWaitCondition];

        // Assert
        waitConditions.Should().HaveCount(2);
        waitConditions[0].Should().BeOfType<MessageWaitCondition>();
        waitConditions[1].Should().BeOfType<ToolApprovalWaitCondition>();
    }

    [Fact]
    public void WaitCondition_ShouldSetSatisfiedByTriggerId()
    {
        // Arrange
        var waitCondition = new MessageWaitCondition
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            MessageAfterDateTime = DateTime.UtcNow.AddMinutes(-5)
        };
        var triggerId = Guid.NewGuid();

        // Act
        waitCondition.SatisfiedByTriggerId = triggerId;

        // Assert
        waitCondition.SatisfiedByTriggerId.Should().Be(triggerId);
    }
}
