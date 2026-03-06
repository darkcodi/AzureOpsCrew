using System.Text.Json;

namespace AzureOpsCrew.Domain.Utils;

public static class JsonUtils
{
    public static JsonElement Schema(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
