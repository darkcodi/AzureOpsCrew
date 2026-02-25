namespace Worker.Models.Content;

public sealed class AocUsageContent : AocAiContent
{
    public long? InputTokenCount { get; set; }
    public long? OutputTokenCount { get; set; }
    public long? TotalTokenCount { get; set; }
    public long? CachedInputTokenCount { get; set; }
    public long? ReasoningTokenCount { get; set; }
    public Dictionary<string, long>? AdditionalCounts { get; set; }
}
