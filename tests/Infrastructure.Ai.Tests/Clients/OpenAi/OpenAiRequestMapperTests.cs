using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;
using Microsoft.Extensions.AI;

namespace Infrastructure.Ai.Tests.Clients.OpenAi;

public class OpenAiRequestMapperTests
{
    [Fact]
    public void MapToOpenAiRequest_DoesNotSerializeNameForToolMessages()
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "Deploy it")
            {
                AuthorName = "User"
            },
            new ChatMessage(ChatRole.Assistant, "Running deployment")
            {
                AuthorName = "Manager"
            },
            new ChatMessage(
                ChatRole.Tool,
                [new FunctionResultContent("call_123", "{\"status\":\"ok\"}")])
            {
                AuthorName = "azuredevops"
            }
        };

        var request = OpenAiRequestMapper.MapToOpenAiRequest(messages, options: null, model: "gpt-5", stream: true);
        var json = JsonSerializer.Serialize(request, SerializerOptions);

        using var document = JsonDocument.Parse(json);
        var serializedMessages = document.RootElement.GetProperty("messages");

        Assert.Equal("User", serializedMessages[0].GetProperty("name").GetString());
        Assert.Equal("Manager", serializedMessages[1].GetProperty("name").GetString());

        var toolMessage = serializedMessages[2];
        Assert.Equal("tool", toolMessage.GetProperty("role").GetString());
        Assert.Equal("call_123", toolMessage.GetProperty("tool_call_id").GetString());
        Assert.False(toolMessage.TryGetProperty("name", out _));
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
