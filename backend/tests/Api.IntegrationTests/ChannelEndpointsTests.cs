using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

[Collection("Shared")]
public class ChannelEndpointsTests
{
    private readonly AocWebApplicationFactory _factory;

    public ChannelEndpointsTests(AocWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListChannels_WithAuth_ReturnsSeededOpsRoom()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/channels");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var channels = await response.Content.ReadFromJsonAsync<JsonElement>();
        channels.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var names = channels.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();
        names.Should().Contain("Ops Room");
    }

    [Fact]
    public async Task ListChannels_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/channels");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpsRoom_Has3Agents()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/channels");
        response.EnsureSuccessStatusCode();

        var channels = await response.Content.ReadFromJsonAsync<JsonElement>();
        var opsRoom = channels.EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("name").GetString() == "Ops Room");

        // Assert
        opsRoom.ValueKind.Should().NotBe(JsonValueKind.Undefined, "Ops Room channel should exist");

        var agentIds = opsRoom.GetProperty("agentIds");
        agentIds.GetArrayLength().Should().Be(3, "Ops Room should have 3 agents");
    }

    [Fact]
    public async Task CreateChannel_CreatesNewChannel()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();
        var channelName = $"Test Channel {Guid.NewGuid():N}";

        // Act
        var response = await client.PostAsJsonAsync("/api/channels/create", new
        {
            name = channelName,
            agentIds = Array.Empty<string>()
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("channelId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateChannel_ThenListContainsIt()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();
        var channelName = $"TestChan_{Guid.NewGuid():N}";

        // Act - Create
        var createResponse = await client.PostAsJsonAsync("/api/channels/create", new
        {
            name = channelName,
            agentIds = Array.Empty<string>()
        });
        createResponse.EnsureSuccessStatusCode();

        // Act - List
        var listResponse = await client.GetAsync("/api/channels");
        listResponse.EnsureSuccessStatusCode();

        var channels = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var names = channels.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        // Assert
        names.Should().Contain(channelName);
    }

    [Fact]
    public async Task AddAgent_ToChannel_UpdatesAgentIds()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Create a new empty channel
        var createResponse = await client.PostAsJsonAsync("/api/channels/create", new
        {
            name = $"AddAgentTest_{Guid.NewGuid():N}",
            agentIds = Array.Empty<string>()
        });
        createResponse.EnsureSuccessStatusCode();
        var channel = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var channelId = channel.GetProperty("channelId").GetString()!;

        // Get a seeded agent ID
        var agentsResponse = await client.GetAsync("/api/agents");
        agentsResponse.EnsureSuccessStatusCode();
        var agents = await agentsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var agentId = agents.EnumerateArray().First().GetProperty("id").GetString()!;

        // Act
        var addResponse = await client.PostAsJsonAsync($"/api/channels/{channelId}/add-agent", new
        {
            agentId
        });

        // Assert
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify agent is in channel
        var getResponse = await client.GetAsync("/api/channels");
        getResponse.EnsureSuccessStatusCode();
        var updatedChannels = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var updatedChannel = updatedChannels.EnumerateArray()
            .First(c => c.GetProperty("id").GetString() == channelId);

        var agentIds = updatedChannel.GetProperty("agentIds").EnumerateArray()
            .Select(a => a.GetString())
            .ToList();
        agentIds.Should().Contain(agentId);
    }

    [Fact]
    public async Task RemoveAgent_FromChannel_UpdatesAgentIds()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Ops Room has agents - get it
        var listResponse = await client.GetAsync("/api/channels");
        listResponse.EnsureSuccessStatusCode();
        var channels = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var opsRoom = channels.EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == "Ops Room");
        var channelId = opsRoom.GetProperty("id").GetString()!;
        var agentId = opsRoom.GetProperty("agentIds").EnumerateArray().First().GetString()!;

        // Act
        var removeResponse = await client.PostAsJsonAsync($"/api/channels/{channelId}/remove-agent", new
        {
            agentId
        });

        // Assert
        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify agent was removed
        var getResponse = await client.GetAsync("/api/channels");
        getResponse.EnsureSuccessStatusCode();
        var updated = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var updatedOps = updated.EnumerateArray()
            .First(c => c.GetProperty("id").GetString() == channelId);
        var remaining = updatedOps.GetProperty("agentIds").EnumerateArray()
            .Select(a => a.GetString())
            .ToList();
        remaining.Should().NotContain(agentId);
    }

    [Fact]
    public async Task DeleteChannel_RemovesIt()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Create a channel to delete
        var createResponse = await client.PostAsJsonAsync("/api/channels/create", new
        {
            name = $"DeleteMe_{Guid.NewGuid():N}",
            agentIds = Array.Empty<string>()
        });
        createResponse.EnsureSuccessStatusCode();
        var channel = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var channelId = channel.GetProperty("channelId").GetString()!;

        // Act
        var deleteResponse = await client.DeleteAsync($"/api/channels/{channelId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await client.GetAsync("/api/channels");
        getResponse.EnsureSuccessStatusCode();
        var channels = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var ids = channels.EnumerateArray()
            .Select(c => c.GetProperty("id").GetString())
            .ToList();
        ids.Should().NotContain(channelId);
    }
}
