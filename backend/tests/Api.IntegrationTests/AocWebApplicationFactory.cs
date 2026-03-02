using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AzureOpsCrew.Api.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory that overrides configuration for tests.
/// Uses an isolated in-memory SQLite database per test fixture,
/// a deterministic JWT signing key, and enables seeding.
/// </summary>
public class AocWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>A strong signing key used exclusively in tests.</summary>
    public const string TestSigningKey = "TestSigningKeyForIntegrationTests_2025_AtLeast32Chars!";
    public const string TestIssuer = "AzureOpsCrew";
    public const string TestAudience = "AzureOpsCrewFrontend";

    private readonly string _dbName = $"test_{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Program.cs uses .AddEnvironmentVariables() as the last config source,
        // so we set env vars that will override appsettings.json.
        // .NET config maps "Jwt__SigningKey" → "Jwt:SigningKey" automatically.
        Environment.SetEnvironmentVariable("DatabaseProvider", "Sqlite");
        Environment.SetEnvironmentVariable("Sqlite__DataSource", $"Data Source={_dbName}");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestAudience);
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "480");
        Environment.SetEnvironmentVariable("OpenAI__ApiKey", "sk-test-fake-key-for-tests");
        Environment.SetEnvironmentVariable("Seeding__IsEnabled", "true");
        Environment.SetEnvironmentVariable("LongTermMemory__Type", "InMemory");
        Environment.SetEnvironmentVariable("Mcp__Azure__ServerUrl", "");
        Environment.SetEnvironmentVariable("Mcp__AzureDevOps__ServerUrl", "");
    }

    /// <summary>
    /// Performs auto-login against the API and returns an HttpClient
    /// with the JWT Bearer token pre-configured in the Authorization header.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsync("/api/auth/auto-login", null);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = json.GetProperty("accessToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Clean up test database file
        if (File.Exists(_dbName))
        {
            try { File.Delete(_dbName); } catch { /* best-effort cleanup */ }
        }
    }
}
