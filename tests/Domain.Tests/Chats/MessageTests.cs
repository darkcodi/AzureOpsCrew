using AzureOpsCrew.Domain.Chats;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace Domain.Tests.Chats;

public class MessageTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var message = new Message();

        // Assert
        message.Id.Should().BeEmpty();
        message.Text.Should().BeEmpty();
        message.PostedAt.Should().Be(default);
        message.AuthorName.Should().BeNull();
        message.AgentId.Should().BeNull();
        message.UserId.Should().BeNull();
        message.ChannelId.Should().BeNull();
        message.DmId.Should().BeNull();
        message.AgentThoughtId.Should().BeNull();
    }

    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var text = "Hello, world!";
        var postedAt = DateTime.UtcNow;
        var authorName = "TestUser";
        var agentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var dmId = Guid.NewGuid();
        var agentThoughtId = Guid.NewGuid();

        // Act
        var message = new Message
        {
            Id = id,
            Text = text,
            PostedAt = postedAt,
            AuthorName = authorName,
            AgentId = agentId,
            UserId = userId,
            ChannelId = channelId,
            DmId = dmId,
            AgentThoughtId = agentThoughtId
        };

        // Assert
        message.Id.Should().Be(id);
        message.Text.Should().Be(text);
        message.PostedAt.Should().Be(postedAt);
        message.AuthorName.Should().Be(authorName);
        message.AgentId.Should().Be(agentId);
        message.UserId.Should().Be(userId);
        message.ChannelId.Should().Be(channelId);
        message.DmId.Should().Be(dmId);
        message.AgentThoughtId.Should().Be(agentThoughtId);
    }

    [Fact]
    public void ToChatMessage_FromUser_ShouldReturnUserRole()
    {
        // Arrange
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = "Hello from user!",
            PostedAt = DateTime.UtcNow,
            AuthorName = "TestUser",
            UserId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid()
        };

        // Act
        var chatMessage = message.ToChatMessage();

        // Assert
        chatMessage.Role.Should().Be(ChatRole.User);
        chatMessage.AuthorName.Should().Be("TestUser");
        chatMessage.CreatedAt.Should().BeCloseTo(
            new DateTimeOffset(message.PostedAt, TimeSpan.Zero),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ToChatMessage_FromAgent_ShouldReturnAssistantRole()
    {
        // Arrange
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = "Hello from agent!",
            PostedAt = DateTime.UtcNow,
            AuthorName = "AgentSmith",
            AgentId = Guid.NewGuid(),
            ChannelId = Guid.NewGuid()
        };

        // Act
        var chatMessage = message.ToChatMessage();

        // Assert
        chatMessage.Role.Should().Be(ChatRole.Assistant);
        chatMessage.AuthorName.Should().Be("AgentSmith");
        chatMessage.CreatedAt.Should().BeCloseTo(
            new DateTimeOffset(message.PostedAt, TimeSpan.Zero),
            TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ToChatMessage_ShouldConvertTextToTextContent()
    {
        // Arrange
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = "This is a test message",
            PostedAt = DateTime.UtcNow,
            UserId = Guid.NewGuid()
        };

        // Act
        var chatMessage = message.ToChatMessage();

        // Assert
        // ChatMessage contains the text content - verify it's non-empty and contains expected text
        chatMessage.ToString().Should().Contain("This is a test message");
    }

    [Fact]
    public void ToChatMessage_WithNullAuthorName_ShouldHandleGracefully()
    {
        // Arrange
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = "Message without author",
            PostedAt = DateTime.UtcNow,
            AuthorName = null,
            UserId = Guid.NewGuid()
        };

        // Act
        var chatMessage = message.ToChatMessage();

        // Assert
        chatMessage.AuthorName.Should().BeNull();
    }

    [Fact]
    public void ToChatMessage_ShouldPreservePostedAtAsCreatedAt()
    {
        // Arrange
        var postedAt = new DateTime(2024, 3, 15, 10, 30, 0, DateTimeKind.Utc);
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = "Test message",
            PostedAt = postedAt,
            UserId = Guid.NewGuid()
        };

        // Act
        var chatMessage = message.ToChatMessage();

        // Assert
        chatMessage.CreatedAt.Should().Be(new DateTimeOffset(postedAt, TimeSpan.Zero));
    }

    [Fact]
    public void ToChatMessage_WithEmptyText_ShouldHandleGracefully()
    {
        // Arrange
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = string.Empty,
            PostedAt = DateTime.UtcNow,
            UserId = Guid.NewGuid()
        };

        // Act
        var chatMessage = message.ToChatMessage();

        // Assert
        // Empty text should still create a valid ChatMessage
        chatMessage.Role.Should().Be(ChatRole.User);
    }

    [Fact]
    public void ToChatMessage_WhenBothAgentIdAndUserIdAreNull_ShouldDefaultToUserRole()
    {
        // Arrange
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = "Ambiguous message",
            PostedAt = DateTime.UtcNow,
            AgentId = null,
            UserId = null
        };

        // Act
        var chatMessage = message.ToChatMessage();

        // Assert
        chatMessage.Role.Should().Be(ChatRole.User);
    }

    [Fact]
    public void ToChatMessage_WhenBothAgentIdAndUserIdAreSet_ShouldPreferAgent()
    {
        // Arrange
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = "Message from both",
            PostedAt = DateTime.UtcNow,
            AgentId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        // Act
        var chatMessage = message.ToChatMessage();

        // Assert
        chatMessage.Role.Should().Be(ChatRole.Assistant);
    }
}
