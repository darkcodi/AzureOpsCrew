using System.Text.Json;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Infrastructure.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Db.Tests.EntityTypes;

public class AgentEntityTypeConfigurationTests
{
    [Fact]
    public async Task Agent_ShouldHave_TableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Agent));

        // Assert
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("Agents");
    }

    [Fact]
    public async Task Agent_ShouldHave_PrimaryKey()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Agent));
        var primaryKey = entity!.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle();
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public async Task Agent_Id_ShouldBeValueGeneratedOnAdd()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Agent))!.FindProperty("Id");

        // Assert
        property.Should().NotBeNull();
        property!.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd);
    }

    [Fact]
    public async Task Agent_ProviderAgentId_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Agent))!.FindProperty("ProviderAgentId");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Agent_Color_ShouldHaveDefaultValue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Agent))!.FindProperty("Color");

        // Assert
        property.Should().NotBeNull();
        property!.GetDefaultValue().Should().Be("#43b581");
    }

    [Fact]
    public async Task Agent_Color_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Agent))!.FindProperty("Color");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Agent_AvailableMcpServerTools_ShouldConvertToJson()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var serverId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
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
                "provider-agent-id",
                "#43b581");
            context.Agents.Add(agent);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var agent = await context.Agents.FirstOrDefaultAsync();
            agent.Should().NotBeNull();
            agent!.Info.AvailableMcpServerTools.Should().ContainSingle();
            agent.Info.AvailableMcpServerTools[0].McpServerConfigurationId.Should().Be(serverId);
        }
    }

    [Fact]
    public async Task Agent_ProviderId_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Agent))!.FindProperty("ProviderId");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Agent_DateCreated_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Agent))!.FindProperty("DateCreated");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }
}
