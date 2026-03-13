using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Ai.ProviderFacades;
using FluentAssertions;

namespace Infrastructure.Ai.Tests.ProviderFacades;

public class AnthropicProviderFacadeTests
{
    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var facade = new AnthropicProviderFacade(httpClient);

        // Assert
        facade.Should().NotBeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_WithNullApiKey_ShouldReturnValidationFailed()
    {
        // Arrange
        var httpClient = new HttpClient();
        var facade = new AnthropicProviderFacade(httpClient);
        var config = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.Anthropic,
            null); // Null API key

        // Act
        var result = await facade.TestConnectionAsync(config, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorDetails.Should().Be("API key is required");
    }

    [Fact]
    public async Task TestConnectionAsync_WithEmptyApiKey_ShouldReturnValidationFailed()
    {
        // Arrange
        var httpClient = new HttpClient();
        var facade = new AnthropicProviderFacade(httpClient);
        var config = new Provider(
            Guid.NewGuid(),
            "TestProvider",
            ProviderType.Anthropic,
            string.Empty);

        // Act
        var result = await facade.TestConnectionAsync(config, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorDetails.Should().Be("API key is required");
    }

    [Fact]
    public void TestConnectionResult_ShouldHaveExpectedProperties()
    {
        // Arrange & Act
        var successResult = TestConnectionResult.Successful(100, []);

        // Assert
        successResult.Success.Should().BeTrue();
        successResult.LatencyMs.Should().Be(100);
        successResult.AvailableModels.Should().BeEmpty();
    }

    [Fact]
    public void Provider_ShouldHaveAnthropicType()
    {
        // Arrange & Act
        var provider = new Provider(
            Guid.NewGuid(),
            "Anthropic",
            ProviderType.Anthropic,
            "sk-test-key");

        // Assert
        provider.ProviderType.Should().Be(ProviderType.Anthropic);
    }

    [Theory]
    [InlineData(ProviderType.Anthropic)]
    [InlineData(ProviderType.AzureFoundry)]
    [InlineData(ProviderType.Ollama)]
    [InlineData(ProviderType.OpenRouter)]
    public void ProviderType_ShouldIncludeAllTypes(ProviderType providerType)
    {
        // Arrange & Act
        var provider = new Provider(
            Guid.NewGuid(),
            "Test",
            providerType,
            "test-key");

        // Assert
        provider.ProviderType.Should().Be(providerType);
    }

    [Fact]
    public void ParseModels_ShouldHandleEmptyArray()
    {
        // Arrange
        var json = """{"data": []}""";
        var doc = JsonDocument.Parse(json);
        var modelsElement = doc.RootElement.GetProperty("data");

        // Act
        var models = modelsElement.EnumerateArray().ToList();

        // Assert
        models.Should().BeEmpty();
    }
}
