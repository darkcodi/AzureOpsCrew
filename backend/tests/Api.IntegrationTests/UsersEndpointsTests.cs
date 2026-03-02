using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

[Collection("Shared")]
public class UsersEndpointsTests
{
    private readonly AocWebApplicationFactory _factory;

    public UsersEndpointsTests(AocWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListUsers_WithAuth_ReturnsDemoUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await response.Content.ReadFromJsonAsync<JsonElement>();
        users.GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var hasDemoUser = users.EnumerateArray()
            .Any(u => u.GetProperty("id").GetInt32() > 0);
        hasDemoUser.Should().BeTrue();
    }

    [Fact]
    public async Task ListUsers_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListUsers_ReturnsDemoUserAsOnline()
    {
        // Arrange — auto-login updates LastLoginAt, so user should appear online
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/users");
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<JsonElement>();
        var demo = users.EnumerateArray()
            .FirstOrDefault(u => u.GetProperty("isCurrentUser").GetBoolean());

        // Assert
        demo.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        demo.GetProperty("isOnline").GetBoolean().Should().BeTrue(
            "current user just logged in so should appear online");
    }
}
