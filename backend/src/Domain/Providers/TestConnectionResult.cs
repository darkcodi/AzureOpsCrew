namespace AzureOpsCrew.Domain.Providers;

public sealed record TestConnectionResult(
    bool Success,
    string? ErrorType = null,
    string? ErrorDetails = null)
{
    public static TestConnectionResult Successful() => new(true);

    public static TestConnectionResult ValidationFailed(string message) =>
        new(false, "validation", message);

    public static TestConnectionResult AuthenticationFailed(string message) =>
        new(false, "authentication", message);

    public static TestConnectionResult NetworkError(string message) =>
        new(false, "network", message);

    public static TestConnectionResult Timeout() =>
        new(false, "timeout", "Request timed out");

    public static TestConnectionResult UnknownError(string message) =>
        new(false, "unknown", message);
}
