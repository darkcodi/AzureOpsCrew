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

    [Required]
    [StringLength(30, MinimumLength = 2)]
    [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Username must contain only letters and numbers.")]
    public string Username { get; set; } = string.Empty;
}
