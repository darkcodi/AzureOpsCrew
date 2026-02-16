using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Providers;

public record UpdateProviderConfigBodyDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "ApiKey is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "ApiKey must be between 1 and 500 characters.")]
    public string ApiKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "ApiEndpoint is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "ApiEndpoint must be between 1 and 500 characters.")]
    public string ApiEndpoint { get; set; } = string.Empty;

    [Required(ErrorMessage = "DefaultModel is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "DefaultModel must be between 1 and 200 characters.")]
    public string DefaultModel { get; set; } = string.Empty;

    [Required(ErrorMessage = "IsEnabled is required.")]
    public bool IsEnabled { get; set; }
}
