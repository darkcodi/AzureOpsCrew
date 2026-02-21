using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Auth;

public sealed class RegisterRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(320, MinimumLength = 3)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [StringLength(120, MinimumLength = 2)]
    public string? DisplayName { get; set; }
}
