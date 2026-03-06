using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record UpdateMcpServerConfigurationBodyDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "Description must be at most 4000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Url is required.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Url must be between 1 and 1000 characters.")]
    [Url(ErrorMessage = "Url must be a valid absolute URL.")]
    public string Url { get; set; } = string.Empty;
}