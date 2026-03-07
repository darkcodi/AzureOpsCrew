using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record SetMcpServerToolEnabledBodyDto
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
}
