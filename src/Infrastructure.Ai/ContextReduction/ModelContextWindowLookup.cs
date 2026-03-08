namespace AzureOpsCrew.Infrastructure.Ai.ContextReduction;

public static class ModelContextWindowLookup
{
    private static readonly Dictionary<string, int> KnownModels = new(StringComparer.OrdinalIgnoreCase)
    {
        // Anthropic
        ["claude-3-5-sonnet"] = 200_000,
        ["claude-3.5-sonnet"] = 200_000,
        ["claude-3-5-haiku"] = 200_000,
        ["claude-3.5-haiku"] = 200_000,
        ["claude-3-opus"] = 200_000,
        ["claude-3-sonnet"] = 200_000,
        ["claude-3-haiku"] = 200_000,
        ["claude-4-sonnet"] = 200_000,
        ["claude-4-opus"] = 200_000,
        ["claude-sonnet"] = 200_000,
        ["claude-opus"] = 200_000,
        ["claude-haiku"] = 200_000,

        // OpenAI GPT-4o
        ["gpt-4o"] = 128_000,
        ["gpt-4o-mini"] = 128_000,

        // OpenAI GPT-4
        ["gpt-4-turbo"] = 128_000,
        ["gpt-4"] = 8_192,
        ["gpt-4-32k"] = 32_768,

        // OpenAI GPT-3.5
        ["gpt-3.5-turbo"] = 16_385,

        // OpenAI o-series
        ["o1"] = 200_000,
        ["o1-mini"] = 128_000,
        ["o1-pro"] = 200_000,
        ["o3"] = 200_000,
        ["o3-mini"] = 200_000,
        ["o3-pro"] = 200_000,
        ["o4-mini"] = 200_000,

        // Google
        ["gemini-2.0-flash"] = 1_048_576,
        ["gemini-2.5-pro"] = 1_048_576,
        ["gemini-1.5-pro"] = 2_097_152,
        ["gemini-1.5-flash"] = 1_048_576,

        // Meta Llama
        ["llama-3.1"] = 128_000,
        ["llama-3.2"] = 128_000,
        ["llama-3.3"] = 128_000,
        ["llama-4"] = 128_000,

        // Mistral
        ["mistral-large"] = 128_000,
        ["mistral-medium"] = 32_000,
        ["mistral-small"] = 128_000,

        // DeepSeek
        ["deepseek-chat"] = 64_000,
        ["deepseek-r1"] = 64_000,
        ["deepseek-v3"] = 64_000,
    };

    public static int? GetContextWindowSize(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return null;

        // Try exact match first
        if (KnownModels.TryGetValue(modelId, out var exactSize))
            return exactSize;

        // Try prefix matching (e.g., "gpt-4o-2024-08-06" matches "gpt-4o")
        // Sort by key length descending so longer (more specific) prefixes match first
        foreach (var kvp in KnownModels.OrderByDescending(k => k.Key.Length))
        {
            if (modelId.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }
}
