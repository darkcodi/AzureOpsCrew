using System.Text;
using System.Text.Json;
using AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;
using Xunit.Abstractions;

namespace Infrastructure.Ai.Tests.Clients.OpenAi;

public class OpenAiSseParserTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public OpenAiSseParserTests(ITestOutputHelper testOutputHelper)
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
        var results = new List<OpenAiChatCompletionChunk?>();
        await foreach (var (_, chunk) in OpenAiSseParser.ParseStreamAsync(stream))
        {
            results.Add(chunk);
        }

        // Assert
        Assert.Equal(27, results.Count);

        /* First chunk is "Prompt moderation annotation"
data: {
  "choices": [],
  "created": 0,
  "id": "",
  "model": "",
  "object": "",
  "prompt_filter_results": [
    {
      "prompt_index": 0,
      "content_filter_results": {
        "hate": {"filtered": false, "severity": "safe"},
        "self_harm": {"filtered": false, "severity": "safe"},
        "sexual": {"filtered": false, "severity": "safe"},
        "violence": {"filtered": false, "severity": "safe"}
      }
    }
  ]
}
         */
        // This is not assistant text. It is an Azure/OpenAI-in-Foundry prompt filtering annotation message.
        // Microsoft’s streaming docs show this exact pattern: blank id/object/model, created: 0, choices: [], and prompt_filter_results describing moderation results for the prompt before normal text chunks arrive.
        var chunk1 = results[0];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk1, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk1);
        Assert.Empty(chunk1.Id);
        Assert.Empty(chunk1.Object);
        Assert.Empty(chunk1.Model);
        Assert.Equal(0, chunk1.Created);
        Assert.Empty(chunk1.Choices);
        Assert.Null(chunk1.Usage);
        Assert.Null(chunk1.SystemFingerprint);
        Assert.NotNull(chunk1.PromptFilterResults);
        Assert.Single(chunk1.PromptFilterResults);
        Assert.Equal(0, chunk1.PromptFilterResults[0].PromptIndex);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Hate);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Hate!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Hate!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Violence);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Violence!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Violence!.Severity);

        /*
data: {
  "choices": [
    {
      "content_filter_results": {},
      "delta": {"content": "", "refusal": null, "role": "assistant"},
      "finish_reason": null,
      "index": 0,
      "logprobs": null
    }
  ],
  "created": 1772304649,
  "id": "chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG",
  "model": "gpt-5.2-chat-2025-12-11",
  "obfuscation": "70NTC3qxf",
  "object": "chat.completion.chunk",
  "system_fingerprint": null
}
         */
        /*
         * This is the first real completion chunk. A few pieces matter:
         *  - object: "chat.completion.chunk" means this is a normal streamed completion chunk.
         *  - id is the completion ID, and all chunks for this response share that same ID.
         *  - created is a Unix timestamp, and each chunk shares that timestamp.
         *  - delta.role: "assistant" announces the speaker role for the streamed message.
         *  - delta.content: "" is normal. OpenAI’s streaming examples show an initial chunk with role: "assistant" and empty content before actual text tokens start arriving.
         *  - refusal: null means there is no refusal text in this delta. The refusal field is where a refusal message would appear if the model were refusing.
         *  - finish_reason: null means generation is still ongoing. The stop reason only arrives later.
         *  - logprobs: null means token log probabilities were not included.
         *  - system_fingerprint: null is fine; that field is optional and deprecated in the current reference.
         *  - obfuscation is random padding added to normalize chunk sizes against side-channel leakage. It is not model output and should be ignored. It can be disabled with stream_options.include_obfuscation = false if you trust the network path and want less bandwidth overhead.
         *  - content_filter_results: {} here just means there is no detailed per-chunk filter annotation attached to this event. The Azure docs note that filter annotations may be absent, empty, or null depending on configuration and API behavior. Since later chunks do contain concrete filter results, this empty object is not alarming; it is just an empty annotation payload on this chunk. That last sentence is an inference from the documented examples plus your payload shape, not a magical secret rune.
         */
        var chunk2 = results[1];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk2, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk2);
        Assert.Equal("chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG", chunk2.Id);
        Assert.Equal("chat.completion.chunk", chunk2.Object);
        Assert.Equal(1772304649, chunk2.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", chunk2.Model);
        Assert.Null(chunk2.Usage);
        Assert.Null(chunk2.SystemFingerprint);
        Assert.Null(chunk2.PromptFilterResults);
        Assert.NotNull(chunk2.Choices);
        Assert.Single(chunk2.Choices);
        Assert.Equal(0, chunk2.Choices[0].Index);
        Assert.NotNull(chunk2.Choices[0].ContentFilterResults);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Hate);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Sexual);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Violence);
        Assert.Null(chunk2.Choices[0].FinishReason);
        Assert.NotNull(chunk2.Choices[0].Delta);
        Assert.Equal("assistant", chunk2.Choices[0].Delta.Role);
        Assert.Empty(chunk2.Choices[0].Delta.Content!);
        Assert.Null(chunk2.Choices[0].Delta.Reasoning);
        Assert.Null(chunk2.Choices[0].Delta.ToolCalls);
        Assert.Null(chunk2.Choices[0].Delta.Refusal);

        /*
data: {
  "choices": [
    {
      "content_filter_results": {
        "hate": {"filtered": false, "severity": "safe"},
        "self_harm": {"filtered": false, "severity": "safe"},
        "sexual": {"filtered": false, "severity": "safe"},
        "violence": {"filtered": false, "severity": "safe"}
      },
      "delta": {"content": "Hey"},
      "finish_reason": null,
      "index": 0,
      "logprobs": null
    }
  ],
  ...
  "obfuscation": "wC83yD3e"
}
         */
        // First text token
        var chunk3 = results[2];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk3, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk3);
        Assert.Equal("chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG", chunk3.Id);
        Assert.Equal("chat.completion.chunk", chunk3.Object);
        Assert.Equal(1772304649, chunk3.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", chunk3.Model);
        Assert.Null(chunk3.Usage);
        Assert.Null(chunk3.SystemFingerprint);
        Assert.Null(chunk3.PromptFilterResults);
        Assert.NotNull(chunk3.Choices);
        Assert.Single(chunk3.Choices);
        Assert.Equal(0, chunk3.Choices[0].Index);
        Assert.NotNull(chunk3.Choices[0].ContentFilterResults);
        Assert.NotNull(chunk3.Choices[0].ContentFilterResults!.Hate);
        Assert.False(chunk3.Choices[0].ContentFilterResults!.Hate!.Filtered);
        Assert.Equal("safe", chunk3.Choices[0].ContentFilterResults!.Hate!.Severity);
        Assert.NotNull(chunk3.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.False(chunk3.Choices[0].ContentFilterResults!.SelfHarm!.Filtered);
        Assert.Equal("safe", chunk3.Choices[0].ContentFilterResults!.SelfHarm!.Severity);
        Assert.NotNull(chunk3.Choices[0].ContentFilterResults!.Sexual);
        Assert.False(chunk3.Choices[0].ContentFilterResults!.Sexual!.Filtered);
        Assert.Equal("safe", chunk3.Choices[0].ContentFilterResults!.Sexual!.Severity);
        Assert.NotNull(chunk3.Choices[0].ContentFilterResults!.Violence);
        Assert.False(chunk3.Choices[0].ContentFilterResults!.Violence!.Filtered);
        Assert.Equal("safe", chunk3.Choices[0].ContentFilterResults!.Violence!.Severity);
        Assert.Null(chunk3.Choices[0].FinishReason);
        Assert.NotNull(chunk3.Choices[0].Delta);
        Assert.Null(chunk3.Choices[0].Delta.Role);
        Assert.Equal("Hey", chunk3.Choices[0].Delta.Content);
        Assert.Null(chunk3.Choices[0].Delta.Reasoning);
        Assert.Null(chunk3.Choices[0].Delta.ToolCalls);
        Assert.Null(chunk3.Choices[0].Delta.Refusal);

        /*
data: {
  "choices": [{
    "content_filter_results": {},
    "delta": {},
    "finish_reason":"stop",
    "index":0,
    "logprobs":null
  }],
  "created":1772304649,
  "id":"chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG",
  "model":"gpt-5.2-chat-2025-12-11",
  "obfuscation":"wnbgD",
  "object":"chat.completion.chunk",
  "system_fingerprint":null
}
         */
        /* This is the final completion chunk:
            - delta: {} means no more text is being added in this event. That is normal for the final chunk.
            - finish_reason: "stop" means the model ended normally, rather than being cut off by length, content filtering, or a tool call.
            - content_filter_results: {} here is just an empty annotation payload on the terminal chunk, not extra content.
            - obfuscation remains ignorable junk padding.
         */
        var lastChunk = results.Last();
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(lastChunk, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(lastChunk);
        Assert.Equal("chatcmpl-DEJgnNRPyTQCzYVeB8duG7wiB4McG", lastChunk.Id);
        Assert.Equal("chat.completion.chunk", lastChunk.Object);
        Assert.Equal(1772304649, lastChunk.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", lastChunk.Model);
        Assert.Null(lastChunk.Usage);
        Assert.Null(lastChunk.SystemFingerprint);
        Assert.Null(lastChunk.PromptFilterResults);
        Assert.NotNull(lastChunk.Choices);
        Assert.Single(lastChunk.Choices);
        Assert.Equal(0, lastChunk.Choices[0].Index);
    }

    [Fact]
    public async Task HttpRequest2_ResponseParsing()
    {
        // Arrange
        var response = TestData.HTTP_RESPONSE_2;

        // Act
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(response));
        var results = new List<OpenAiChatCompletionChunk?>();
        await foreach (var (_, chunk) in OpenAiSseParser.ParseStreamAsync(stream))
        {
          results.Add(chunk);
        }

        // Assert
        Assert.Equal(4, results.Count);

        var chunk1 = results[0];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk1, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk1);
        Assert.Empty(chunk1.Id);
        Assert.Empty(chunk1.Object);
        Assert.Empty(chunk1.Model);
        Assert.Equal(0, chunk1.Created);
        Assert.Empty(chunk1.Choices);
        Assert.Null(chunk1.Usage);
        Assert.Null(chunk1.SystemFingerprint);
        Assert.NotNull(chunk1.PromptFilterResults);
        Assert.Single(chunk1.PromptFilterResults);
        Assert.Equal(0, chunk1.PromptFilterResults[0].PromptIndex);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Hate);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Hate!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Hate!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Violence);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Violence!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Violence!.Severity);

        /*
data: {
  "choices": [{
    "content_filter_results": {},
    "delta": {
      "content": null,
      "refusal": null,
      "role": "assistant",
      "tool_calls": [{
        "function": {
          "arguments": "",
          "name": "getMyIp"
        },
        "id": "call_uLWyKTpLtOsRmTudAFNQ2Vwv",
        "index": 0,
        "type": "function"
      }]
    },
    "finish_reason": null,
    "index": 0
  }],
  ...
  "obfuscation": "beU6bph0"
}
         */
        /*
         * This first real chunk says:
            - the speaker is assistant
            - the assistant is not sending text (content: null)
            - it is starting tool call index 0
            - tool call ID is call_uLWyKTpLtOsRmTudAFNQ2Vwv
            - tool/function name is getMyIp
            - arguments have started but are currently empty ("")
          That means parser should initialize a tool-call accumulator for index: 0, store the ID and function name, and start concatenating function.arguments as later chunks arrive.
          The docs for streamed chat chunks describe deltas this way, including tool calls and chunk-by-chunk argument assembly.
         */
        var chunk2 = results[1];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk2, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk2);
        Assert.Equal("chatcmpl-DEJgre24uAUHKoNbjvT3tZcrLP4tr", chunk2.Id);
        Assert.Equal("chat.completion.chunk", chunk2.Object);
        Assert.Equal(1772304653, chunk2.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", chunk2.Model);
        Assert.Null(chunk2.Usage);
        Assert.Null(chunk2.SystemFingerprint);
        Assert.Null(chunk2.PromptFilterResults);
        Assert.NotNull(chunk2.Choices);
        Assert.Single(chunk2.Choices);
        Assert.Equal(0, chunk2.Choices[0].Index);
        Assert.NotNull(chunk2.Choices[0].ContentFilterResults);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Hate);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Sexual);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Violence);
        Assert.Null(chunk2.Choices[0].FinishReason);
        Assert.NotNull(chunk2.Choices[0].Delta);
        Assert.Equal("assistant", chunk2.Choices[0].Delta.Role);
        Assert.Null(chunk2.Choices[0].Delta.Content);
        Assert.Null(chunk2.Choices[0].Delta.Reasoning);
        Assert.NotNull(chunk2.Choices[0].Delta.ToolCalls);
        Assert.Single(chunk2.Choices[0].Delta.ToolCalls!);
        Assert.Equal("call_uLWyKTpLtOsRmTudAFNQ2Vwv", chunk2.Choices[0].Delta.ToolCalls![0].Id);
        Assert.Equal("getMyIp", chunk2.Choices[0].Delta.ToolCalls![0].Function!.Name);
        Assert.Equal("", chunk2.Choices[0].Delta.ToolCalls![0].Function!.Arguments);
        Assert.Null(chunk2.Choices[0].Delta.Refusal);

        /*
data: {
  "choices": [{
    "content_filter_results": {},
    "delta": {
      "tool_calls": [{
        "function": {
          "arguments": "{}"
        },
        "index": 0
      }]
    },
    "finish_reason": null,
    "index": 0
  }],
  ...
}
         */
        /*
         * This chunk adds more data for the same tool call because it uses index: 0. It contributes the argument fragment: {}
         * Since the previous arguments fragment was "", the accumulated argument string is now: {}
         */
        var chunk3 = results[2];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk3, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk3);
        Assert.Equal("chatcmpl-DEJgre24uAUHKoNbjvT3tZcrLP4tr", chunk3.Id);
        Assert.Equal("chat.completion.chunk", chunk3.Object);
        Assert.Equal(1772304653, chunk3.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", chunk3.Model);
        Assert.Null(chunk3.Usage);
        Assert.Null(chunk3.SystemFingerprint);
        Assert.Null(chunk3.PromptFilterResults);
        Assert.NotNull(chunk3.Choices);
        Assert.Single(chunk3.Choices);
        Assert.Equal(0, chunk3.Choices[0].Index);
        Assert.NotNull(chunk3.Choices[0].ContentFilterResults);
        Assert.Null(chunk3.Choices[0].ContentFilterResults!.Hate);
        Assert.Null(chunk3.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.Null(chunk3.Choices[0].ContentFilterResults!.Sexual);
        Assert.Null(chunk3.Choices[0].ContentFilterResults!.Violence);
        Assert.Null(chunk3.Choices[0].FinishReason);
        Assert.NotNull(chunk3.Choices[0].Delta);
        Assert.Null(chunk3.Choices[0].Delta.Role);
        Assert.Null(chunk3.Choices[0].Delta.Content);
        Assert.Null(chunk3.Choices[0].Delta.Reasoning);
        Assert.NotNull(chunk3.Choices[0].Delta.ToolCalls);
        Assert.Single(chunk3.Choices[0].Delta.ToolCalls!);
        Assert.Equal(0, chunk3.Choices[0].Delta.ToolCalls![0].Index);
        Assert.Null(chunk3.Choices[0].Delta.ToolCalls![0].Id);
        Assert.Null(chunk3.Choices[0].Delta.ToolCalls![0].Function!.Name);
        Assert.Equal("{}", chunk3.Choices[0].Delta.ToolCalls![0].Function!.Arguments);
        Assert.Null(chunk3.Choices[0].Delta.Refusal);

        /*
data: {
  "choices": [{
    "content_filter_results": {},
    "delta": {},
    "finish_reason": "tool_calls",
    "index": 0
  }],
  ...
}
         */
        /*
         * This is the terminal chunk for the assistant turn.
           It means:
            - no more text or tool-call deltas are coming in this assistant message (delta: {})
            - the reason generation stopped is "tool_calls", which means the model intentionally ended the turn because it wants your app to execute the requested tool(s), not because it finished with ordinary text like "stop"
         */
        var lastChunk = results.Last();
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(lastChunk, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(lastChunk);
        Assert.Equal("chatcmpl-DEJgre24uAUHKoNbjvT3tZcrLP4tr", lastChunk.Id);
        Assert.Equal("chat.completion.chunk", lastChunk.Object);
        Assert.Equal(1772304653, lastChunk.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", lastChunk.Model);
        Assert.Null(lastChunk.Usage);
        Assert.Null(lastChunk.SystemFingerprint);
        Assert.Null(lastChunk.PromptFilterResults);
        Assert.NotNull(lastChunk.Choices);
        Assert.Single(lastChunk.Choices);
        Assert.Equal(0, lastChunk.Choices[0].Index);
        Assert.NotNull(lastChunk.Choices[0].ContentFilterResults);
        Assert.Null(lastChunk.Choices[0].ContentFilterResults!.Hate);
        Assert.Null(lastChunk.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.Null(lastChunk.Choices[0].ContentFilterResults!.Sexual);
        Assert.Null(lastChunk.Choices[0].ContentFilterResults!.Violence);
        Assert.NotNull(lastChunk.Choices[0].Delta);
        Assert.Null(lastChunk.Choices[0].Delta.Role);
        Assert.Null(lastChunk.Choices[0].Delta.Content);
        Assert.Null(lastChunk.Choices[0].Delta.Reasoning);
        Assert.Null(lastChunk.Choices[0].Delta.ToolCalls);
        Assert.Null(lastChunk.Choices[0].Delta.Refusal);
        Assert.Equal("tool_calls", lastChunk.Choices[0].FinishReason);
    }

    [Fact]
    public async Task HttpRequest3_ResponseParsing()
    {
        // Arrange
        var response = TestData.HTTP_RESPONSE_3;

        // Act
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(response));
        var results = new List<OpenAiChatCompletionChunk?>();
        await foreach (var (_, chunk) in OpenAiSseParser.ParseStreamAsync(stream))
        {
          results.Add(chunk);
        }

        // Assert
        Assert.Equal(126, results.Count);

        var chunk1 = results[0];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk1, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk1);
        Assert.Empty(chunk1.Id);
        Assert.Empty(chunk1.Object);
        Assert.Empty(chunk1.Model);
        Assert.Equal(0, chunk1.Created);
        Assert.Empty(chunk1.Choices);
        Assert.Null(chunk1.Usage);
        Assert.Null(chunk1.SystemFingerprint);
        Assert.NotNull(chunk1.PromptFilterResults);
        Assert.Single(chunk1.PromptFilterResults);
        Assert.Equal(0, chunk1.PromptFilterResults[0].PromptIndex);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Hate);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Hate!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Hate!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.SelfHarm!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Sexual!.Severity);
        Assert.NotNull(chunk1.PromptFilterResults[0].ContentFilterResults!.Violence);
        Assert.False(chunk1.PromptFilterResults[0].ContentFilterResults!.Violence!.Filtered);
        Assert.Equal("safe", chunk1.PromptFilterResults[0].ContentFilterResults!.Violence!.Severity);

        var chunk2 = results[1];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk2, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk2);
        Assert.Equal("chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi", chunk2.Id);
        Assert.Equal("chat.completion.chunk", chunk2.Object);
        Assert.Equal(1772304656, chunk2.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", chunk2.Model);
        Assert.Null(chunk2.Usage);
        Assert.Null(chunk2.SystemFingerprint);
        Assert.Null(chunk2.PromptFilterResults);
        Assert.NotNull(chunk2.Choices);
        Assert.Single(chunk2.Choices);
        Assert.Equal(0, chunk2.Choices[0].Index);
        Assert.NotNull(chunk2.Choices[0].ContentFilterResults);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Hate);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Sexual);
        Assert.Null(chunk2.Choices[0].ContentFilterResults!.Violence);
        Assert.Null(chunk2.Choices[0].FinishReason);
        Assert.NotNull(chunk2.Choices[0].Delta);
        Assert.Equal("assistant", chunk2.Choices[0].Delta.Role);
        Assert.Null(chunk2.Choices[0].Delta.Content);
        Assert.Null(chunk2.Choices[0].Delta.Reasoning);
        Assert.NotNull(chunk2.Choices[0].Delta.ToolCalls);
        Assert.Single(chunk2.Choices[0].Delta.ToolCalls!);
        Assert.Equal("call_OW6GhPkVXNfP7dlpCEvCqAVK", chunk2.Choices[0].Delta.ToolCalls![0].Id);
        Assert.Equal("showMyIp", chunk2.Choices[0].Delta.ToolCalls![0].Function!.Name);
        Assert.Equal("", chunk2.Choices[0].Delta.ToolCalls![0].Function!.Arguments);
        Assert.Null(chunk2.Choices[0].Delta.Refusal);

        var chunk3 = results[2];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk3, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk3);
        Assert.Equal("chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi", chunk3.Id);
        Assert.Equal("chat.completion.chunk", chunk3.Object);
        Assert.Equal(1772304656, chunk3.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", chunk3.Model);
        Assert.Null(chunk3.Usage);
        Assert.Null(chunk3.SystemFingerprint);
        Assert.Null(chunk3.PromptFilterResults);
        Assert.NotNull(chunk3.Choices);
        Assert.Single(chunk3.Choices);
        Assert.Equal(0, chunk3.Choices[0].Index);
        Assert.NotNull(chunk3.Choices[0].ContentFilterResults);
        Assert.Null(chunk3.Choices[0].ContentFilterResults!.Hate);
        Assert.Null(chunk3.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.Null(chunk3.Choices[0].ContentFilterResults!.Sexual);
        Assert.Null(chunk3.Choices[0].ContentFilterResults!.Violence);
        Assert.Null(chunk3.Choices[0].FinishReason);
        Assert.NotNull(chunk3.Choices[0].Delta);
        Assert.Null(chunk3.Choices[0].Delta.Role);
        Assert.Null(chunk3.Choices[0].Delta.Content);
        Assert.Null(chunk3.Choices[0].Delta.Reasoning);
        Assert.NotNull(chunk3.Choices[0].Delta.ToolCalls);
        Assert.Single(chunk3.Choices[0].Delta.ToolCalls!);
        Assert.Equal(0, chunk3.Choices[0].Delta.ToolCalls![0].Index);
        Assert.Null(chunk3.Choices[0].Delta.ToolCalls![0].Id);
        Assert.Null(chunk3.Choices[0].Delta.ToolCalls![0].Function!.Name);
        Assert.Equal("{\"", chunk3.Choices[0].Delta.ToolCalls![0].Function!.Arguments);
        Assert.Null(chunk3.Choices[0].Delta.Refusal);

        var chunk4 = results[3];
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(chunk4, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(chunk4);
        Assert.Equal("chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi", chunk4.Id);
        Assert.Equal("chat.completion.chunk", chunk4.Object);
        Assert.Equal(1772304656, chunk4.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", chunk4.Model);
        Assert.Null(chunk4.Usage);
        Assert.Null(chunk4.SystemFingerprint);
        Assert.Null(chunk4.PromptFilterResults);
        Assert.NotNull(chunk4.Choices);
        Assert.Single(chunk4.Choices);
        Assert.Equal(0, chunk4.Choices[0].Index);
        Assert.NotNull(chunk4.Choices[0].ContentFilterResults);
        Assert.Null(chunk4.Choices[0].ContentFilterResults!.Hate);
        Assert.Null(chunk4.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.Null(chunk4.Choices[0].ContentFilterResults!.Sexual);
        Assert.Null(chunk4.Choices[0].ContentFilterResults!.Violence);
        Assert.Null(chunk4.Choices[0].FinishReason);
        Assert.NotNull(chunk4.Choices[0].Delta);
        Assert.Null(chunk4.Choices[0].Delta.Role);
        Assert.Null(chunk4.Choices[0].Delta.Content);
        Assert.Null(chunk4.Choices[0].Delta.Reasoning);
        Assert.NotNull(chunk4.Choices[0].Delta.ToolCalls);
        Assert.Single(chunk4.Choices[0].Delta.ToolCalls!);
        Assert.Equal(0, chunk4.Choices[0].Delta.ToolCalls![0].Index);
        Assert.Null(chunk4.Choices[0].Delta.ToolCalls![0].Id);
        Assert.Null(chunk4.Choices[0].Delta.ToolCalls![0].Function!.Name);
        Assert.Equal("ip", chunk4.Choices[0].Delta.ToolCalls![0].Function!.Arguments);
        Assert.Null(chunk4.Choices[0].Delta.Refusal);

        var lastChunk = results.Last();
        _testOutputHelper.WriteLine(JsonSerializer.Serialize(lastChunk, new JsonSerializerOptions() { WriteIndented = true }));
        Assert.NotNull(lastChunk);
        Assert.Equal("chatcmpl-DEJguaV0swncJqLDbjg6R2zaFdxCi", lastChunk.Id);
        Assert.Equal("chat.completion.chunk", lastChunk.Object);
        Assert.Equal(1772304656, lastChunk.Created);
        Assert.Equal("gpt-5.2-chat-2025-12-11", lastChunk.Model);
        Assert.Null(lastChunk.Usage);
        Assert.Null(lastChunk.SystemFingerprint);
        Assert.Null(lastChunk.PromptFilterResults);
        Assert.NotNull(lastChunk.Choices);
        Assert.Single(lastChunk.Choices);
        Assert.Equal(0, lastChunk.Choices[0].Index);
        Assert.NotNull(lastChunk.Choices[0].ContentFilterResults);
        Assert.Null(lastChunk.Choices[0].ContentFilterResults!.Hate);
        Assert.Null(lastChunk.Choices[0].ContentFilterResults!.SelfHarm);
        Assert.Null(lastChunk.Choices[0].ContentFilterResults!.Sexual);
        Assert.Null(lastChunk.Choices[0].ContentFilterResults!.Violence);
        Assert.NotNull(lastChunk.Choices[0].Delta);
        Assert.Null(lastChunk.Choices[0].Delta.Role);
        Assert.Null(lastChunk.Choices[0].Delta.Content);
        Assert.Null(lastChunk.Choices[0].Delta.Reasoning);
        Assert.Null(lastChunk.Choices[0].Delta.ToolCalls);
        Assert.Null(lastChunk.Choices[0].Delta.Refusal);
        Assert.Equal("tool_calls", lastChunk.Choices[0].FinishReason);
    }
}
