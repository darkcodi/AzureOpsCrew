using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AzureOpsCrew.Domain.McpServerConfigurations;
namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;
public record SetAuthMcpServerConfigurationBodyDto : IValidatableObject
{
    public SetAuthMcpServerConfigurationBodyDto()
    {
        Headers = [];
    }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public McpServerConfigurationAuthType Type { get; set; } = McpServerConfigurationAuthType.None;
    [StringLength(4000, ErrorMessage = "BearerToken must be at most 4000 characters.")]
    public string? BearerToken { get; set; }
    public AuthHeaderBodyDto[] Headers { get; set; }
    public McpServerConfigurationAuth ToDomainAuth()
    {
        var headers = Headers ?? [];
        return new McpServerConfigurationAuth(
            Type,
            Type == McpServerConfigurationAuthType.BearerToken ? BearerToken?.Trim() : null,
            Type == McpServerConfigurationAuthType.CustomHeaders
                ? headers.Select(x => x.ToDomainAuthHeader()).ToList()
                : []);
    }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return ValidateWithPrefix(null);
    }
    public IEnumerable<ValidationResult> ValidateWithPrefix(string? prefix)
    {
        var headers = Headers ?? [];
        if (Type == McpServerConfigurationAuthType.BearerToken && string.IsNullOrWhiteSpace(BearerToken))
        {
            yield return CreateValidationResult(prefix, nameof(BearerToken), "BearerToken is required when auth type is BearerToken.");
        }
        if (Type != McpServerConfigurationAuthType.CustomHeaders)
            yield break;
        if (headers.Length == 0)
        {
            yield return CreateValidationResult(prefix, nameof(Headers), "At least one header is required when auth type is CustomHeaders.");
            yield break;
        }
        var seenHeaderNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i];
            var headerPrefix = string.IsNullOrWhiteSpace(prefix)
                ? $"{nameof(Headers)}[{i}]"
                : $"{prefix}.{nameof(Headers)}[{i}]";
            foreach (var validationResult in header.ValidateWithPrefix(headerPrefix))
                yield return validationResult;
            if (string.IsNullOrWhiteSpace(header.Name))
                continue;
            var headerName = header.Name.Trim();
            if (string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase))
            {
                yield return CreateValidationResult(prefix, $"{nameof(Headers)}[{i}].{nameof(AuthHeaderBodyDto.Name)}", "Authorization header is not allowed in CustomHeaders. Use BearerToken auth instead.");
            }
            if (!seenHeaderNames.Add(headerName))
            {
                yield return CreateValidationResult(prefix, $"{nameof(Headers)}[{i}].{nameof(AuthHeaderBodyDto.Name)}", "Header names must be unique.");
            }
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

