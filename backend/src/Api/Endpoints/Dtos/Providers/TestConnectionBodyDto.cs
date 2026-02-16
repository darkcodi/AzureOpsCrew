using System.ComponentModel.DataAnnotations;
using AzureOpsCrew.Domain.Providers;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Providers;

public record TestConnectionBodyDto
{
    [Required(ErrorMessage = "ProviderType is required.")]
    public ProviderType ProviderType { get; set; }

    // Optional for providers that don't use a key (e.g. Ollama)
    [StringLength(500)]
    public string ApiKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "ApiEndpoint is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "ApiEndpoint must be between 1 and 500 characters.")]
    public string ApiEndpoint { get; set; } = string.Empty;

    [StringLength(200)]
    public string? DefaultModel { get; set; }

    [StringLength(200)]
    public string? Name { get; set; }
}
