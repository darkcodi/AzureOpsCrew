namespace AzureOpsCrew.Domain.McpServerConfigurations;

public sealed record McpServerConfigurationAuth(
    McpServerConfigurationAuthType Type,
    string? BearerToken = null,
    string? ApiKey = null,
    string? ApiKeyHeaderName = null);