namespace AzureOpsCrew.Domain.LLMOutputs;

public class LlmOutput
{
    private LlmOutput() { }

    public LlmOutput(Guid id, Guid runId, string text, string? toolCall, long? inputTokens, long? outputTokens)
    {
        Id = id;
        RunId = runId;
        Text = text;
        ToolCall = toolCall;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
    }

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public string? ToolCall { get; private set; }
    public long? InputTokens { get; private set; }
    public long? OutputTokens { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
