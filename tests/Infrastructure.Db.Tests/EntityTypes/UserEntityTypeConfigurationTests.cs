using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Db.Tests.EntityTypes;

public class UserEntityTypeConfigurationTests
{
    [Fact]
    public async Task User_ShouldHave_TableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(User));

        // Assert
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("Users");
    }

    [Fact]
    public async Task User_ShouldHave_PrimaryKey()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(User));
        var primaryKey = entity!.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle();
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public async Task User_Email_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("Email");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task User_Email_ShouldHaveMaxLength200()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("Email");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(320);
    }

    [Fact]
    public async Task User_Email_ShouldHaveUniqueIndex()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var indexes = context.Model.FindEntityType(typeof(User))!.GetIndexes();
        var normalizedEmailIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "NormalizedEmail"));

        // Assert
        normalizedEmailIndex.Should().NotBeNull();
        normalizedEmailIndex!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task User_NormalizedEmail_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("NormalizedEmail");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task User_NormalizedEmail_ShouldHaveMaxLength320()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("NormalizedEmail");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(320);
    }

    [Fact]
    public async Task User_Username_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("Username");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task User_Username_ShouldHaveMaxLength30()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("Username");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(30);
    }

    [Fact]
    public async Task User_NormalizedUsername_ShouldHaveUniqueIndex()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var indexes = context.Model.FindEntityType(typeof(User))!.GetIndexes();
        var normalizedUsernameIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "NormalizedUsername"));

        // Assert
        normalizedUsernameIndex.Should().NotBeNull();
        normalizedUsernameIndex!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task User_NormalizedUsername_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("NormalizedUsername");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task User_NormalizedUsername_ShouldHaveMaxLength30()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("NormalizedUsername");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(30);
    }

    [Fact]
    public async Task User_PasswordHash_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("PasswordHash");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task User_IsActive_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("IsActive");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task User_DateCreated_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty("DateCreated");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }
}
