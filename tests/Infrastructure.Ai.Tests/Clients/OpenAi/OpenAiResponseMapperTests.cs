using System.Text;
using System.Text.Json;
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

    [Fact]
    public void NonStreamingResponse_WithReasoningContent_ParsesCorrectly()
    {
        // Arrange
        var jsonResponse = TestData.NON_STREAMING_RESPONSE_WITH_REASONING_CONTENT;
        var openAiResponse = System.Text.Json.JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(jsonResponse);

        // Act
        var result = OpenAiResponseMapper.ToChatResponse(openAiResponse!);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("gpt-5.2-chat-latest", result.ModelId);
        Assert.Single(result.Messages);
        Assert.Equal(ChatRole.Assistant, result.Messages[0].Role);
        Assert.Equal("e6c388e7-8432-41bf-915e-09ddf26f62bb", result.Messages[0].MessageId);

        // Should have 3 contents: TextContent, TextReasoningContent, and UsageContent
        Assert.NotNull(result.Messages[0].Contents);
        Assert.Equal(3, result.Messages[0].Contents.Count);

        // First content should be TextContent
        var textContent = result.Messages[0].Contents[0] as TextContent;
        Assert.NotNull(textContent);
        Assert.Equal("Hi! I'm an Azure DevOps expert. How can I help you with pipelines, CI/CD, repos, boards, artifacts, or release management today?", textContent.Text);

        // Second content should be TextReasoningContent
        var reasoningContent = result.Messages[0].Contents[1] as TextReasoningContent;
        Assert.NotNull(reasoningContent);
        Assert.Equal("Hello! I'm an Azure DevOps expert ready to help with pipelines, CI/CD, repos, boards, artifacts, and release management. How can I assist you today? Since there's no specific request yet, I'll wait for your question or task. If you need help with anything Azure DevOps related, just let me know!", reasoningContent.Text);

        // Third content should be UsageContent
        var usageContent = result.Messages[0].Contents[2] as UsageContent;
        Assert.NotNull(usageContent);
        Assert.Equal(3866, usageContent.Details.InputTokenCount);
        Assert.Equal(98, usageContent.Details.OutputTokenCount);
        Assert.Equal(3964, usageContent.Details.TotalTokenCount);
        Assert.Equal(3840, usageContent.Details.CachedInputTokenCount);
        Assert.Equal(66, usageContent.Details.ReasoningTokenCount);

        // Additional properties should contain finish_reason
        Assert.NotNull(result.AdditionalProperties);
        Assert.True(result.AdditionalProperties.TryGetValue("finish_reason", out var finishReason));
        Assert.Equal("stop", finishReason);
    }

    [Fact]
    public async Task StreamingResponse_WithReasoningContent_ParsesCorrectly()
    {
        // Arrange
        var response = TestData.STREAMING_RESPONSE_WITH_REASONING_CONTENT;

        // Act
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(response));
        var results = new List<ChatResponseUpdate>();
        var toolCallBuilder = new OpenAiStreamToolCallBuilder();
        var isReasoning = false;
        await foreach (var (data, chunk) in OpenAiSseParser.ParseStreamAsync(stream))
        {
            if (chunk != null)
            {
                var update = OpenAiResponseMapper.ToChatResponseUpdate(chunk, ref isReasoning, toolCallBuilder);
                results.Add(update);
            }
        }

        // Assert
        Assert.NotEmpty(results);
        Assert.Equal(4, results.Count);

        // First chunk - role with empty reasoning_content
        var firstChunk = results[0];
        Assert.NotNull(firstChunk);
        Assert.Equal("63ba8d20-ead4-4b0b-a7c5-b82e75e4fce6", firstChunk.MessageId);
        Assert.Equal("gpt-5.2-chat-latest", firstChunk.ModelId);
        Assert.Empty(firstChunk.Contents); // Empty reasoning_content produces no content

        // Second chunk - "Hello" as reasoning_content
        var reasoningChunk1 = results[1];
        Assert.NotNull(reasoningChunk1);
        Assert.Single(reasoningChunk1.Contents);
        var reasoningContent1 = reasoningChunk1.Contents[0] as TextReasoningContent;
        Assert.NotNull(reasoningContent1);
        Assert.Equal("Hello", reasoningContent1.Text);

        // Third chunk - more reasoning_content
        var reasoningChunk2 = results[2];
        Assert.NotNull(reasoningChunk2);
        Assert.Single(reasoningChunk2.Contents);
        var reasoningContent2 = reasoningChunk2.Contents[0] as TextReasoningContent;
        Assert.NotNull(reasoningContent2);
        Assert.Equal("! I'm an Azure DevOps expert ready to help you with CI/CD pipelines, repos, boards, artifacts, release management, and more.", reasoningContent2.Text);

        // Fourth chunk - regular content + finish_reason + usage
        var contentChunk = results[3];
        Assert.NotNull(contentChunk);
        Assert.Equal(2, contentChunk.Contents.Count);
        // UsageContent is added first, then TextContent
        var usageContent = contentChunk.Contents[0] as UsageContent;
        Assert.NotNull(usageContent);
        Assert.Equal(3866, usageContent.Details.InputTokenCount);
        Assert.Equal(102, usageContent.Details.OutputTokenCount);
        Assert.Equal(3968, usageContent.Details.TotalTokenCount);
        Assert.Equal(3840, usageContent.Details.CachedInputTokenCount);
        Assert.Equal(65, usageContent.Details.ReasoningTokenCount);
        var textContent = contentChunk.Contents[1] as TextContent;
        Assert.NotNull(textContent);
        Assert.Equal("Hello! I'm here to help with all things Azure DevOps - pipelines, CI/CD, repos, boards, artifacts, and release management. What can I assist you with today?", textContent.Text);
        Assert.NotNull(usageContent);
        Assert.Equal(3866, usageContent.Details.InputTokenCount);
        Assert.Equal(102, usageContent.Details.OutputTokenCount);
        Assert.Equal(3968, usageContent.Details.TotalTokenCount);
        Assert.Equal(3840, usageContent.Details.CachedInputTokenCount);
        Assert.Equal(65, usageContent.Details.ReasoningTokenCount);
        Assert.Equal(ChatFinishReason.Stop, contentChunk.FinishReason);
    }
}
