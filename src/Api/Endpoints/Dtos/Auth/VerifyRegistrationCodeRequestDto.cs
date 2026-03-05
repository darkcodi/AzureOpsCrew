using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Auth;

public sealed class VerifyRegistrationCodeRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(320, MinimumLength = 3)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{4,8}$")]
    public string Code { get; set; } = string.Empty;
}
