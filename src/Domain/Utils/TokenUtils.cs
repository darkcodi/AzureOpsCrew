using SharpToken;

namespace AzureOpsCrew.Domain.Utils;

public static class TokenUtils
{
    public static int EstimateTokensCount(string text)
    {
        var encoding = GptEncoding.GetEncoding("o200k_base");
        var count = encoding.CountTokens(text);
        return count;
    }
}
