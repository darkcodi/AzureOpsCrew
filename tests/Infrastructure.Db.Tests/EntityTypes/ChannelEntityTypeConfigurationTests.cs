using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Infrastructure.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Db.Tests.EntityTypes;

public class ChannelEntityTypeConfigurationTests
{
    [Fact]
    public async Task Channel_ShouldHave_TableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Channel));

        // Assert
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("Channels");
    }

    [Fact]
    public async Task Channel_ShouldHave_PrimaryKey()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Channel));
        var primaryKey = entity!.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle();
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public async Task Channel_Name_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Channel))!.FindProperty("Name");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Channel_Name_ShouldNotHaveMaxLength()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Channel))!.FindProperty("Name");

        // Assert
        property.Should().NotBeNull();
        // No MaxLength is configured for Name
        property!.GetMaxLength().Should().BeNull();
    }

    [Fact]
    public async Task Channel_Description_ShouldBeNullable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Channel))!.FindProperty("Description");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task Channel_ConversationId_ShouldBeNullable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Channel))!.FindProperty("ConversationId");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task Channel_DateCreated_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Channel))!.FindProperty("DateCreated");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Can_AddChannel_WithAgents()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var channelId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var channel = new Channel(channelId, "TestChannel")
            {
                AgentIds = [Guid.NewGuid(), Guid.NewGuid()]
            };
            context.Channels.Add(channel);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var channel = await context.Channels.FirstOrDefaultAsync(c => c.Id == channelId);
            channel.Should().NotBeNull();
            channel!.AgentIds.Should().HaveCount(2);
        }
    }
}
