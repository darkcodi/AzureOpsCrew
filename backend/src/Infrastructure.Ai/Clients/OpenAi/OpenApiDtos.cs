using System.Text.Json.Serialization;

namespace AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;

// https://developers.openai.com/api/reference/resources/chat/subresources/completions/streaming-events

/// <summary>
/// Request payload for OpenAI chat completion API
/// </summary>
public class OpenAiChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenAiMessage> Messages { get; set; } = new();

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    public float? TopP { get; set; }

    [JsonPropertyName("n")]
    public int? N { get; set; }

    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stop")]
    public object? Stop { get; set; } // Can be string or string[]

    [JsonPropertyName("presence_penalty")]
    public float? PresencePenalty { get; set; }

    [JsonPropertyName("frequency_penalty")]
    public float? FrequencyPenalty { get; set; }

    [JsonPropertyName("logit_bias")]
    public Dictionary<string, int>? LogitBias { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("tools")]
    public List<OpenAiTool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public object? ToolChoice { get; set; } // Can be "none", "auto", "required", or object

    [JsonPropertyName("parallel_tool_calls")]
    public bool? ParallelToolCalls { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }
}

/// <summary>
/// Message format for OpenAI API
/// </summary>
public class OpenAiMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public object? Content { get; set; } // Can be string or array of content parts

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAiToolCall>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }
}

/// <summary>
/// Content part for multi-content messages (array format)
/// </summary>
public class OpenAiContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    public OpenAiImageUrl? ImageUrl { get; set; }
}

/// <summary>
/// Image URL object for vision content
/// </summary>
public class OpenAiImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}

/// <summary>
/// Tool definition for function calling
/// </summary>
public class OpenAiTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAiFunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// Function definition for tool calling
/// </summary>
public class OpenAiFunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public object? Parameters { get; set; } // JSON schema object
}

/// <summary>
/// Non-streaming response from OpenAI chat completion API
/// </summary>
public class OpenAiChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public OpenAiUsage? Usage { get; set; }

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }
}

/// <summary>
/// Choice in the response
/// </summary>
public class OpenAiChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public OpenAiMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Streaming response chunk from OpenAI chat completion API
/// </summary>
public class OpenAiChatCompletionChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<OpenAiStreamChoice> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public OpenAiUsage? Usage { get; set; }

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }

    [JsonPropertyName("prompt_filter_results")]
    public List<OpenAiPromptFilterResults>? PromptFilterResults { get; set; }
}

/// <summary>
/// Choice in streaming response
/// </summary>
public class OpenAiStreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("content_filter_results")]
    public OpenAiContentFilterResults? ContentFilterResults { get; set; }

    [JsonPropertyName("delta")]
    public OpenAiDelta Delta { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// Prompt filter result
/// </summary>
public class OpenAiPromptFilterResults
{
    [JsonPropertyName("prompt_index")]
    public long? PromptIndex { get; set; }

    [JsonPropertyName("content_filter_results")]
    public OpenAiContentFilterResults? ContentFilterResults { get; set; }
}

/// <summary>
/// Content filter results for prompt filtering
/// </summary>
public class OpenAiContentFilterResults
{
    [JsonPropertyName("hate")]
    public OpenAiContentFilterResultDetail? Hate { get; set; }

    [JsonPropertyName("self_harm")]
    public OpenAiContentFilterResultDetail? SelfHarm { get; set; }

    [JsonPropertyName("sexual")]
    public OpenAiContentFilterResultDetail? Sexual { get; set; }

    [JsonPropertyName("violence")]
    public OpenAiContentFilterResultDetail? Violence { get; set; }
}

/// <summary>
/// Results for individual content filter categories
/// </summary>
public class OpenAiContentFilterResultDetail
{
    [JsonPropertyName("filtered")]
    public bool Filtered { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }
}

/// <summary>
/// Delta for streaming updates
/// </summary>
public class OpenAiDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("refusal")]
    public string? Refusal { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<OpenAiStreamToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// Tool call in response
/// </summary>
public class OpenAiToolCall
{
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public OpenAiFunctionCall Function { get; set; } = new();
}

/// <summary>
/// Stream tool call (with optional index for chunked responses)
/// </summary>
public class OpenAiStreamToolCall
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public OpenAiFunctionCall? Function { get; set; }
}

/// <summary>
/// Function call details
/// </summary>
public class OpenAiFunctionCall
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

/// <summary>
/// Token usage information
/// </summary>
public class OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("prompt_tokens_details")]
    public OpenAiUsageDetails? PromptTokensDetails { get; set; }

    [JsonPropertyName("completion_tokens_details")]
    public OpenAiUsageDetails? CompletionTokensDetails { get; set; }
}

/// <summary>
/// Detailed token usage breakdown
/// </summary>
public class OpenAiUsageDetails
{
    [JsonPropertyName("cached_tokens")]
    public int? CachedTokens { get; set; }

    [JsonPropertyName("reasoning_tokens")]
    public int? ReasoningTokens { get; set; }

    [JsonPropertyName("accepted_prediction_tokens")]
    public int? AcceptedPredictionTokens { get; set; }

    [JsonPropertyName("rejected_prediction_tokens")]
    public int? RejectedPredictionTokens { get; set; }
}

/// <summary>
/// Error response from OpenAI API
/// </summary>
public class OpenAiError
{
    [JsonPropertyName("error")]
    public OpenAiErrorDetails Error { get; set; } = new();
}

/// <summary>
/// Error details
/// </summary>
public class OpenAiErrorDetails
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("param")]
    public string? Param { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}
