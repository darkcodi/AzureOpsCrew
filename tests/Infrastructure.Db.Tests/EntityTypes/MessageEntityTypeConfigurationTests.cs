using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Db.Tests.EntityTypes;

public class MessageEntityTypeConfigurationTests
{
    [Fact]
    public async Task Message_ShouldHave_TableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Message));

        // Assert
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("Messages");
    }

    [Fact]
    public async Task Message_ShouldHave_PrimaryKey()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Message));
        var primaryKey = entity!.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle();
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public async Task Message_Text_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Message))!.FindProperty("Text");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Message_PostedAt_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Message))!.FindProperty("PostedAt");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Message_ShouldHaveIndexOnPostedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var indexes = context.Model.FindEntityType(typeof(Message))!.GetIndexes();
        var index = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "PostedAt"));

        // Assert
        index.Should().NotBeNull();
    }

    [Fact]
    public async Task Message_ShouldHaveIndexOnAgentId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var indexes = context.Model.FindEntityType(typeof(Message))!.GetIndexes();
        var index = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "AgentId"));

        // Assert
        index.Should().NotBeNull();
    }

    [Fact]
    public async Task Message_ShouldHaveIndexOnUserId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var indexes = context.Model.FindEntityType(typeof(Message))!.GetIndexes();
        var index = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "UserId"));

        // Assert
        index.Should().NotBeNull();
    }

    [Fact]
    public async Task Message_ShouldHaveIndexOnChannelId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var indexes = context.Model.FindEntityType(typeof(Message))!.GetIndexes();
        var index = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "ChannelId"));

        // Assert
        index.Should().NotBeNull();
    }

    [Fact]
    public async Task Message_ShouldHaveIndexOnDmId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var indexes = context.Model.FindEntityType(typeof(Message))!.GetIndexes();
        var index = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "DmId"));

        // Assert
        index.Should().NotBeNull();
    }

    [Fact]
    public async Task Message_ShouldHaveIndexOnAgentThoughtId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var indexes = context.Model.FindEntityType(typeof(Message))!.GetIndexes();
        var index = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "AgentThoughtId"));

        // Assert
        index.Should().NotBeNull();
    }

    [Fact]
    public async Task Message_AuthorName_ShouldBeNullable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Message))!.FindProperty("AuthorName");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }
}
