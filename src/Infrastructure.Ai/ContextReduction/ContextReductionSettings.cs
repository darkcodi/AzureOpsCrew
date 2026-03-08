namespace AzureOpsCrew.Infrastructure.Ai.ContextReduction;

public sealed class ContextReductionSettings
{
    public double SoftThresholdPercent { get; set; } = 0.85;
    public int RecentToolBudgetTokens { get; set; } = 12_000;
    public int MinReservedOutputTokens { get; set; } = 4096;
    public int FallbackContextWindowSize { get; set; } = 128_000;
    public double SafetyMargin { get; set; } = 1.15;
    public double CharsPerToken { get; set; } = 4.0;
}
