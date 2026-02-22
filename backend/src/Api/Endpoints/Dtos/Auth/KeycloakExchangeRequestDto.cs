using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Auth;

public sealed class KeycloakExchangeRequestDto
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
