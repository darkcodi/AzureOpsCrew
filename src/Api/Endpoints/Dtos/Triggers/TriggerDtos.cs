using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Triggers;

public class CreateTriggerDto
{
    [Required]
    public Guid AgentId { get; set; }

    [Required]
    public Guid ChatId { get; set; }

    [Required]
    public string TriggerType { get; set; } = string.Empty;

    public string? ConfigurationJson { get; set; }
}

public class UpdateTriggerDto
{
    public string? ConfigurationJson { get; set; }
}

public class SetTriggerEnabledDto
{
    public bool IsEnabled { get; set; }
}

public class TriggerResponseDto
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string? ConfigurationJson { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? LastFiredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
