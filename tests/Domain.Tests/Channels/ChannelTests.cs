using AzureOpsCrew.Domain.Channels;
using FluentAssertions;

namespace Domain.Tests.Channels;

public class ChannelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "TestChannel";

        // Act
        var channel = new Channel(id, name);

        // Assert
        channel.Id.Should().Be(id);
        channel.Name.Should().Be(name);
        channel.Description.Should().BeNull();
        channel.ConversationId.Should().BeNull();
        channel.AgentIds.Should().BeEmpty();
        channel.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AddAgent_ShouldAddAgentIdToEmptyList()
    {
        // Arrange
        var channel = new Channel(Guid.NewGuid(), "TestChannel");
        var agentId = Guid.NewGuid();

        // Act
        channel.AddAgent(agentId);

        // Assert
        channel.AgentIds.Should().ContainSingle();
        channel.AgentIds[0].Should().Be(agentId);
    }

    [Fact]
    public void AddAgent_ShouldAppendAgentIdToExistingList()
    {
        // Arrange
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();
        var channel = new Channel(Guid.NewGuid(), "TestChannel")
        {
            AgentIds = [agentId1]
        };

        // Act
        channel.AddAgent(agentId2);

        // Assert
        channel.AgentIds.Should().HaveCount(2);
        channel.AgentIds[0].Should().Be(agentId1);
        channel.AgentIds[1].Should().Be(agentId2);
    }

    [Fact]
    public void AddAgent_ShouldAllowMultipleAdditionsOfSameAgent()
    {
        // Arrange
        var channel = new Channel(Guid.NewGuid(), "TestChannel");
        var agentId = Guid.NewGuid();

        // Act
        channel.AddAgent(agentId);
        channel.AddAgent(agentId);
        channel.AddAgent(agentId);

        // Assert
        channel.AgentIds.Should().HaveCount(3);
        channel.AgentIds.Should().AllBeEquivalentTo(agentId);
    }

    [Fact]
    public void RemoveAgent_ShouldRemoveAgentFromList()
    {
        // Arrange
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();
        var agentId3 = Guid.NewGuid();
        var channel = new Channel(Guid.NewGuid(), "TestChannel")
        {
            AgentIds = [agentId1, agentId2, agentId3]
        };

        // Act
        channel.RemoveAgent(agentId2);

        // Assert
        channel.AgentIds.Should().HaveCount(2);
        channel.AgentIds.Should().Contain(agentId1);
        channel.AgentIds.Should().Contain(agentId3);
        channel.AgentIds.Should().NotContain(agentId2);
    }

    [Fact]
    public void RemoveAgent_ShouldHandleRemovingOnlyAgent()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var channel = new Channel(Guid.NewGuid(), "TestChannel")
        {
            AgentIds = [agentId]
        };

        // Act
        channel.RemoveAgent(agentId);

        // Assert
        channel.AgentIds.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAgent_ShouldDoNothing_WhenAgentNotInList()
    {
        // Arrange
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();
        var channel = new Channel(Guid.NewGuid(), "TestChannel")
        {
            AgentIds = [agentId1]
        };
        var originalCount = channel.AgentIds.Length;

        // Act
        channel.RemoveAgent(agentId2);

        // Assert
        channel.AgentIds.Should().HaveCount(originalCount);
        channel.AgentIds[0].Should().Be(agentId1);
    }

    [Fact]
    public void RemoveAgent_ShouldHandleEmptyList()
    {
        // Arrange
        var channel = new Channel(Guid.NewGuid(), "TestChannel");
        var agentId = Guid.NewGuid();

        // Act
        channel.RemoveAgent(agentId);

        // Assert
        channel.AgentIds.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAgent_ShouldRemoveAllOccurrencesOfSameAgent()
    {
        // Arrange
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();
        var channel = new Channel(Guid.NewGuid(), "TestChannel")
        {
            AgentIds = [agentId1, agentId2, agentId2, agentId2]
        };

        // Act
        channel.RemoveAgent(agentId2);

        // Assert
        channel.AgentIds.Should().ContainSingle();
        channel.AgentIds[0].Should().Be(agentId1);
    }

    [Fact]
    public void AddAndRemoveAgent_ShouldWorkTogether()
    {
        // Arrange
        var agentId1 = Guid.NewGuid();
        var agentId2 = Guid.NewGuid();
        var agentId3 = Guid.NewGuid();
        var channel = new Channel(Guid.NewGuid(), "TestChannel")
        {
            AgentIds = [agentId1, agentId2]
        };

        // Act - Remove one, add another
        channel.RemoveAgent(agentId1);
        channel.AddAgent(agentId3);

        // Assert
        channel.AgentIds.Should().HaveCount(2);
        channel.AgentIds.Should().Contain(agentId2);
        channel.AgentIds.Should().Contain(agentId3);
        channel.AgentIds.Should().NotContain(agentId1);
    }

    [Fact]
    public void Description_ShouldBeSettable()
    {
        // Arrange
        var channel = new Channel(Guid.NewGuid(), "TestChannel");
        var description = "This is a test channel";

        // Act
        channel.Description = description;

        // Assert
        channel.Description.Should().Be(description);
    }

    [Fact]
    public void ConversationId_ShouldBeSettable()
    {
        // Arrange
        var channel = new Channel(Guid.NewGuid(), "TestChannel");
        var conversationId = "conv_12345";

        // Act
        channel.ConversationId = conversationId;

        // Assert
        channel.ConversationId.Should().Be(conversationId);
    }

    [Fact]
    public void Name_ShouldBeSettable()
    {
        // Arrange
        var channel = new Channel(Guid.NewGuid(), "OriginalName");
        var newName = "UpdatedName";

        // Act
        channel.Name = newName;

        // Assert
        channel.Name.Should().Be(newName);
    }

    [Fact]
    public void DateCreated_ShouldBeSetToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);
        var channelId = Guid.NewGuid();

        // Act
        var channel = new Channel(channelId, "TestChannel");
        var afterCreation = DateTime.UtcNow.AddSeconds(1);

        // Assert
        channel.DateCreated.Should().BeAfter(beforeCreation);
        channel.DateCreated.Should().BeBefore(afterCreation);
    }

    [Fact]
    public void MultipleAgents_ShouldBeManageable()
    {
        // Arrange
        var channel = new Channel(Guid.NewGuid(), "TestChannel");
        var agentIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

        // Act - Add all agents
        foreach (var agentId in agentIds)
        {
            channel.AddAgent(agentId);
        }

        // Assert
        channel.AgentIds.Should().HaveCount(10);
        foreach (var agentId in agentIds)
        {
            channel.AgentIds.Should().Contain(agentId);
        }

        // Act - Remove every other agent
        for (int i = 0; i < agentIds.Count; i += 2)
        {
            channel.RemoveAgent(agentIds[i]);
        }

        // Assert
        channel.AgentIds.Should().HaveCount(5);
        for (int i = 1; i < agentIds.Count; i += 2)
        {
            channel.AgentIds.Should().Contain(agentIds[i]);
        }
    }
}
