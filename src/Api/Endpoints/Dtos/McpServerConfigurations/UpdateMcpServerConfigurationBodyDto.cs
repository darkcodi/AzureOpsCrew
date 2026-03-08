using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AzureOpsCrew.Domain.McpServerConfigurations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record UpdateMcpServerConfigurationBodyDto : IValidatableObject
{
    public UpdateMcpServerConfigurationBodyDto()
    {
        Headers = [];
    }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "Description must be at most 4000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Url is required.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Url must be between 1 and 1000 characters.")]
    [Url(ErrorMessage = "Url must be a valid absolute URL.")]
    public string Url { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public McpServerConfigurationAuthType AuthType { get; set; } = McpServerConfigurationAuthType.None;

    [StringLength(4000, ErrorMessage = "BearerToken must be at most 4000 characters.")]
    public string? BearerToken { get; set; }

    public AuthHeaderBodyDto[] Headers { get; set; }

    public McpServerConfigurationAuth ToDomainAuth()
    {
        var headers = Headers ?? [];
        return new McpServerConfigurationAuth(
            AuthType,
            AuthType == McpServerConfigurationAuthType.BearerToken ? BearerToken?.Trim() : null,
            AuthType == McpServerConfigurationAuthType.CustomHeaders
                ? headers.Select(x => x.ToDomainAuthHeader()).ToList()
                : []);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return ValidateWithPrefix(null);
    }

    public IEnumerable<ValidationResult> ValidateWithPrefix(string? prefix)
    {
        // On update, null/empty values preserve existing auth (no validation required)
        if (AuthType != McpServerConfigurationAuthType.CustomHeaders)
            yield break;
        var headers = Headers ?? [];
        if (headers.Length == 0)
            yield break;
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