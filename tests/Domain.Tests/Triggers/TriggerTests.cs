using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Triggers;
using FluentAssertions;

namespace Domain.Tests.Triggers;

public class TriggerTests
{
    [Fact]
    public void MessageTrigger_ShouldInitializeWithDefaultValues()
    {
        // Act
        var trigger = new MessageTrigger();

        // Assert
        trigger.Id.Should().BeEmpty();
        trigger.AgentId.Should().BeEmpty();
        trigger.ChatId.Should().BeEmpty();
        trigger.CreatedAt.Should().Be(default);
        trigger.StartedAt.Should().BeNull();
        trigger.CompletedAt.Should().BeNull();
        trigger.IsSkipped.Should().BeFalse();
        trigger.MessageId.Should().BeEmpty();
        trigger.AuthorId.Should().BeEmpty();
        trigger.AuthorName.Should().BeEmpty();
        trigger.MessageContent.Should().BeEmpty();
    }

    [Fact]
    public void MessageTrigger_ShouldSetAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var authorName = "TestUser";
        var messageContent = "Hello, world!";
        var createdAt = DateTime.UtcNow;
        var startedAt = DateTime.UtcNow.AddMinutes(1);
        var completedAt = DateTime.UtcNow.AddMinutes(2);

        // Act
        var trigger = new MessageTrigger
        {
            Id = id,
            AgentId = agentId,
            ChatId = chatId,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            IsSkipped = false,
            MessageId = messageId,
            AuthorId = authorId,
            AuthorName = authorName,
            MessageContent = messageContent
        };

        // Assert
        trigger.Id.Should().Be(id);
        trigger.AgentId.Should().Be(agentId);
        trigger.ChatId.Should().Be(chatId);
        trigger.CreatedAt.Should().Be(createdAt);
        trigger.StartedAt.Should().Be(startedAt);
        trigger.CompletedAt.Should().Be(completedAt);
        trigger.IsSkipped.Should().BeFalse();
        trigger.MessageId.Should().Be(messageId);
        trigger.AuthorId.Should().Be(authorId);
        trigger.AuthorName.Should().Be(authorName);
        trigger.MessageContent.Should().Be(messageContent);
    }

    [Fact]
    public void ToolApprovalTrigger_ShouldInitializeWithDefaultValues()
    {
        // Act
        var trigger = new ToolApprovalTrigger();

        // Assert
        trigger.Id.Should().BeEmpty();
        trigger.AgentId.Should().BeEmpty();
        trigger.ChatId.Should().BeEmpty();
        trigger.CreatedAt.Should().Be(default);
        trigger.StartedAt.Should().BeNull();
        trigger.CompletedAt.Should().BeNull();
        trigger.IsSkipped.Should().BeFalse();
        trigger.CallId.Should().BeEmpty();
        trigger.Resolution.Should().Be(ApprovalResolution.None);
        trigger.ToolName.Should().BeEmpty();
        trigger.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void ToolApprovalTrigger_ShouldSetAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var callId = "call_123";
        var resolution = ApprovalResolution.Approved;
        var toolName = "deploy_tool";
        var parameters = "{\"environment\":\"production\"}";
        var createdAt = DateTime.UtcNow;
        var startedAt = DateTime.UtcNow.AddMinutes(1);
        var completedAt = DateTime.UtcNow.AddMinutes(2);

        // Act
        var trigger = new ToolApprovalTrigger
        {
            Id = id,
            AgentId = agentId,
            ChatId = chatId,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            IsSkipped = false,
            CallId = callId,
            Resolution = resolution,
            ToolName = toolName,
            Parameters = parameters
        };

        // Assert
        trigger.Id.Should().Be(id);
        trigger.AgentId.Should().Be(agentId);
        trigger.ChatId.Should().Be(chatId);
        trigger.CreatedAt.Should().Be(createdAt);
        trigger.StartedAt.Should().Be(startedAt);
        trigger.CompletedAt.Should().Be(completedAt);
        trigger.IsSkipped.Should().BeFalse();
        trigger.CallId.Should().Be(callId);
        trigger.Resolution.Should().Be(resolution);
        trigger.ToolName.Should().Be(toolName);
        trigger.Parameters.Should().Be(parameters);
    }

    [Theory]
    [InlineData(ApprovalResolution.None)]
    [InlineData(ApprovalResolution.Approved)]
    [InlineData(ApprovalResolution.Rejected)]
    public void ToolApprovalTrigger_ShouldAcceptAllResolutions(ApprovalResolution resolution)
    {
        // Arrange & Act
        var trigger = new ToolApprovalTrigger
        {
            Resolution = resolution
        };

        // Assert
        trigger.Resolution.Should().Be(resolution);
    }

    [Fact]
    public void TriggerBase_ShouldAllowPolymorphicUsage()
    {
        // Arrange
        var messageTrigger = new MessageTrigger
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        var toolApprovalTrigger = new ToolApprovalTrigger
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        // Act - Use base class type
        List<Trigger> triggers = [messageTrigger, toolApprovalTrigger];

        // Assert
        triggers.Should().HaveCount(2);
        triggers[0].Should().BeOfType<MessageTrigger>();
        triggers[1].Should().BeOfType<ToolApprovalTrigger>();
    }

    [Fact]
    public void MessageTrigger_ShouldBeSkipped_WhenIsSkippedSetToTrue()
    {
        // Arrange
        var trigger = new MessageTrigger
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            ChatId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            IsSkipped = false
        };

        // Act
        trigger.IsSkipped = true;

        // Assert
        trigger.IsSkipped.Should().BeTrue();
    }

    [Fact]
    public void ToolApprovalTrigger_ShouldTrackApprovalState()
    {
        // Arrange
        var trigger = new ToolApprovalTrigger
        {
            CallId = "call_123",
            ToolName = "deploy_tool"
        };

        // Act
        trigger.Resolution = ApprovalResolution.Approved;
        var wasApproved = trigger.Resolution == ApprovalResolution.Approved;

        // Assert
        wasApproved.Should().BeTrue();
    }
}
