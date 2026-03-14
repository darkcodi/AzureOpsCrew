using AzureOpsCrew.Domain.Providers;
using FluentAssertions;

namespace Domain.Tests.Providers;

public class ProviderTests
{
    [Fact]
    public void Constructor_ShouldInitializeAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "TestProvider";
        var providerType = ProviderType.OpenAI;
        var apiKey = "test-key";
        var apiEndpoint = "https://api.example.com";
        var defaultModel = "gpt-4";
        var isEnabled = true;
        var selectedModels = "model1,model2";

        // Act
        var provider = new Provider(
            id,
            name,
            providerType,
            apiKey,
            apiEndpoint,
            defaultModel,
            isEnabled,
            selectedModels);

        // Assert
        provider.Id.Should().Be(id);
        provider.Name.Should().Be(name);
        provider.ProviderType.Should().Be(providerType);
        provider.ApiKey.Should().Be(apiKey);
        provider.ApiEndpoint.Should().Be(apiEndpoint);
        provider.DefaultModel.Should().Be(defaultModel);
        provider.IsEnabled.Should().Be(isEnabled);
        provider.SelectedModels.Should().Be(selectedModels);
        provider.ModelsCount.Should().Be(0); // Default value
        provider.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        provider.DateModified.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithOptionalParameters_ShouldUseDefaults()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "TestProvider";
        var providerType = ProviderType.OpenAI;

        // Act
        var provider = new Provider(id, name, providerType, null, null, null, true, null);

        // Assert
        provider.ApiKey.Should().BeNull();
        provider.ApiEndpoint.Should().BeNull();
        provider.DefaultModel.Should().BeNull();
        provider.IsEnabled.Should().BeTrue();
        provider.SelectedModels.Should().BeNull();
    }

    [Theory]
    [InlineData(ProviderType.OpenAI)]
    [InlineData(ProviderType.Anthropic)]
    [InlineData(ProviderType.AzureFoundry)]
    [InlineData(ProviderType.Ollama)]
    [InlineData(ProviderType.OpenRouter)]
    public void Constructor_ShouldAcceptAllProviderTypes(ProviderType providerType)
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "TestProvider";

        // Act
        var provider = new Provider(id, name, providerType, null);

        // Assert
        provider.ProviderType.Should().Be(providerType);
    }

    [Fact]
    public void Update_ShouldUpdateAllProperties()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "OldName",
            ProviderType.OpenAI,
            "old-key",
            "https://old-api.example.com",
            "gpt-3.5",
            true,
            "old-model1,old-model2");

        // Act
        provider.Update(
            name: "NewName",
            apiKey: "new-key",
            apiEndpoint: "https://new-api.example.com",
            defaultModel: "gpt-4",
            isEnabled: false,
            selectedModels: "new-model1,new-model2");

        // Assert
        provider.Name.Should().Be("NewName");
        provider.ApiKey.Should().Be("new-key");
        provider.ApiEndpoint.Should().Be("https://new-api.example.com");
        provider.DefaultModel.Should().Be("gpt-4");
        provider.IsEnabled.Should().BeFalse();
        provider.SelectedModels.Should().Be("new-model1,new-model2");
        provider.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Update_WithPartialParameters_ShouldUpdateOnlyProvidedProperties()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "OldName",
            ProviderType.OpenAI,
            "old-key",
            "https://old-api.example.com",
            "gpt-3.5",
            true,
            "old-model1,old-model2");

        // Act - Update only name and API key
        provider.Update("NewName", "new-key", null, null, null, null);

        // Assert
        provider.Name.Should().Be("NewName");
        provider.ApiKey.Should().Be("new-key");
        // Note: Update method sets values directly, so null becomes null (doesn't preserve old values)
        provider.ApiEndpoint.Should().BeNull();
        provider.DefaultModel.Should().BeNull();
        provider.IsEnabled.Should().BeTrue(); // Unchanged (isEnabled is optional)
        provider.SelectedModels.Should().BeNull();
    }

    [Fact]
    public void Update_WithoutIsEnabled_ShouldNotChangeIsEnabled()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key",
            null,
            null,
            true,
            null);

        // Act
        provider.Update("NewName", null, null, null, null, null);

        // Assert
        provider.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Update_WithIsEnabledFalse_ShouldDisableProvider()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key",
            null,
            null,
            true,
            null);

        // Act
        provider.Update("NewName", null, null, null, false, null);

        // Assert
        provider.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Update_WithIsEnabledTrue_ShouldEnableProvider()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key",
            null,
            null,
            false,
            null);

        // Act
        provider.Update("NewName", null, null, null, true, null);

        // Assert
        provider.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SetEnabled_ToTrue_ShouldEnableProvider()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key",
            null,
            null,
            false,
            null);

        // Act
        provider.SetEnabled(true);

        // Assert
        provider.IsEnabled.Should().BeTrue();
        provider.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SetEnabled_ToFalse_ShouldDisableProvider()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key",
            null,
            null,
            true,
            null);

        // Act
        provider.SetEnabled(false);

        // Assert
        provider.IsEnabled.Should().BeFalse();
        provider.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SetModelsCount_ShouldUpdateModelsCount()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key");

        // Act
        provider.SetModelsCount(5);

        // Assert
        provider.ModelsCount.Should().Be(5);
    }

    [Fact]
    public void SetModelsCount_ShouldAllowZero()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key");
        provider.SetModelsCount(10);

        // Act
        provider.SetModelsCount(0);

        // Assert
        provider.ModelsCount.Should().Be(0);
    }

    [Fact]
    public void SetModelsCount_ShouldUpdateMultipleTimes()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key");

        // Act
        provider.SetModelsCount(3);
        provider.SetModelsCount(7);
        provider.SetModelsCount(15);

        // Assert
        provider.ModelsCount.Should().Be(15);
    }

    [Fact]
    public void DateModified_ShouldBeNull_Initially()
    {
        // Arrange & Act
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key");

        // Assert
        provider.DateModified.Should().BeNull();
    }

    [Fact]
    public void DateModified_ShouldBeSet_AfterUpdate()
    {
        // Arrange
        var provider = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.OpenAI,
            "test-key");

        // Act
        provider.Update("NewName", null, null, null, null, null);

        // Assert
        provider.DateModified.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
