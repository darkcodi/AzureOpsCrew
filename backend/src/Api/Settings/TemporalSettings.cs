namespace AzureOpsCrew.Api.Settings;

public sealed class TemporalSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 7233;

    public string GetTarget() => $"{Host}:{Port}";
}
