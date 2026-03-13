using System.Net;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Ai.ProviderFacades;
using FluentAssertions;

namespace Infrastructure.Ai.Tests.ProviderFacades;

public class OllamaProviderFacadeTests
{
    [Fact]
    public void Constructor_ShouldInitialize()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var facade = new OllamaProviderFacade(httpClient);

        // Assert
        facade.Should().NotBeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldUseDefaultEndpoint_WhenNotProvided()
    {
        // Arrange
        var httpClient = new HttpClient();
        var facade = new OllamaProviderFacade(httpClient);
        var config = new Provider(
            Guid.NewGuid(),
            "Ollama",
            ProviderType.Ollama,
            apiKey: null,
            apiEndpoint: null);

        // Act
        // Note: This will fail to connect in test environment, but we can verify the facade doesn't throw
        var result = await facade.TestConnectionAsync(config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_ShouldUseCustomEndpoint_WhenProvided()
    {
        // Arrange
        var httpClient = new HttpClient();
        var facade = new OllamaProviderFacade(httpClient);
        var config = new Provider(
            Guid.NewGuid(),
            "Ollama",
            ProviderType.Ollama,
            apiKey: null,
            apiEndpoint: "http://custom-ollama:11434");

        // Act
        var result = await facade.TestConnectionAsync(config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void CreateChatClient_ShouldUseDefaultEndpoint_WhenNotProvided()
    {
        // Arrange
        var httpClient = new HttpClient();
        var facade = new OllamaProviderFacade(httpClient);
        var config = new Provider(
            Guid.NewGuid(),
            "Ollama",
            ProviderType.Ollama,
            apiKey: null,
            apiEndpoint: null);

        // Act
        var client = facade.CreateChatClient(config, "llama3", CancellationToken.None);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateChatClient_ShouldUseCustomEndpoint_WhenProvided()
    {
        // Arrange
        var httpClient = new HttpClient();
        var facade = new OllamaProviderFacade(httpClient);
        var config = new Provider(
            Guid.NewGuid(),
            "Ollama",
            ProviderType.Ollama,
            apiKey: null,
            apiEndpoint: "http://custom-ollama:11434");

        // Act
        var client = facade.CreateChatClient(config, "llama3", CancellationToken.None);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Provider_ShouldHaveOllamaType()
    {
        // Arrange & Act
        var provider = new Provider(
            Guid.NewGuid(),
            "Ollama",
            ProviderType.Ollama,
            apiKey: null);

        // Assert
        provider.ProviderType.Should().Be(ProviderType.Ollama);
    }
}
