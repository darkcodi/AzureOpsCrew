#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Triggers;

public class AgentTrigger
{
    private AgentTrigger()
    {
    }

    public AgentTrigger(Guid id, Guid agentId, Guid chatId, TriggerType triggerType, string? configurationJson = null)
    {
        Id = id;
        AgentId = agentId;
        ChatId = chatId;
        TriggerType = triggerType;
        ConfigurationJson = configurationJson;
        IsEnabled = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public Guid ChatId { get; private set; }
    public TriggerType TriggerType { get; private set; }
    public string? ConfigurationJson { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime? LastFiredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateConfiguration(string? configurationJson)
    {
        ConfigurationJson = configurationJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFired()
    {
        LastFiredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
