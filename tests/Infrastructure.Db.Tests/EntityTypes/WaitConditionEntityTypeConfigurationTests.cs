using AzureOpsCrew.Domain.WaitConditions;
using AzureOpsCrew.Infrastructure.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Db.Tests.EntityTypes;

public class WaitConditionEntityTypeConfigurationTests
{
    [Fact]
    public async Task WaitCondition_ShouldHave_TableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(WaitCondition));

        // Assert
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("WaitConditions");
    }

    [Fact]
    public async Task WaitCondition_ShouldHave_PrimaryKey()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(WaitCondition));
        var primaryKey = entity!.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle();
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public async Task WaitCondition_CreatedAt_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(WaitCondition))!.FindProperty("CreatedAt");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task WaitCondition_CompletedAt_ShouldBeNullable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(WaitCondition))!.FindProperty("CompletedAt");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task WaitCondition_SatisfiedByTriggerId_ShouldBeNullable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(WaitCondition))!.FindProperty("SatisfiedByTriggerId");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }

    [Fact]
    public async Task Can_AddMessageWaitCondition()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var waitConditionId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var waitCondition = new MessageWaitCondition
            {
                Id = waitConditionId,
                AgentId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                MessageAfterDateTime = DateTime.UtcNow.AddMinutes(-5)
            };
            context.WaitConditions.Add(waitCondition);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var waitCondition = await context.WaitConditions.FirstOrDefaultAsync(w => w.Id == waitConditionId);
            waitCondition.Should().NotBeNull();
            waitCondition!.Should().BeOfType<MessageWaitCondition>();
        }
    }

    [Fact]
    public async Task Can_AddToolApprovalWaitCondition()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var waitConditionId = Guid.NewGuid();

        // Act
        using (var context = new AzureOpsCrewContext(options))
        {
            var waitCondition = new ToolApprovalWaitCondition
            {
                Id = waitConditionId,
                AgentId = Guid.NewGuid(),
                ChatId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ToolCallId = "call_123"
            };
            context.WaitConditions.Add(waitCondition);
            await context.SaveChangesAsync();
        }

        // Assert
        using (var context = new AzureOpsCrewContext(options))
        {
            var waitCondition = await context.WaitConditions.FirstOrDefaultAsync(w => w.Id == waitConditionId);
            waitCondition.Should().NotBeNull();
            waitCondition!.Should().BeOfType<ToolApprovalWaitCondition>();
        }
    }
}
