namespace Worker.Models;

public record AgentStatus(
    string? CurrentTurnId,
    string Phase,
    int TurnsSoFar);
