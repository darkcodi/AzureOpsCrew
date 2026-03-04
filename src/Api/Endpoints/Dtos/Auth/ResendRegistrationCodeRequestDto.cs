using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Auth;

public sealed class ResendRegistrationCodeRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(320, MinimumLength = 3)]
    public string Email { get; set; } = string.Empty;
}
