using System.ComponentModel.DataAnnotations;
using AzureOpsCrew.Domain.McpServerConfigurations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record AuthHeaderBodyDto
{
    [StringLength(200, ErrorMessage = "Name must be at most 200 characters.")]
    public string? Name { get; set; }

    [StringLength(4000, ErrorMessage = "Value must be at most 4000 characters.")]
    public string? Value { get; set; }

    public AuthHeader ToDomainAuthHeader()
    {
        return new AuthHeader(Name!.Trim(), Value!.Trim());
    }

    public IEnumerable<ValidationResult> ValidateWithPrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return CreateValidationResult(prefix, nameof(Name), "Name is required.");
        }

        if (string.IsNullOrWhiteSpace(Value))
        {
            yield return CreateValidationResult(prefix, nameof(Value), "Value is required.");
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
