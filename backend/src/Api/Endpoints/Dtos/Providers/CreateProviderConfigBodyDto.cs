using System.ComponentModel.DataAnnotations;
using AzureOpsCrew.Domain.Providers;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Providers;

public record CreateProviderConfigBodyDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "ClientId is required.")]
    public int ClientId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "ProviderType is required.")]
    public ProviderType ProviderType { get; set; }

    [StringLength(500, ErrorMessage = "ApiKey must be at most 500 characters.")]
    public string? ApiKey { get; set; }

    [Required(ErrorMessage = "ApiEndpoint is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "ApiEndpoint must be between 1 and 500 characters.")]
    public string ApiEndpoint { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "DefaultModel must be at most 200 characters.")]
    public string? DefaultModel { get; set; }

    [StringLength(4000, ErrorMessage = "SelectedModels must be at most 4000 characters.")]
    public string? SelectedModels { get; set; }

    public bool IsEnabled { get; set; } = true;
}
