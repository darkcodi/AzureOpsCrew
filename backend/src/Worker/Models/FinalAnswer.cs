namespace Worker.Models;

public class FinalAnswer
{
    public string Text { get; set; } = string.Empty;
    public long? InputTokenCount { get; set; }
    public long? OutputTokenCount { get; set; }
    public long? TotalTokenCount { get; set; }
    public long? CachedInputTokenCount { get; set; }
    public long? ReasoningTokenCount { get; set; }
}
