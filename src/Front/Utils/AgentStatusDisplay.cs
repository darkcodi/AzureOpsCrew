namespace Front.Utils;
public static class AgentStatusDisplay
{
    public const string WaitingForApproval = "WaitingForApproval";
    public static string ToDisplayText(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "Idle";
        return IsWaitingForApproval(status)
            ? "Waiting for approval"
            : status;
    }
    public static bool IsWaitingForApproval(string? status) =>
        string.Equals(status, WaitingForApproval, StringComparison.OrdinalIgnoreCase);
}