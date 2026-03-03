using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

[Collection("Shared")]
public class AgentEndpointsTests
{
    private readonly AocWebApplicationFactory _factory;

    public AgentEndpointsTests(AocWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListAgents_WithAuth_ReturnsSeeded3Agents()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var agents = await response.Content.ReadFromJsonAsync<JsonElement>();
        agents.GetArrayLength().Should().BeGreaterThanOrEqualTo(3,
            "seeder creates 3 agents: Manager, Azure DevOps, Azure Dev");
    }

    [Fact]
    public async Task ListAgents_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/agents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListAgents_ContainsExpectedAgentNames()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/agents");
        response.EnsureSuccessStatusCode();

        var agents = await response.Content.ReadFromJsonAsync<JsonElement>();
        var names = agents.EnumerateArray()
            .Select(a => a.GetProperty("info").GetProperty("name").GetString())
            .ToList();

        // Assert
        names.Should().Contain("Manager");
        names.Should().Contain("DevOps");
        names.Should().Contain("Developer");
    }

    [Fact]
    public async Task SeededAgents_HaveCorrectModel()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/agents");
        response.EnsureSuccessStatusCode();

        var agents = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert - all seeded agents should use gpt-4o-mini
        foreach (var agent in agents.EnumerateArray())
        {
            agent.GetProperty("info").GetProperty("model").GetString().Should().Be("gpt-4o-mini");
        }
    }

    [Fact]
    public async Task SeededAgents_HaveSystemPrompts()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/agents");
        response.EnsureSuccessStatusCode();

        var agents = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        foreach (var agent in agents.EnumerateArray())
        {
            agent.GetProperty("info").GetProperty("prompt").GetString().Should().NotBeNullOrWhiteSpace(
                $"agent {agent.GetProperty("info").GetProperty("name").GetString()} should have a system prompt");
        }
    }
}
