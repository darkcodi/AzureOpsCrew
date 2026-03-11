using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Triggers;

public static class TriggerMappings
{
    public static TriggerResponseDto ToResponseDto(this AgentTrigger trigger)
    {
        return new TriggerResponseDto
        {
            Id = trigger.Id,
            AgentId = trigger.AgentId,
            ChatId = trigger.ChatId,
            TriggerType = trigger.TriggerType.ToString(),
            ConfigurationJson = trigger.ConfigurationJson,
            IsEnabled = trigger.IsEnabled,
            LastFiredAt = trigger.LastFiredAt,
            CreatedAt = trigger.CreatedAt,
            UpdatedAt = trigger.UpdatedAt,
        };
    }
}
