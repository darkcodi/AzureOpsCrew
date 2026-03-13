using AzureOpsCrew.Domain.Agents;
using FluentAssertions;

namespace Domain.Tests.Agents;

public class AgentTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var info = new AgentInfo("TestAgent", "Test prompt", "gpt-4");
        var providerId = Guid.NewGuid();
        var providerAgentId = "agent-123";
        var color = "#ff0000";

        // Act
        var agent = new Agent(id, info, providerId, providerAgentId, color);

        // Assert
        agent.Id.Should().Be(id);
        agent.Info.Should().Be(info);
        agent.ProviderId.Should().Be(providerId);
        agent.ProviderAgentId.Should().Be(providerAgentId);
        agent.Color.Should().Be(color);
        agent.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_ShouldUseDefaultColor_WhenNotProvided()
    {
        // Arrange
        var id = Guid.NewGuid();
        var info = new AgentInfo("TestAgent", "Test prompt", "gpt-4");
        var providerId = Guid.NewGuid();
        var providerAgentId = "agent-123";

        // Act
        var agent = new Agent(id, info, providerId, providerAgentId, "#43b581");

        // Assert
        agent.Color.Should().Be("#43b581"); // Default color
    }

    [Fact]
    public void Update_ShouldUpdateAllProperties()
    {
        // Arrange
        var agent = new Agent(
            Guid.NewGuid(),
            new AgentInfo("OldName", "Old prompt", "gpt-3.5"),
            Guid.NewGuid(),
            "old-agent-id",
            "#ff0000");
        var newInfo = new AgentInfo("NewName", "New prompt", "gpt-4");
        var newProviderId = Guid.NewGuid();
        var newColor = "#00ff00";

        // Act
        agent.Update(newInfo, newProviderId, newColor);

        // Assert
        agent.Info.Should().Be(newInfo);
        agent.ProviderId.Should().Be(newProviderId);
        agent.Color.Should().Be(newColor);
    }

    [Fact]
    public void Update_ShouldUseDefaultColor_WhenColorIsNull()
    {
        // Arrange
        var agent = new Agent(
            Guid.NewGuid(),
            new AgentInfo("OldName", "Old prompt", "gpt-3.5"),
            Guid.NewGuid(),
            "old-agent-id",
            "#ff0000");
        var newInfo = new AgentInfo("NewName", "New prompt", "gpt-4");
        var newProviderId = Guid.NewGuid();

        // Act
        agent.Update(newInfo, newProviderId, null!);

        // Assert
        agent.Color.Should().Be("#43b581"); // Default color
    }

    [Fact]
    public void SetAvailableMcpServer_ShouldAddNewServer_WhenNotAlreadyPresent()
    {
        // Arrange
        var agent = new Agent(
            Guid.NewGuid(),
            new AgentInfo("TestAgent", "Test prompt", "gpt-4"),
            Guid.NewGuid(),
            "agent-123",
            "#ff0000");
        var serverId = Guid.NewGuid();
        var availability = new AgentMcpServerToolAvailability(serverId)
        {
            EnabledToolNames = ["tool1", "tool2"],
            ApprovalRequiredNames = ["tool1"]
        };

        // Act
        agent.SetAvailableMcpServer(availability);

        // Assert
        agent.Info.AvailableMcpServerTools.Should().ContainSingle();
        agent.Info.AvailableMcpServerTools[0].McpServerConfigurationId.Should().Be(serverId);
        agent.Info.AvailableMcpServerTools[0].EnabledToolNames.Should().BeEquivalentTo(["tool1", "tool2"]);
        agent.Info.AvailableMcpServerTools[0].ApprovalRequiredNames.Should().BeEquivalentTo(["tool1"]);
    }

    [Fact]
    public void SetAvailableMcpServer_ShouldReplaceExistingServer_WhenAlreadyPresent()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var agent = new Agent(
            Guid.NewGuid(),
            new AgentInfo("TestAgent", "Test prompt", "gpt-4")
            {
                AvailableMcpServerTools = [
                    new AgentMcpServerToolAvailability(serverId)
                    {
                        EnabledToolNames = ["old-tool"],
                        ApprovalRequiredNames = []
                    }
                ]
            },
            Guid.NewGuid(),
            "agent-123",
            "#ff0000");
        var newAvailability = new AgentMcpServerToolAvailability(serverId)
        {
            EnabledToolNames = ["new-tool1", "new-tool2"],
            ApprovalRequiredNames = ["new-tool1"]
        };

        // Act
        agent.SetAvailableMcpServer(newAvailability);

        // Assert
        agent.Info.AvailableMcpServerTools.Should().ContainSingle();
        agent.Info.AvailableMcpServerTools[0].EnabledToolNames.Should().BeEquivalentTo(["new-tool1", "new-tool2"]);
        agent.Info.AvailableMcpServerTools[0].ApprovalRequiredNames.Should().BeEquivalentTo(["new-tool1"]);
    }

    [Fact]
    public void SetAvailableMcpServer_ShouldAppendServer_WhenDifferentServerId()
    {
        // Arrange
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();
        var agent = new Agent(
            Guid.NewGuid(),
            new AgentInfo("TestAgent", "Test prompt", "gpt-4")
            {
                AvailableMcpServerTools = [
                    new AgentMcpServerToolAvailability(serverId1)
                    {
                        EnabledToolNames = ["tool1"],
                        ApprovalRequiredNames = []
                    }
                ]
            },
            Guid.NewGuid(),
            "agent-123",
            "#ff0000");
        var newAvailability = new AgentMcpServerToolAvailability(serverId2)
        {
            EnabledToolNames = ["tool2"],
            ApprovalRequiredNames = []
        };

        // Act
        agent.SetAvailableMcpServer(newAvailability);

        // Assert
        agent.Info.AvailableMcpServerTools.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveAvailableMcpServer_ShouldRemoveServer_WhenExists()
    {
        // Arrange
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();
        var agent = new Agent(
            Guid.NewGuid(),
            new AgentInfo("TestAgent", "Test prompt", "gpt-4")
            {
                AvailableMcpServerTools = [
                    new AgentMcpServerToolAvailability(serverId1)
                    {
                        EnabledToolNames = ["tool1"],
                        ApprovalRequiredNames = []
                    },
                    new AgentMcpServerToolAvailability(serverId2)
                    {
                        EnabledToolNames = ["tool2"],
                        ApprovalRequiredNames = []
                    }
                ]
            },
            Guid.NewGuid(),
            "agent-123",
            "#ff0000");

        // Act
        agent.RemoveAvailableMcpServer(serverId1);

        // Assert
        agent.Info.AvailableMcpServerTools.Should().ContainSingle();
        agent.Info.AvailableMcpServerTools[0].McpServerConfigurationId.Should().Be(serverId2);
    }

    [Fact]
    public void RemoveAvailableMcpServer_ShouldDoNothing_WhenServerDoesNotExist()
    {
        // Arrange
        var serverId = Guid.NewGuid();
        var agent = new Agent(
            Guid.NewGuid(),
            new AgentInfo("TestAgent", "Test prompt", "gpt-4")
            {
                AvailableMcpServerTools = [
                    new AgentMcpServerToolAvailability(serverId)
                    {
                        EnabledToolNames = ["tool1"],
                        ApprovalRequiredNames = []
                    }
                ]
            },
            Guid.NewGuid(),
            "agent-123",
            "#ff0000");
        var nonExistentServerId = Guid.NewGuid();
        var originalCount = agent.Info.AvailableMcpServerTools.Length;

        // Act
        agent.RemoveAvailableMcpServer(nonExistentServerId);

        // Assert
        agent.Info.AvailableMcpServerTools.Should().HaveCount(originalCount);
        agent.Info.AvailableMcpServerTools[0].McpServerConfigurationId.Should().Be(serverId);
    }

    [Fact]
    public void RemoveAvailableMcpServer_ShouldHandleEmptyList()
    {
        // Arrange
        var agent = new Agent(
            Guid.NewGuid(),
            new AgentInfo("TestAgent", "Test prompt", "gpt-4"),
            Guid.NewGuid(),
            "agent-123",
            "#ff0000");

        // Act
        agent.RemoveAvailableMcpServer(Guid.NewGuid());

        // Assert
        agent.Info.AvailableMcpServerTools.Should().BeEmpty();
    }
}
