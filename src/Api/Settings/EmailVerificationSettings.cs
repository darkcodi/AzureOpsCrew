namespace AzureOpsCrew.Api.Settings;

public sealed class EmailVerificationSettings
{
    public bool IsEnabled { get; set; } = true;
    public int CodeLength { get; set; } = 6;
    public int CodeTtlMinutes { get; set; } = 10;
    public int ResendCooldownSeconds { get; set; } = 30;
    public int MaxVerificationAttempts { get; set; } = 5;
}
