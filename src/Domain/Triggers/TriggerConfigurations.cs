namespace AzureOpsCrew.Domain.Triggers;

public record ScheduledTriggerConfig(string CronExpression);

public record WebhookTriggerConfig(string WebhookToken, string? Secret);

public record EventTriggerConfig(string EventType, Guid? SourceAgentId);
