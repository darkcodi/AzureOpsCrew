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

    [Fact]
    public void MapToOpenAiRequest_ReordersToolResultsToFollowTheirAssistantToolCalls()
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "Deploy it"),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_1", "deploy", null)]),
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_2", "verify", null)]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_1", "{\"status\":\"ok\"}")]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_2", "{\"status\":\"healthy\"}")])
        };

        var request = OpenAiRequestMapper.MapToOpenAiRequest(messages, options: null, model: "gpt-5", stream: true);
        var serializedMessages = SerializeMessages(request);

        Assert.Equal("user", serializedMessages[0].GetProperty("role").GetString());
        Assert.Equal("call_1", serializedMessages[1].GetProperty("tool_calls")[0].GetProperty("id").GetString());
        Assert.Equal("call_1", serializedMessages[2].GetProperty("tool_call_id").GetString());
        Assert.Equal("call_2", serializedMessages[3].GetProperty("tool_calls")[0].GetProperty("id").GetString());
        Assert.Equal("call_2", serializedMessages[4].GetProperty("tool_call_id").GetString());
    }

    [Fact]
    public void MapToOpenAiRequest_ReordersToolResultsImmediatelyAfterMultiToolAssistantMessages()
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "What's my ip?"),
            new ChatMessage(ChatRole.Assistant, [
                new TextContent("Checking."),
                new FunctionCallContent("call_1", "getMyIp", null),
                new FunctionCallContent("call_2", "showMyIp", new Dictionary<string, object?>())
            ]),
            new ChatMessage(ChatRole.Assistant, "Summarizing next."),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_2", "{\"shown\":true}")]),
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call_1", "{\"ip\":\"127.0.0.1\"}")])
        };

        var request = OpenAiRequestMapper.MapToOpenAiRequest(messages, options: null, model: "gpt-5", stream: true);
        var serializedMessages = SerializeMessages(request);

        Assert.Equal("assistant", serializedMessages[1].GetProperty("role").GetString());
        Assert.Equal("call_1", serializedMessages[2].GetProperty("tool_call_id").GetString());
        Assert.Equal("call_2", serializedMessages[3].GetProperty("tool_call_id").GetString());
        Assert.Equal("assistant", serializedMessages[4].GetProperty("role").GetString());
        Assert.Equal("Summarizing next.", serializedMessages[4].GetProperty("content").GetString());
    }

    private static JsonElement SerializeMessages(OpenAiChatCompletionRequest request)
    {
        var json = JsonSerializer.Serialize(request, SerializerOptions);

        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("messages").Clone();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
