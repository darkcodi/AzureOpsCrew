namespace Worker.Models;

public record AgentSnapshot(
    Guid AgentId,
    string MemorySummary,
    List<(string Role, string Text)> RecentTranscript);
