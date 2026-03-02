using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AzureOpsCrew.Api.IntegrationTests;

[Collection("Shared")]
public class AuthEndpointsTests
{
    private readonly AocWebApplicationFactory _factory;

    public AuthEndpointsTests(AocWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AutoLogin_ReturnsJwtToken_And_DemoUser()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/auth/auto-login", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("expiresAtUtc").GetString().Should().NotBeNullOrWhiteSpace();

        var user = json.GetProperty("user");
        user.GetProperty("id").GetInt32().Should().BeGreaterThan(0);
        user.GetProperty("email").GetString().Should().Be("demo@azureopscrew.dev");
        user.GetProperty("displayName").GetString().Should().Be("Demo User");
    }

    [Fact]
    public async Task AutoLogin_CalledTwice_ReturnsSameUser()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response1 = await client.PostAsync("/api/auth/auto-login", null);
        var json1 = await response1.Content.ReadFromJsonAsync<JsonElement>();

        var response2 = await client.PostAsync("/api/auth/auto-login", null);
        var json2 = await response2.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        var userId1 = json1.GetProperty("user").GetProperty("id").GetInt32();
        var userId2 = json2.GetProperty("user").GetProperty("id").GetInt32();
        userId1.Should().Be(userId2);
    }

    [Fact]
    public async Task Me_WithoutAuth_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsDemoUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync();

        // Act
        var response = await client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("email").GetString().Should().Be("demo@azureopscrew.dev");
        json.GetProperty("displayName").GetString().Should().Be("Demo User");
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsToken()
    {
        // Arrange
        var client = _factory.CreateClient();
        // First ensure demo user exists via auto-login
        await client.PostAsync("/api/auth/auto-login", null);

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "demo@azureopscrew.dev",
            password = "AzureOpsCrew2025!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        await client.PostAsync("/api/auth/auto-login", null); // ensure user exists

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "demo@azureopscrew.dev",
            password = "WrongPassword123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonexistentUser_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "nobody@example.com",
            password = "SomePassword123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
