namespace AzureOpsCrew.Domain.Agents;

public class PersistedAgentSnapshot
{
    private PersistedAgentSnapshot()
    {
    }

    public PersistedAgentSnapshot(Guid agentId, string memorySummary, List<TranscriptEntry> recentTranscript)
    {
        AgentId = agentId;
        MemorySummary = memorySummary;
        RecentTranscript = recentTranscript;
    }

    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string MemorySummary { get; private set; } = string.Empty;
    public List<TranscriptEntry> RecentTranscript { get; private set; } = new();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public void Update(string memorySummary, List<TranscriptEntry> recentTranscript)
    {
        MemorySummary = memorySummary;
        RecentTranscript = recentTranscript;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class TranscriptEntry
{
    public string Role { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
