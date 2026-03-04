using System.Text;
using AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

namespace Infrastructure.Ai.Tests.Clients.OpenAi;

public class OpenAiResponseMapperTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public OpenAiResponseMapperTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task HttpRequest1_ResponseParsing()
    {
        // Arrange
        var response = TestData.HTTP_RESPONSE_1;

        // Act
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(response));
        var results = new List<ChatResponseUpdate?>();
        var toolCallBuilder = new OpenAiStreamToolCallBuilder();
        var isReasoning = false;
        await foreach (var (_, chunk) in OpenAiSseParser.ParseStreamAsync(stream))
        {
            Assert.NotNull(chunk);
            var update = OpenAiResponseMapper.ToChatResponseUpdate(chunk, ref isReasoning, toolCallBuilder);
            results.Add(update);
        }

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal(27, results.Count);

        var firstTextChunk = results[2];
        Assert.NotNull(firstTextChunk);
        Assert.Equal("chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG", firstTextChunk.MessageId);
        Assert.Equal("gpt-5.2-chat-2025-12-11", firstTextChunk.ModelId);
        Assert.Equal("Hey", firstTextChunk.Text);
    }

    [Fact]
    public async Task HttpRequest2_ResponseParsing()
    {
        // Arrange
        var response = TestData.HTTP_RESPONSE_2;

        // Act
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(response));
        var results = new List<ChatResponseUpdate?>();
        var toolCallBuilder = new OpenAiStreamToolCallBuilder();
        var isReasoning = false;
        await foreach (var (_, chunk) in OpenAiSseParser.ParseStreamAsync(stream))
        {
            Assert.NotNull(chunk);
            var update = OpenAiResponseMapper.ToChatResponseUpdate(chunk, ref isReasoning, toolCallBuilder);
            results.Add(update);
        }

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal(4, results.Count);

        var functionCallChunk = results.Last();
        Assert.NotNull(functionCallChunk);
        Assert.Equal("chatcmpl-DEJgre24uAUHKoNbjvT3tZcrLP4tr", functionCallChunk.MessageId);
        Assert.Equal("gpt-5.2-chat-2025-12-11", functionCallChunk.ModelId);
        Assert.NotNull(functionCallChunk.Contents);
        Assert.Single(functionCallChunk.Contents);
        var functionCallContent = functionCallChunk.Contents[0] as FunctionCallContent;
        Assert.NotNull(functionCallContent);
        Assert.Equal("call_uLWyKTpLtOsRmTudAFNQ2Vwv", functionCallContent.CallId);
        Assert.Equal("getMyIp", functionCallContent.Name);
        Assert.NotNull(functionCallContent.Arguments);
        Assert.Empty(functionCallContent.Arguments!);
    }

    [Fact]
    public async Task HttpRequest3_ResponseParsing()
    {
        // Arrange
        var response = TestData.HTTP_RESPONSE_3;

        // Act
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(response));
        var results = new List<ChatResponseUpdate?>();
        var toolCallBuilder = new OpenAiStreamToolCallBuilder();
        var isReasoning = false;
        await foreach (var (_, chunk) in OpenAiSseParser.ParseStreamAsync(stream))
        {
            Assert.NotNull(chunk);
            var update = OpenAiResponseMapper.ToChatResponseUpdate(chunk, ref isReasoning, toolCallBuilder);
            results.Add(update);
        }

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal(126, results.Count);

        var functionCallChunk = results.Last();
        Assert.NotNull(functionCallChunk);
        Assert.Equal("chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi", functionCallChunk.MessageId);
        Assert.Equal("gpt-5.2-chat-2025-12-11", functionCallChunk.ModelId);
        Assert.NotNull(functionCallChunk.Contents);
        Assert.Single(functionCallChunk.Contents);
        var functionCallContent = functionCallChunk.Contents[0] as FunctionCallContent;
        Assert.NotNull(functionCallContent);
        Assert.Equal("call_OW6GhPkVXNfP7dlpCEvCqAVK", functionCallContent.CallId);
        Assert.Equal("showMyIp", functionCallContent.Name);
        Assert.NotNull(functionCallContent.Arguments);
        Assert.Equal(20, functionCallContent.Arguments!.Count);
    }
}
