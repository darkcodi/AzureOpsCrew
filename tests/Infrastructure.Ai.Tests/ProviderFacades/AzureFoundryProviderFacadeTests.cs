using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Ai.ProviderFacades;
using FluentAssertions;

namespace Infrastructure.Ai.Tests.ProviderFacades;

public class AzureFoundryProviderFacadeTests
{
    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var facade = new AzureFoundryProviderFacade(httpClient);

        // Assert
        facade.Should().NotBeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_WithNullApiKey_ShouldReturnValidationFailed()
    {
        // Arrange
        var httpClient = new HttpClient();
        var facade = new AzureFoundryProviderFacade(httpClient);
        var config = new Provider(
            Guid.NewGuid(),
            "AzureFoundry",
            ProviderType.AzureFoundry,
            null); // Null API key

        // Act
        var result = await facade.TestConnectionAsync(config, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorDetails.Should().Be("API key is required");
    }

    [Fact]
    public void Provider_ShouldHaveAzureFoundryType()
    {
        // Arrange & Act
        var provider = new Provider(
            Guid.NewGuid(),
            "AzureFoundry",
            ProviderType.AzureFoundry,
            "test-key");

        // Assert
        provider.ProviderType.Should().Be(ProviderType.AzureFoundry);
    }

    [Fact]
    public void CreateChatClient_ShouldReturnClient()
    {
        // Arrange
        var httpClient = new HttpClient();
        var facade = new AzureFoundryProviderFacade(httpClient);
        var config = new Provider(
            Guid.NewGuid(),
            "AzureFoundry",
            ProviderType.AzureFoundry,
            "test-key",
            "https://test.openai.azure.com");

        // Act
        var client = facade.CreateChatClient(config, "gpt-4", CancellationToken.None);

        // Assert
        client.Should().NotBeNull();
    }
}
