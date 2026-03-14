using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Db;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Db.Tests.EntityTypes;

public class ProviderEntityTypeConfigurationTests
{
    [Fact]
    public async Task Provider_ShouldHave_TableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Provider));

        // Assert
        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("Providers");
    }

    [Fact]
    public async Task Provider_ShouldHave_PrimaryKey()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var entity = context.Model.FindEntityType(typeof(Provider));
        var primaryKey = entity!.FindPrimaryKey();

        // Assert
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Should().ContainSingle();
        primaryKey.Properties[0].Name.Should().Be("Id");
    }

    [Fact]
    public async Task Provider_Name_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("Name");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Provider_Name_ShouldHaveMaxLength200()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("Name");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(200);
    }

    [Fact]
    public async Task Provider_ProviderType_ShouldBeConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("ProviderType");

        // Assert
        // Verify the ProviderType property exists and is configured
        property.Should().NotBeNull();
    }

    [Fact]
    public async Task Provider_ApiKey_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("ApiKey");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Provider_ApiKey_ShouldHaveMaxLength500()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("ApiKey");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(500);
    }

    [Fact]
    public async Task Provider_ApiEndpoint_ShouldHaveMaxLength500()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("ApiEndpoint");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(500);
    }

    [Fact]
    public async Task Provider_DefaultModel_ShouldHaveMaxLength200()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("DefaultModel");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(200);
    }

    [Fact]
    public async Task Provider_SelectedModels_ShouldHaveMaxLength4000()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("SelectedModels");

        // Assert
        property.Should().NotBeNull();
        property!.GetMaxLength().Should().Be(4000);
    }

    [Fact]
    public async Task Provider_IsEnabled_ShouldHaveDefaultValueTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("IsEnabled");

        // Assert
        property.Should().NotBeNull();
        property!.GetDefaultValue().Should().Be(true);
    }

    [Fact]
    public async Task Provider_DateCreated_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("DateCreated");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeFalse();
    }

    [Fact]
    public async Task Provider_DateModified_ShouldBeNullable()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AzureOpsCrewContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // Act
        using var context = new AzureOpsCrewContext(options);
        var property = context.Model.FindEntityType(typeof(Provider))!.FindProperty("DateModified");

        // Assert
        property.Should().NotBeNull();
        property!.IsNullable.Should().BeTrue();
    }
}
