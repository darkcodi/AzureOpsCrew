using AzureOpsCrew.Domain.Triggers;
using AzureOpsCrew.Infrastructure.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Db.Tests.EntityTypes;

public class TriggerEntityTypeConfigurationTests
{
    [Fact]
    public async Task Trigger_ShouldHave_TableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Trigger));

        // Assert
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("Triggers");
    }

    [Fact]
    public async Task Trigger_ShouldHave_PrimaryKey()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Trigger));
        var primaryKey = entity!.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle();
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public async Task Trigger_CreatedAt_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Trigger))!.FindProperty("CreatedAt");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Trigger_StartedAt_ShouldBeNullable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Trigger))!.FindProperty("StartedAt");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task Trigger_CompletedAt_ShouldBeNullable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Trigger))!.FindProperty("CompletedAt");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task Can_AddMessageTrigger()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var triggerId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var trigger = new MessageTrigger
            {
                Id = triggerId,
                AgentId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                MessageId = Guid.NewGuid(),
                AuthorId = Guid.NewGuid(),
                AuthorName = "TestUser",
                MessageContent = "Test message"
            };
            context.Triggers.Add(trigger);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var trigger = await context.Triggers.FirstOrDefaultAsync(t => t.Id == triggerId);
            trigger.Should().NotBeNull();
            trigger!.Should().BeOfType<MessageTrigger>();
        }
    }

    [Fact]
    public async Task Can_AddToolApprovalTrigger()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var triggerId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var trigger = new ToolApprovalTrigger
            {
                Id = triggerId,
                AgentId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                CallId = "call_123",
                ToolName = "test_tool",
                Parameters = "{}"
            };
            context.Triggers.Add(trigger);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var trigger = await context.Triggers.FirstOrDefaultAsync(t => t.Id == triggerId);
            trigger.Should().NotBeNull();
            trigger!.Should().BeOfType<ToolApprovalTrigger>();
        }
    }
}
