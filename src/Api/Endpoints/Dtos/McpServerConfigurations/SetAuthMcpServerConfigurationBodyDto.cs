using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AzureOpsCrew.Domain.McpServerConfigurations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record SetAuthMcpServerConfigurationBodyDto : IValidatableObject
{
    private const string DefaultApiKeyHeaderName = "X-API-Key";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public McpServerConfigurationAuthType Type { get; set; } = McpServerConfigurationAuthType.None;

    [StringLength(4000, ErrorMessage = "BearerToken must be at most 4000 characters.")]
    public string? BearerToken { get; set; }

    [StringLength(4000, ErrorMessage = "ApiKey must be at most 4000 characters.")]
    public string? ApiKey { get; set; }

    [StringLength(200, ErrorMessage = "ApiKeyHeaderName must be at most 200 characters.")]
    public string? ApiKeyHeaderName { get; set; }

    public McpServerConfigurationAuth ToDomainAuth()
    {
        return new McpServerConfigurationAuth(
            Type,
            Type == McpServerConfigurationAuthType.BearerToken ? BearerToken?.Trim() : null,
            Type == McpServerConfigurationAuthType.ApiKey ? ApiKey?.Trim() : null,
            Type == McpServerConfigurationAuthType.ApiKey
                ? string.IsNullOrWhiteSpace(ApiKeyHeaderName)
                    ? DefaultApiKeyHeaderName
                    : ApiKeyHeaderName.Trim()
                : null);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return ValidateWithPrefix(null);
    }

    public IEnumerable<ValidationResult> ValidateWithPrefix(string? prefix)
    {
        if (Type == McpServerConfigurationAuthType.BearerToken && string.IsNullOrWhiteSpace(BearerToken))
        {
            yield return CreateValidationResult(prefix, nameof(BearerToken), "BearerToken is required when auth type is BearerToken.");
        }

        if (Type == McpServerConfigurationAuthType.ApiKey && string.IsNullOrWhiteSpace(ApiKey))
        {
            yield return CreateValidationResult(prefix, nameof(ApiKey), "ApiKey is required when auth type is ApiKey.");
        }
    }

    private static ValidationResult CreateValidationResult(string? prefix, string memberName, string errorMessage)
    {
        var fullMemberName = string.IsNullOrWhiteSpace(prefix)
            ? memberName
            : $"{prefix}.{memberName}";

        return new ValidationResult(errorMessage, [fullMemberName]);
    }
}