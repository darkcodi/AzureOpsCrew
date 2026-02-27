using System.ClientModel;
using System.ClientModel.Primitives;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AzureOpsCrew.Infrastructure.Ai.Extensions;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Serilog;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;
using OpenAIClientExtensions = AzureOpsCrew.Infrastructure.Ai.Extensions.OpenAIClientExtensions;

#pragma warning disable CA1308 // Normalize strings to uppercase
#pragma warning disable S3011 // Reflection should not be used to increase accessibility of classes, methods, or fields
#pragma warning disable SA1204 // Static elements should appear before instance elements
#pragma warning disable OPENAI001 // Endpoint and Model are experimental
#pragma warning disable CS9204 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SCME0001 // JsonPatch is experimental
namespace AzureOpsCrew.Infrastructure.Ai.ProviderServices;

/// <summary>Represents an <see cref="IChatClient"/> for an OpenAI <see cref="OpenAIClient"/> or <see cref="ChatClient"/>.</summary>
internal sealed partial class CustomOpenAIChatClient : IChatClient
{
    // These delegate instances are used to call the internal overloads of CompleteChatAsync and CompleteChatStreamingAsync that accept
    // a RequestOptions. These should be replaced once a better way to pass RequestOptions is available.
    private static readonly Func<ChatClient, IEnumerable<OpenAI.Chat.ChatMessage>, ChatCompletionOptions, RequestOptions, Task<ClientResult<ChatCompletion>>>?
        _completeChatAsync =
        (Func<ChatClient, IEnumerable<OpenAI.Chat.ChatMessage>, ChatCompletionOptions, RequestOptions, Task<ClientResult<ChatCompletion>>>?)
        typeof(ChatClient)
        .GetMethod(
            nameof(ChatClient.CompleteChatAsync), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, [typeof(IEnumerable<OpenAI.Chat.ChatMessage>), typeof(ChatCompletionOptions), typeof(RequestOptions)], null)
        ?.CreateDelegate(
            typeof(Func<ChatClient, IEnumerable<OpenAI.Chat.ChatMessage>, ChatCompletionOptions, RequestOptions, Task<ClientResult<ChatCompletion>>>));
    private static readonly Func<ChatClient, IEnumerable<OpenAI.Chat.ChatMessage>, ChatCompletionOptions, RequestOptions, AsyncCollectionResult<StreamingChatCompletionUpdate>>?
        _completeChatStreamingAsync =
        (Func<ChatClient, IEnumerable<OpenAI.Chat.ChatMessage>, ChatCompletionOptions, RequestOptions, AsyncCollectionResult<StreamingChatCompletionUpdate>>?)
        typeof(ChatClient)
        .GetMethod(
            nameof(ChatClient.CompleteChatStreamingAsync), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, [typeof(IEnumerable<OpenAI.Chat.ChatMessage>), typeof(ChatCompletionOptions), typeof(RequestOptions)], null)
        ?.CreateDelegate(
            typeof(Func<ChatClient, IEnumerable<OpenAI.Chat.ChatMessage>, ChatCompletionOptions, RequestOptions, AsyncCollectionResult<StreamingChatCompletionUpdate>>));

    /// <summary>Metadata about the client.</summary>
    private readonly ChatClientMetadata _metadata;

    /// <summary>The underlying <see cref="ChatClient" />.</summary>
    private readonly ChatClient _chatClient;

    /// <summary>Initializes a new instance of the <see cref="CustomOpenAIChatClient"/> class for the specified <see cref="ChatClient"/>.</summary>
    /// <param name="chatClient">The underlying client.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chatClient"/> is <see langword="null"/>.</exception>
    public CustomOpenAIChatClient(ChatClient chatClient)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));

        _metadata = new("openai", chatClient.Endpoint, _chatClient.Model);

        Serilog.Log.Information("[CustomOpenAIChatClient] Created. Endpoint={Endpoint}, Model={Model}", chatClient.Endpoint, _chatClient.Model);
    }

    /// <inheritdoc />
    object? IChatClient.GetService(Type serviceType, object? serviceKey)
    {
        if (serviceType is null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }

        return
            serviceKey is not null ? null :
            serviceType == typeof(ChatClientMetadata) ? _metadata :
            serviceType == typeof(ChatClient) ? _chatClient :
            serviceType.IsInstanceOfType(this) ? this :
            null;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (messages is null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        Log.Information("[GetResponseAsync] Starting request. MessageCount={MessageCount}", messages.Count());

        // Log request details
        LogRequestDetails(messages, options);

        var openAIChatMessages = ToOpenAIChatMessages(messages, options);
        var openAIOptions = ToOpenAIOptions(options);

        Log.Information("[GetResponseAsync] Sending request to OpenAI. Temperature={Temperature}, MaxTokens={MaxTokens}",
            openAIOptions?.Temperature, openAIOptions?.MaxOutputTokenCount);

        // Make the call to OpenAI.
        var task = _completeChatAsync is not null ?
            _completeChatAsync(_chatClient, openAIChatMessages, openAIOptions, cancellationToken.ToRequestOptions(streaming: false)) :
            _chatClient.CompleteChatAsync(openAIChatMessages, openAIOptions, cancellationToken);
        var response = await task.ConfigureAwait(false);

        Log.Information("[GetResponseAsync] Received response from OpenAI. ResponseId={ResponseId}, Role={Role}, FinishReason={FinishReason}, Model={Model}",
            response.Value.Id, response.Value.Role, response.Value.FinishReason, response.Value.Model);

        // Log EXTENSIVE response details
        LogResponseDetails(response.Value);

        var result = FromOpenAIChatCompletion(response.Value, openAIOptions);

        // Log final result details
        Log.Information("[GetResponseAsync] Returning ChatResponse. ResponseId={ResponseId}, FinishReason={FinishReason}, ModelId={ModelId}",
            result.ResponseId, result.FinishReason, result.ModelId);

        return result;
    }

    /// <summary>Logs detailed request information.</summary>
    private void LogRequestDetails(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        try
        {
            Log.Information("[LogRequestDetails] === REQUEST START ===");

            foreach (var msg in messages)
            {
                Log.Information("[LogRequestDetails] Message: Role={Role}, AuthorName={AuthorName}, CreatedAt={CreatedAt}",
                    msg.Role, msg.AuthorName, msg.CreatedAt);

                foreach (var content in msg.Contents)
                {
                    switch (content)
                    {
                        case TextContent textContent:
                            Log.Information("[LogRequestDetails]   TextContent: \"{Text}\"", Truncate(textContent.Text, 500));
                            break;
                        case FunctionCallContent funcCall:
                            Log.Information("[LogRequestDetails]   FunctionCallContent: Name={Name}, CallId={CallId}, Arguments={Arguments}",
                                funcCall.Name, funcCall.CallId, funcCall.Arguments);
                            break;
                        case FunctionResultContent funcResult:
                            Log.Information("[LogRequestDetails]   FunctionResultContent: CallId={CallId}, Result={Result}",
                                funcResult.CallId, Truncate(funcResult.Result?.ToString() ?? "null", 500));
                            break;
                        default:
                            Log.Information("[LogRequestDetails]   Content: Type={ContentType}, ToString={ToString}",
                                content.GetType().Name, content);
                            break;
                    }
                }
            }

            if (options is not null)
            {
                Log.Information("[LogRequestDetails] Options: ModelId={ModelId}, Temperature={Temperature}, MaxOutputTokens={MaxOutputTokens}, TopP={TopP}, PresencePenalty={PresencePenalty}, FrequencyPenalty={FrequencyPenalty}",
                    options.ModelId, options.Temperature, options.MaxOutputTokens, options.TopP, options.PresencePenalty, options.FrequencyPenalty);

                if (options.Tools is { Count: > 0 })
                {
                    Log.Information("[LogRequestDetails] Tools: Count={ToolCount}", options.Tools.Count);
                    foreach (var tool in options.Tools)
                    {
                        if (tool is AIFunctionDeclaration funcDecl)
                        {
                            Log.Information("[LogRequestDetails]   Tool: Name={Name}, Description={Description}",
                                funcDecl.Name, funcDecl.Description);
                        }
                    }
                }

                if (options.ToolMode is not null)
                {
                    Log.Information("[LogRequestDetails] ToolMode: {ToolMode}", options.ToolMode);
                }

                if (options.Reasoning is not null)
                {
                    Log.Information("[LogRequestDetails] Reasoning: Effort={Effort}", options.Reasoning.Effort);
                }
            }

            Log.Information("[LogRequestDetails] === REQUEST END ===");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[LogRequestDetails] Error logging request details");
        }
    }

    /// <summary>Logs EXTENSIVE response details including all content and reasoning.</summary>
    private void LogResponseDetails(ChatCompletion completion)
    {
        try
        {
            Log.Information("[LogResponseDetails] === RESPONSE START ===");
            Log.Information("[LogResponseDetails] Completion: Id={Id}, CreatedAt={CreatedAt}, Model={Model}, Role={Role}, FinishReason={FinishReason}",
                completion.Id, completion.CreatedAt, completion.Model, completion.Role, completion.FinishReason);

            // Log all content parts - THIS IS WHERE REASONING TYPICALLY APPEARS
            Log.Information("[LogResponseDetails] Content Parts: Count={Count}", completion.Content.Count);

            foreach (var contentPart in completion.Content)
            {
                Log.Information("[LogResponseDetails]   ContentPart: Kind={Kind}, Text=\"{Text}\", ImageUri={ImageUri}, FileId={FileId}",
                    contentPart.Kind, Truncate(contentPart.Text, 1000), contentPart.ImageUri, contentPart.FileId);

                // Log any refusal in content parts
                if (!string.IsNullOrEmpty(contentPart.Refusal))
                {
                    Log.Warning("[LogResponseDetails]   ContentPart.Refusal: \"{Refusal}\"", contentPart.Refusal);
                }
            }

            // Log raw JSON representation of content for debugging
            try
            {
                var contentJson = JsonSerializer.Serialize(completion.Content);
                Log.Information("[LogResponseDetails] Raw Content JSON: {ContentJson}", Truncate(contentJson, 2000));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[LogResponseDetails] Could not serialize content to JSON");
            }

            // Log tool calls
            if (completion.ToolCalls.Count > 0)
            {
                Log.Information("[LogResponseDetails] Tool Calls: Count={Count}", completion.ToolCalls.Count);
                foreach (var toolCall in completion.ToolCalls)
                {
                    Log.Information("[LogResponseDetails]   ToolCall: Id={Id}, FunctionName={FunctionName}, FunctionArguments={Arguments}",
                        toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments);
                }
            }

            // Log refusal at message level
            if (!string.IsNullOrEmpty(completion.Refusal))
            {
                Log.Warning("[LogResponseDetails] Message Refusal: \"{Refusal}\"", completion.Refusal);
            }

            // Log annotations (citations, etc.)
            if (completion.Annotations.Count > 0)
            {
                Log.Information("[LogResponseDetails] Annotations: Count={Count}", completion.Annotations.Count);
                foreach (var annotation in completion.Annotations)
                {
                    Log.Information("[LogResponseDetails]   Annotation: StartIndex={StartIndex}, EndIndex={EndIndex}, Title={Title}, Url={Url}",
                        annotation.StartIndex, annotation.EndIndex, annotation.WebResourceTitle, annotation.WebResourceUri);
                }
            }

            // Log usage/tokens
            if (completion.Usage is not null)
            {
                Log.Information("[LogResponseDetails] Usage: InputTokens={InputTokens}, OutputTokens={OutputTokens}, TotalTokens={TotalTokens}, CachedTokens={CachedTokens}, ReasoningTokens={ReasoningTokens}",
                    completion.Usage.InputTokenCount, completion.Usage.OutputTokenCount, completion.Usage.TotalTokenCount,
                    completion.Usage.InputTokenDetails?.CachedTokenCount, completion.Usage.OutputTokenDetails?.ReasoningTokenCount);

                if (completion.Usage.OutputTokenDetails is not null)
                {
                    Log.Information("[LogResponseDetails] OutputTokenDetails: ReasoningTokens={ReasoningTokens}, AcceptedPredictionTokens={AcceptedPredictionTokens}, RejectedPredictionTokens={RejectedPredictionTokens}, AudioTokens={AudioTokens}",
                        completion.Usage.OutputTokenDetails.ReasoningTokenCount,
                        completion.Usage.OutputTokenDetails.AcceptedPredictionTokenCount,
                        completion.Usage.OutputTokenDetails.RejectedPredictionTokenCount,
                        completion.Usage.OutputTokenDetails.AudioTokenCount);
                }
            }

            // Log output audio if present
            if (completion.OutputAudio is not null)
            {
                Log.Information("[LogResponseDetails] OutputAudio: ByteCount={ByteCount}",
                    completion.OutputAudio.AudioBytes?.Length ?? 0);
            }

            // Try to serialize the completion to JSON for debugging
            try
            {
                // Try to get raw JSON through ModelReaderWriter if available
                var rawJson = completion.ToString();
                Log.Information("[LogResponseDetails] Completion ToString: {RawJson}", Truncate(rawJson, 5000));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[LogResponseDetails] Could not serialize completion to string");
            }

            Log.Information("[LogResponseDetails] === RESPONSE END ===");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[LogResponseDetails] Error logging response details");
        }
    }

    /// <summary>Truncates a string to a maximum length for logging.</summary>
    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (messages is null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        Log.Information("[GetStreamingResponseAsync] Starting streaming request. MessageCount={MessageCount}", messages.Count());

        // Log request details
        LogRequestDetails(messages, options);

        var openAIChatMessages = ToOpenAIChatMessages(messages, options);
        var openAIOptions = ToOpenAIOptions(options);

        Log.Information("[GetStreamingResponseAsync] Sending streaming request to OpenAI. Temperature={Temperature}, MaxTokens={MaxTokens}",
            openAIOptions?.Temperature, openAIOptions?.MaxOutputTokenCount);

        // Make the call to OpenAI.
        var chatCompletionUpdates = _completeChatStreamingAsync is not null ?
            _completeChatStreamingAsync(_chatClient, openAIChatMessages, openAIOptions, cancellationToken.ToRequestOptions(streaming: true)) :
            _chatClient.CompleteChatStreamingAsync(openAIChatMessages, openAIOptions, cancellationToken);

        return FromOpenAIStreamingChatCompletionAsync(chatCompletionUpdates, openAIOptions, cancellationToken);
    }

    /// <inheritdoc />
    void IDisposable.Dispose()
    {
        // Nothing to dispose. Implementation required for the IChatClient interface.
    }

    /// <summary>Converts an Extensions function to an OpenAI chat tool.</summary>
    internal static ChatTool ToOpenAIChatTool(AIFunctionDeclaration aiFunction, ChatOptions? options = null)
    {
        bool? strict =
            OpenAIClientExtensions.HasStrict(aiFunction.AdditionalProperties) ??
            OpenAIClientExtensions.HasStrict(options?.AdditionalProperties);

        return ChatTool.CreateFunctionTool(
            aiFunction.Name,
            aiFunction.Description,
            OpenAIClientExtensions.ToOpenAIFunctionParameters(aiFunction, strict),
            strict);
    }

    /// <summary>Converts an Extensions chat message enumerable to an OpenAI chat message enumerable.</summary>
    internal static IEnumerable<OpenAI.Chat.ChatMessage> ToOpenAIChatMessages(IEnumerable<ChatMessage> inputs, ChatOptions? chatOptions)
    {
        // Maps all of the M.E.AI types to the corresponding OpenAI types.
        // Unrecognized or non-processable content is ignored.

        if (chatOptions?.Instructions is { } instructions && !string.IsNullOrWhiteSpace(instructions))
        {
            yield return new SystemChatMessage(instructions);
        }

        foreach (ChatMessage input in inputs)
        {
            if (input.RawRepresentation is OpenAI.Chat.ChatMessage raw)
            {
                yield return raw;
                continue;
            }

            if (input.Role == ChatRole.System ||
                input.Role == ChatRole.User ||
                input.Role == OpenAIClientExtensions.ChatRoleDeveloper)
            {
                var parts = ToOpenAIChatContent(input.Contents);
                string? name = SanitizeAuthorName(input.AuthorName);
                yield return
                    input.Role == ChatRole.System ? new SystemChatMessage(parts) { ParticipantName = name } :
                    input.Role == OpenAIClientExtensions.ChatRoleDeveloper ? new DeveloperChatMessage(parts) { ParticipantName = name } :
                    new UserChatMessage(parts) { ParticipantName = name };
            }
            else if (input.Role == ChatRole.Tool)
            {
                foreach (AIContent item in input.Contents)
                {
                    if (item is FunctionResultContent resultContent)
                    {
                        string? result = resultContent.Result as string;
                        if (result is null && resultContent.Result is not null)
                        {
                            try
                            {
                                result = JsonSerializer.Serialize(resultContent.Result, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(object)));
                            }
                            catch (NotSupportedException)
                            {
                                // If the type can't be serialized, skip it.
                            }
                        }

                        yield return new ToolChatMessage(resultContent.CallId, result ?? string.Empty);
                    }
                }
            }
            else if (input.Role == ChatRole.Assistant)
            {
                List<ChatMessageContentPart>? contentParts = null;
                List<ChatToolCall>? toolCalls = null;
                string? refusal = null;
                foreach (var content in input.Contents)
                {
                    switch (content)
                    {
                        case ErrorContent ec when ec.ErrorCode == nameof(AssistantChatMessage.Refusal):
                            refusal = ec.Message;
                            break;

                        case FunctionCallContent fc:
                            (toolCalls ??= []).Add(
                                ChatToolCall.CreateFunctionToolCall(fc.CallId, fc.Name, new(JsonSerializer.SerializeToUtf8Bytes(
                                    fc.Arguments, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(IDictionary<string, object?>))))));
                            break;

                        default:
                            if (ToChatMessageContentPart(content) is { } part)
                            {
                                (contentParts ??= []).Add(part);
                            }

                            break;
                    }
                }

                AssistantChatMessage message;
                if (contentParts is not null)
                {
                    message = new(contentParts);
                    if (toolCalls is not null)
                    {
                        foreach (var toolCall in toolCalls)
                        {
                            message.ToolCalls.Add(toolCall);
                        }
                    }
                }
                else
                {
                    message = toolCalls is not null ?
                        new(toolCalls) :
                        new(ChatMessageContentPart.CreateTextPart(string.Empty));
                }

                message.ParticipantName = SanitizeAuthorName(input.AuthorName);
                message.Refusal = refusal;

                yield return message;
            }
        }
    }

    /// <summary>Converts a list of <see cref="AIContent"/> to a list of <see cref="ChatMessageContentPart"/>.</summary>
    internal static List<ChatMessageContentPart> ToOpenAIChatContent(IEnumerable<AIContent> contents)
    {
        List<ChatMessageContentPart> parts = [];

        foreach (var content in contents)
        {
            if (content.RawRepresentation is ChatMessageContentPart raw)
            {
                parts.Add(raw);
            }
            else
            {
                if (ToChatMessageContentPart(content) is { } part)
                {
                    parts.Add(part);
                }
            }
        }

        if (parts.Count == 0)
        {
            parts.Add(ChatMessageContentPart.CreateTextPart(string.Empty));
        }

        return parts;
    }

    private static ChatMessageContentPart? ToChatMessageContentPart(AIContent content)
    {
        switch (content)
        {
            case AIContent when content.RawRepresentation is ChatMessageContentPart rawContentPart:
                return rawContentPart;

            case TextContent textContent:
                return ChatMessageContentPart.CreateTextPart(textContent.Text);

            case UriContent uriContent when uriContent.HasTopLevelMediaType("image"):
                return ChatMessageContentPart.CreateImagePart(uriContent.Uri, GetImageDetail(content));

            case DataContent dataContent when dataContent.HasTopLevelMediaType("image"):
                return ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(dataContent.Data), dataContent.MediaType, GetImageDetail(content));

#pragma warning disable OPENAI001 // Audio and file content parts are experimental
            case DataContent dataContent when dataContent.HasTopLevelMediaType("audio"):
                var audioData = BinaryData.FromBytes(dataContent.Data);
                if (dataContent.MediaType.Equals("audio/mpeg", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Mp3);
                }
                else if (dataContent.MediaType.Equals("audio/wav", StringComparison.OrdinalIgnoreCase))
                {
                    return ChatMessageContentPart.CreateInputAudioPart(audioData, ChatInputAudioFormat.Wav);
                }

                break;

            case DataContent dataContent when dataContent.MediaType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase):
                return ChatMessageContentPart.CreateFilePart(BinaryData.FromBytes(dataContent.Data), dataContent.MediaType, dataContent.Name ?? $"{Guid.NewGuid():N}.pdf");

            case HostedFileContent fileContent:
                return ChatMessageContentPart.CreateFilePart(fileContent.FileId);
#pragma warning restore OPENAI001
        }

        return null;
    }

    private static ChatImageDetailLevel? GetImageDetail(AIContent content)
    {
        if (content.AdditionalProperties?.TryGetValue("detail", out object? value) is true)
        {
            return value switch
            {
                string detailString => new ChatImageDetailLevel(detailString),
                ChatImageDetailLevel detail => detail,
                _ => null
            };
        }

        return null;
    }

    [Experimental("OPENAI001")]
    internal async IAsyncEnumerable<ChatResponseUpdate> FromOpenAIStreamingChatCompletionAsync(
        IAsyncEnumerable<StreamingChatCompletionUpdate> updates,
        ChatCompletionOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] Starting to process streaming updates");

        Dictionary<int, FunctionCallInfo>? functionCallInfos = null;
        ChatRole? streamedRole = null;
        ChatFinishReason? finishReason = null;
        StringBuilder? refusal = null;
        string? responseId = null;
        DateTimeOffset? createdAt = null;
        string? modelId = null;
        var updateCount = 0;
        var fullContentBuilder = new StringBuilder();
        var allContentParts = new List<string>();

        // Process each update as it arrives
        await foreach (StreamingChatCompletionUpdate update in updates.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            updateCount++;

            Log.Information("[FromOpenAIStreamingChatCompletionAsync] Update #{UpdateCount}: CompletionId={CompletionId}, Role={Role}, FinishReason={FinishReason}, Model={Model}",
                updateCount, update.CompletionId, update.Role, update.FinishReason, update.Model);

            Log.Information("LogUpdate: {Data}", JsonSerializer.Serialize(update));

            // Log content update - THIS IS CRITICAL FOR CATCHING REASONING
            if (update.ContentUpdate is { Count: > 0 })
            {
                Log.Information("[FromOpenAIStreamingChatCompletionAsync] ContentUpdate: Count={Count}", update.ContentUpdate.Count);
                foreach (var contentPart in update.ContentUpdate)
                {
                    var text = contentPart.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        allContentParts.Add(text);
                        fullContentBuilder.Append(text);
                        Log.Information("[FromOpenAIStreamingChatCompletionAsync]   ContentPart: Kind={Kind}, Text=\"{Text}\"",
                            contentPart.Kind, Truncate(text, 500));

                        // Check for reasoning indicators in the text
                        var lowerText = text.ToLowerInvariant();
                        if (lowerText.Contains("reason") || lowerText.Contains("think") || lowerText.Contains("thought") ||
                            lowerText.Contains("consider") || lowerText.Contains("analyze") || lowerText.Contains("because"))
                        {
                            Log.Information("[FromOpenAIStreamingChatCompletionAsync]   *** POTENTIAL REASONING DETECTED in content: \"{Text}\"", Truncate(text, 200));
                        }
                    }
                    else
                    {
                        Log.Information("[FromOpenAIStreamingChatCompletionAsync]   ContentPart: Kind={Kind}, HasImageUri={HasImageUri}, HasImageBytes={HasImageBytes}, FileId={FileId}",
                            contentPart.Kind, contentPart.ImageUri is not null, contentPart.ImageBytes is not null, contentPart.FileId);

                        // Check for refusal in content part
                        if (!string.IsNullOrEmpty(contentPart.Refusal))
                        {
                            Log.Warning("[FromOpenAIStreamingChatCompletionAsync]   *** REFUSAL in ContentPart: \"{Refusal}\"", contentPart.Refusal);
                        }
                    }
                }
            }

            // Log refusal update at update level
            if (update.RefusalUpdate is not null)
            {
                Log.Warning("[FromOpenAIStreamingChatCompletionAsync] *** REFUSAL UPDATE: \"{Refusal}\"", update.RefusalUpdate);
            }

            // Log tool call updates
            if (update.ToolCallUpdates is { Count: > 0 })
            {
                Log.Information("[FromOpenAIStreamingChatCompletionAsync] ToolCallUpdates: Count={Count}", update.ToolCallUpdates.Count);
                foreach (var toolCallUpdate in update.ToolCallUpdates)
                {
                    Log.Information("[FromOpenAIStreamingChatCompletionAsync]   ToolCall: Index={Index}, ToolCallId={ToolCallId}, FunctionName={FunctionName}, HasArgumentsUpdate={HasArguments}",
                        toolCallUpdate.Index, toolCallUpdate.ToolCallId, toolCallUpdate.FunctionName,
                        toolCallUpdate.FunctionArgumentsUpdate is not null);

                    if (toolCallUpdate.FunctionArgumentsUpdate is { } argUpdate && !argUpdate.ToMemory().IsEmpty)
                    {
                        Log.Information("[FromOpenAIStreamingChatCompletionAsync]     Arguments: {Arguments}", Truncate(argUpdate.ToString(), 500));
                    }
                }
            }

            // Log usage updates
            if (update.Usage is ChatTokenUsage usageLog)
            {
                Log.Information("[FromOpenAIStreamingChatCompletionAsync] Usage: InputTokens={InputTokens}, OutputTokens={OutputTokens}, TotalTokens={TotalTokens}, ReasoningTokens={ReasoningTokens}",
                    usageLog.InputTokenCount, usageLog.OutputTokenCount, usageLog.TotalTokenCount,
                    usageLog.OutputTokenDetails?.ReasoningTokenCount);
            }

            // Log audio update
            if (update.OutputAudioUpdate is { } audioLog)
            {
                Log.Information("[FromOpenAIStreamingChatCompletionAsync] OutputAudioUpdate: ByteCount={ByteCount}",
                    audioLog.AudioBytesUpdate?.Length ?? 0);
            }

            // The role and finish reason may arrive during any update, but once they've arrived, the same value should be the same for all subsequent updates.
            streamedRole ??= update.Role is ChatMessageRole role ? FromOpenAIChatRole(role) : null;
            finishReason ??= update.FinishReason is OpenAI.Chat.ChatFinishReason reason ? FromOpenAIFinishReason(reason) : null;
            responseId ??= update.CompletionId;
            createdAt ??= update.CreatedAt;
            modelId ??= update.Model;

            // Create the response content object.
            ChatResponseUpdate responseUpdate = new()
            {
                ResponseId = update.CompletionId,
                MessageId = update.CompletionId, // There is no per-message ID, but there's only one message per response, so use the response ID
                CreatedAt = update.CreatedAt,
                FinishReason = finishReason,
                ModelId = modelId,
                RawRepresentation = update,
                Role = streamedRole,
            };

            // Transfer over content update items.
            if (update.ContentUpdate is { Count: > 0 })
            {
                ConvertContentParts(update.ContentUpdate, responseUpdate.Contents);
            }

            if (update.OutputAudioUpdate is { } audioUpdate)
            {
                responseUpdate.Contents.Add(new DataContent(audioUpdate.AudioBytesUpdate.ToMemory(), GetOutputAudioMimeType(options))
                {
                    RawRepresentation = audioUpdate,
                });
            }

            // Transfer over refusal updates.
            if (update.RefusalUpdate is not null)
            {
                _ = (refusal ??= new()).Append(update.RefusalUpdate);
            }

            // Transfer over tool call updates.
            if (update.ToolCallUpdates is { Count: > 0 } toolCallUpdates)
            {
                foreach (StreamingChatToolCallUpdate toolCallUpdate in toolCallUpdates)
                {
                    functionCallInfos ??= [];
                    if (!functionCallInfos.TryGetValue(toolCallUpdate.Index, out FunctionCallInfo? existing))
                    {
                        functionCallInfos[toolCallUpdate.Index] = existing = new();
                    }

                    existing.CallId ??= toolCallUpdate.ToolCallId;
                    existing.Name ??= toolCallUpdate.FunctionName;
                    if (toolCallUpdate.FunctionArgumentsUpdate is { } argUpdate && !argUpdate.ToMemory().IsEmpty)
                    {
                        _ = (existing.Arguments ??= new()).Append(argUpdate.ToString());
                    }
                }
            }

            // Transfer over usage updates.
            if (update.Usage is ChatTokenUsage tokenUsage)
            {
                responseUpdate.Contents.Add(new UsageContent(FromOpenAIUsage(tokenUsage))
                {
                    RawRepresentation = tokenUsage,
                });
            }

            // Now yield the item.
            yield return responseUpdate;
        }

        // Log summary of streaming
        var fullContent = fullContentBuilder.ToString();
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] === STREAMING SUMMARY ===");
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] Total Updates: {UpdateCount}", updateCount);
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] ResponseId: {ResponseId}", responseId);
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] Model: {Model}", modelId);
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] Role: {Role}", streamedRole);
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] FinishReason: {FinishReason}", finishReason);
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] Full Content Length: {ContentLength} chars", fullContent.Length);
        Log.Information("[FromOpenAIStreamingChatCompletionAsync] Full Content: \"{FullContent}\"", Truncate(fullContent, 2000));

        // Check for reasoning in full content
        var lowerFullContent = fullContent.ToLowerInvariant();
        if (lowerFullContent.Contains("reason") || lowerFullContent.Contains("think") || lowerFullContent.Contains("thought") ||
            lowerFullContent.Contains("consider") || lowerFullContent.Contains("analyze") || lowerFullContent.Contains("because") ||
            lowerFullContent.Contains("let me") || lowerFullContent.Contains("i should") || lowerFullContent.Contains("first"))
        {
            Log.Information("[FromOpenAIStreamingChatCompletionAsync] *** REASONING INDICATORS DETECTED IN FULL CONTENT ***");
            Log.Information("[FromOpenAIStreamingChatCompletionAsync] *** Content: {Content}***", Truncate(fullContent, 3000));
        }

        // Log refusal if present
        if (refusal is not null)
        {
            Log.Warning("[FromOpenAIStreamingChatCompletionAsync] *** REFUSAL: \"{Refusal}\"", refusal.ToString());
        }

        // Log function calls
        if (functionCallInfos is not null)
        {
            Log.Information("[FromOpenAIStreamingChatCompletionAsync] Function Calls: Count={Count}", functionCallInfos.Count);
            foreach (var entry in functionCallInfos)
            {
                Log.Information("[FromOpenAIStreamingChatCompletionAsync]   FunctionCall: Index={Index}, CallId={CallId}, Name={Name}, Arguments={Arguments}",
                    entry.Key, entry.Value.CallId, entry.Value.Name, entry.Value.Arguments?.ToString() ?? string.Empty);
            }
        }

        // Now that we've received all updates, combine any for function calls into a single item to yield.
        if (functionCallInfos is not null)
        {
            ChatResponseUpdate responseUpdate = new()
            {
                ResponseId = responseId,
                MessageId = responseId, // There is no per-message ID, but there's only one message per response, so use the response ID
                CreatedAt = createdAt,
                FinishReason = finishReason,
                ModelId = modelId,
                Role = streamedRole,
            };

            foreach (var entry in functionCallInfos)
            {
                FunctionCallInfo fci = entry.Value;
                if (!string.IsNullOrWhiteSpace(fci.Name))
                {
                    var callContent = OpenAIClientExtensions.ParseCallContent(
                        fci.Arguments?.ToString() ?? string.Empty,
                        fci.CallId!,
                        fci.Name!);
                    responseUpdate.Contents.Add(callContent);
                }
            }

            // Refusals are about the model not following the schema for tool calls. As such, if we have any refusal,
            // add it to this function calling item.
            if (refusal is not null)
            {
                responseUpdate.Contents.Add(new ErrorContent(refusal.ToString()) { ErrorCode = "Refusal" });
            }

            yield return responseUpdate;
        }

        Log.Information("[FromOpenAIStreamingChatCompletionAsync] === END OF STREAMING ===");
    }

    [Experimental("OPENAI001")]
    private static string GetOutputAudioMimeType(ChatCompletionOptions? options) =>
        options?.AudioOptions?.OutputAudioFormat.ToString()?.ToLowerInvariant() switch
        {
            "opus" => "audio/opus",
            "aac" => "audio/aac",
            "flac" => "audio/flac",
            "wav" => "audio/wav",
            "pcm" => "audio/pcm",
            "mp3" or _ => "audio/mpeg",
        };

    [Experimental("OPENAI001")]
    internal ChatResponse FromOpenAIChatCompletion(ChatCompletion openAICompletion, ChatCompletionOptions? chatCompletionOptions)
    {
        if (openAICompletion is null)
        {
            throw new ArgumentNullException(nameof(openAICompletion));
        }

        Log.Information("[FromOpenAIChatCompletion] === CONVERSION START ===");
        Log.Information("[FromOpenAIChatCompletion] Completion: Id={Id}, CreatedAt={CreatedAt}, Model={Model}, Role={Role}, FinishReason={FinishReason}",
            openAICompletion.Id, openAICompletion.CreatedAt, openAICompletion.Model, openAICompletion.Role, openAICompletion.FinishReason);

        // Create the return message.
        ChatMessage returnMessage = new()
        {
            CreatedAt = openAICompletion.CreatedAt,
            MessageId = openAICompletion.Id, // There's no per-message ID, so we use the same value as the response ID
            RawRepresentation = openAICompletion,
            Role = FromOpenAIChatRole(openAICompletion.Role),
        };

        // Log and populate its content from those in the OpenAI response content.
        Log.Information("[FromOpenAIChatCompletion] Processing Content Parts: Count={Count}", openAICompletion.Content.Count);
        foreach (ChatMessageContentPart contentPart in openAICompletion.Content)
        {
            Log.Information("[FromOpenAIChatCompletion]   ContentPart: Kind={Kind}, Text=\"{Text}\", ImageUri={ImageUri}, FileId={FileId}, Refusal=\"{Refusal}\"",
                contentPart.Kind, Truncate(contentPart.Text, 500), contentPart.ImageUri, contentPart.FileId, contentPart.Refusal);

            if (!string.IsNullOrEmpty(contentPart.Refusal))
            {
                Log.Warning("[FromOpenAIChatCompletion]   *** REFUSAL in ContentPart: {Refusal}", contentPart.Refusal);
            }

            if (ToAIContent(contentPart) is AIContent aiContent)
            {
                returnMessage.Contents.Add(aiContent);
                Log.Information("[FromOpenAIChatCompletion]   -> Added AIContent: Type={Type}", aiContent.GetType().Name);
            }
        }

        // Output audio is handled separately from message content parts.
        if (openAICompletion.OutputAudio is ChatOutputAudio audio)
        {
            Log.Information("[FromOpenAIChatCompletion] OutputAudio: ByteCount={ByteCount}",
                audio.AudioBytes?.Length ?? 0);
            returnMessage.Contents.Add(new DataContent(audio.AudioBytes.ToMemory(), GetOutputAudioMimeType(chatCompletionOptions))
            {
                RawRepresentation = audio,
            });
        }

        // Also manufacture function calling content items from any tool calls in the response.
        Log.Information("[FromOpenAIChatCompletion] Processing Tool Calls: Count={Count}", openAICompletion.ToolCalls.Count);
        foreach (ChatToolCall toolCall in openAICompletion.ToolCalls)
        {
            Log.Information("[FromOpenAIChatCompletion]   ToolCall: Id={Id}, FunctionName={FunctionName}, FunctionArguments={Arguments}",
                toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments);

            if (!string.IsNullOrWhiteSpace(toolCall.FunctionName))
            {
                var callContent = OpenAIClientExtensions.ParseCallContent(toolCall.FunctionArguments, toolCall.Id, toolCall.FunctionName);
                callContent.RawRepresentation = toolCall;

                returnMessage.Contents.Add(callContent);
            }
        }

        // And add error content for any refusals, which represent errors in generating output that conforms to a provided schema.
        if (openAICompletion.Refusal is string refusal)
        {
            Log.Warning("[FromOpenAIChatCompletion] *** MESSAGE REFUSAL: {Refusal}", refusal);
            returnMessage.Contents.Add(new ErrorContent(refusal) { ErrorCode = nameof(openAICompletion.Refusal) });
        }

        // And add annotations. OpenAI chat completion specifies annotations at the message level (and as such they can't be
        // roundtripped back); we store them either on the first text content, assuming there is one, or on a dedicated content
        // instance if not.
        if (openAICompletion.Annotations is { Count: > 0 })
        {
            Log.Information("[FromOpenAIChatCompletion] Annotations: Count={Count}", openAICompletion.Annotations.Count);
            TextContent? annotationContent = returnMessage.Contents.OfType<TextContent>().FirstOrDefault();
            if (annotationContent is null)
            {
                annotationContent = new(null);
                returnMessage.Contents.Add(annotationContent);
            }

            foreach (var annotation in openAICompletion.Annotations)
            {
                Log.Information("[FromOpenAIChatCompletion]   Annotation: StartIndex={StartIndex}, EndIndex={EndIndex}, Title={Title}, Url={Url}",
                    annotation.StartIndex, annotation.EndIndex, annotation.WebResourceTitle, annotation.WebResourceUri);
                (annotationContent.Annotations ??= []).Add(new CitationAnnotation
                {
                    RawRepresentation = annotation,
                    AnnotatedRegions = [new TextSpanAnnotatedRegion { StartIndex = annotation.StartIndex, EndIndex = annotation.EndIndex }],
                    Title = annotation.WebResourceTitle,
                    Url = annotation.WebResourceUri,
                });
            }
        }

        // Wrap the content in a ChatResponse to return.
        var response = new ChatResponse(returnMessage)
        {
            CreatedAt = openAICompletion.CreatedAt,
            FinishReason = FromOpenAIFinishReason(openAICompletion.FinishReason),
            ModelId = openAICompletion.Model,
            RawRepresentation = openAICompletion,
            ResponseId = openAICompletion.Id,
        };

        if (openAICompletion.Usage is ChatTokenUsage tokenUsage)
        {
            response.Usage = FromOpenAIUsage(tokenUsage);
            Log.Information("[FromOpenAIChatCompletion] Usage: InputTokens={InputTokens}, OutputTokens={OutputTokens}, TotalTokens={TotalTokens}, ReasoningTokens={ReasoningTokens}",
                tokenUsage.InputTokenCount, tokenUsage.OutputTokenCount, tokenUsage.TotalTokenCount,
                tokenUsage.OutputTokenDetails?.ReasoningTokenCount);
        }

        Log.Information("[FromOpenAIChatCompletion] Returning ChatResponse: ResponseId={ResponseId}, FinishReason={FinishReason}, ModelId={ModelId}",
            response.ResponseId, response.FinishReason, response.ModelId);
        Log.Information("[FromOpenAIChatCompletion] === CONVERSION END ===");

        return response;
    }

    /// <summary>Converts an extensions options instance to an OpenAI options instance.</summary>
    [Experimental("OPENAI001")]
    private ChatCompletionOptions ToOpenAIOptions(ChatOptions? options)
    {
        if (options is null)
        {
            return new();
        }

        if (options.RawRepresentationFactory?.Invoke(this) is not ChatCompletionOptions result)
        {
            result = new();
        }

        result.FrequencyPenalty ??= options.FrequencyPenalty;
        result.MaxOutputTokenCount ??= options.MaxOutputTokens;
        result.TopP ??= options.TopP;
        result.PresencePenalty ??= options.PresencePenalty;
        result.Temperature ??= options.Temperature;

        result.Seed ??= options.Seed;
        result.ReasoningEffortLevel ??= ToOpenAIChatReasoningEffortLevel(options.Reasoning?.Effort);

        OpenAIClientExtensions.PatchModelIfNotSet(ref result.Patch, options.ModelId);

        if (options.StopSequences is { Count: > 0 } stopSequences)
        {
            foreach (string stopSequence in stopSequences)
            {
                result.StopSequences.Add(stopSequence);
            }
        }

        if (options.Tools is { Count: > 0 } tools)
        {
            foreach (AITool tool in tools)
            {
                if (tool is AIFunctionDeclaration af)
                {
                    result.Tools.Add(ToOpenAIChatTool(af, options));
                }
            }

            if (result.Tools.Count > 0)
            {
                result.AllowParallelToolCalls ??= options.AllowMultipleToolCalls;
            }

            if (result.ToolChoice is null && result.Tools.Count > 0)
            {
                switch (options.ToolMode)
                {
                    case NoneChatToolMode:
                        result.ToolChoice = ChatToolChoice.CreateNoneChoice();
                        break;

                    case AutoChatToolMode:
                    case null:
                        result.ToolChoice = ChatToolChoice.CreateAutoChoice();
                        break;

                    case RequiredChatToolMode required:
                        result.ToolChoice = required.RequiredFunctionName is null ?
                            ChatToolChoice.CreateRequiredChoice() :
                            ChatToolChoice.CreateFunctionChoice(required.RequiredFunctionName);
                        break;
                }
            }
        }

        result.ResponseFormat ??= ToOpenAIChatResponseFormat(options.ResponseFormat, options);

        return result;
    }

    internal static OpenAI.Chat.ChatResponseFormat? ToOpenAIChatResponseFormat(ChatResponseFormat? format, ChatOptions? options) =>
        format switch
        {
            ChatResponseFormatText => OpenAI.Chat.ChatResponseFormat.CreateTextFormat(),

            ChatResponseFormatJson jsonFormat when OpenAIClientExtensions.StrictSchemaTransformCache.GetOrCreateTransformedSchema(jsonFormat) is { } jsonSchema =>
                 OpenAI.Chat.ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonFormat.SchemaName ?? "json_schema",
                    BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(jsonSchema, OpenAIJsonContext.Default.JsonElement)),
                    jsonFormat.SchemaDescription,
                    OpenAIClientExtensions.HasStrict(options?.AdditionalProperties)),

            ChatResponseFormatJson => OpenAI.Chat.ChatResponseFormat.CreateJsonObjectFormat(),

            _ => null
        };

    [Experimental("OPENAI001")]
    private static ChatReasoningEffortLevel? ToOpenAIChatReasoningEffortLevel(ReasoningEffort? effort) =>
        effort switch
        {
            ReasoningEffort.Low => ChatReasoningEffortLevel.Low,
            ReasoningEffort.Medium => ChatReasoningEffortLevel.Medium,
            ReasoningEffort.High => ChatReasoningEffortLevel.High,
            ReasoningEffort.ExtraHigh => ChatReasoningEffortLevel.High,
            _ => (ChatReasoningEffortLevel?)null,
        };

    [Experimental("OPENAI001")]
    private static UsageDetails FromOpenAIUsage(ChatTokenUsage tokenUsage)
    {
        var destination = new UsageDetails
        {
            InputTokenCount = tokenUsage.InputTokenCount,
            OutputTokenCount = tokenUsage.OutputTokenCount,
            TotalTokenCount = tokenUsage.TotalTokenCount,
            CachedInputTokenCount = tokenUsage.InputTokenDetails?.CachedTokenCount,
            ReasoningTokenCount = tokenUsage.OutputTokenDetails?.ReasoningTokenCount,
            AdditionalCounts = [],
        };

        var counts = destination.AdditionalCounts;

        if (tokenUsage.InputTokenDetails is ChatInputTokenUsageDetails inputDetails)
        {
            const string InputDetails = nameof(ChatTokenUsage.InputTokenDetails);
            counts.Add($"{InputDetails}.{nameof(ChatInputTokenUsageDetails.AudioTokenCount)}", inputDetails.AudioTokenCount);
        }

        if (tokenUsage.OutputTokenDetails is ChatOutputTokenUsageDetails outputDetails)
        {
            const string OutputDetails = nameof(ChatTokenUsage.OutputTokenDetails);
            counts.Add($"{OutputDetails}.{nameof(ChatOutputTokenUsageDetails.AudioTokenCount)}", outputDetails.AudioTokenCount);

            counts.Add($"{OutputDetails}.{nameof(ChatOutputTokenUsageDetails.AcceptedPredictionTokenCount)}", outputDetails.AcceptedPredictionTokenCount);
            counts.Add($"{OutputDetails}.{nameof(ChatOutputTokenUsageDetails.RejectedPredictionTokenCount)}", outputDetails.RejectedPredictionTokenCount);
        }

        return destination;
    }

    /// <summary>Converts an OpenAI role to an Extensions role.</summary>
    [Experimental("OPENAI001")]
    private static ChatRole FromOpenAIChatRole(ChatMessageRole role) =>
        role switch
        {
            ChatMessageRole.System => ChatRole.System,
            ChatMessageRole.User => ChatRole.User,
            ChatMessageRole.Assistant => ChatRole.Assistant,
            ChatMessageRole.Tool => ChatRole.Tool,
            ChatMessageRole.Developer => OpenAIClientExtensions.ChatRoleDeveloper,
            _ => new ChatRole(role.ToString()),
        };

    /// <summary>Creates <see cref="AIContent"/>s from <see cref="ChatMessageContent"/>.</summary>
    /// <param name="content">The content parts to convert into a content.</param>
    /// <param name="results">The result collection into which to write the resulting content.</param>
    [Experimental("OPENAI001")]
    internal static void ConvertContentParts(ChatMessageContent content, IList<AIContent> results)
    {
        foreach (ChatMessageContentPart contentPart in content)
        {
            if (ToAIContent(contentPart) is { } aiContent)
            {
                results.Add(aiContent);
            }
        }
    }

    /// <summary>Creates an <see cref="AIContent"/> from a <see cref="ChatMessageContentPart"/>.</summary>
    /// <param name="contentPart">The content part to convert into a content.</param>
    /// <returns>The constructed <see cref="AIContent"/>, or <see langword="null"/> if the content part could not be converted.</returns>
    [Experimental("OPENAI001")]
    private static AIContent? ToAIContent(ChatMessageContentPart contentPart)
    {
        AIContent? aiContent = null;

        switch (contentPart.Kind)
        {
            case ChatMessageContentPartKind.Text:
                aiContent = new TextContent(contentPart.Text);
                break;

            case ChatMessageContentPartKind.Image:
                aiContent =
                    contentPart.ImageUri is not null ? new UriContent(contentPart.ImageUri, OpenAIClientExtensions.ImageUriToMediaType(contentPart.ImageUri)) :
                    contentPart.ImageBytes is not null ? new DataContent(contentPart.ImageBytes.ToMemory(), contentPart.ImageBytesMediaType) :
                    null;

                if (aiContent is not null && contentPart.ImageDetailLevel?.ToString() is string detail)
                {
                    (aiContent.AdditionalProperties ??= [])[nameof(contentPart.ImageDetailLevel)] = detail;
                }

                break;

            case ChatMessageContentPartKind.File:
                aiContent =
                    contentPart.FileId is not null ? new HostedFileContent(contentPart.FileId) { Name = contentPart.Filename } :
                    contentPart.FileBytes is not null ? new DataContent(contentPart.FileBytes.ToMemory(), contentPart.FileBytesMediaType) { Name = contentPart.Filename } :
                    null;
                break;
        }

        if (aiContent is not null)
        {
            if (contentPart.Refusal is string refusal)
            {
                (aiContent.AdditionalProperties ??= [])[nameof(contentPart.Refusal)] = refusal;
            }

            aiContent.RawRepresentation = contentPart;
        }

        return aiContent;
    }

    /// <summary>Converts an OpenAI finish reason to an Extensions finish reason.</summary>
    private static ChatFinishReason? FromOpenAIFinishReason(OpenAI.Chat.ChatFinishReason? finishReason) =>
        finishReason?.ToString() is not string s ? null :
        finishReason switch
        {
            OpenAI.Chat.ChatFinishReason.Stop => ChatFinishReason.Stop,
            OpenAI.Chat.ChatFinishReason.Length => ChatFinishReason.Length,
            OpenAI.Chat.ChatFinishReason.ContentFilter => ChatFinishReason.ContentFilter,
            OpenAI.Chat.ChatFinishReason.ToolCalls or OpenAI.Chat.ChatFinishReason.FunctionCall => ChatFinishReason.ToolCalls,
            _ => new ChatFinishReason(s),
        };

    /// <summary>Sanitizes the author name to be appropriate for including as an OpenAI participant name.</summary>
    private static string? SanitizeAuthorName(string? name)
    {
        if (name is not null)
        {
            const int MaxLength = 64;

            name = InvalidAuthorNameRegex().Replace(name, string.Empty);
            if (name.Length == 0)
            {
                name = null;
            }
            else if (name.Length > MaxLength)
            {
                name = name.Substring(0, MaxLength);
            }
        }

        return name;
    }

    /// <summary>POCO representing function calling info. Used to concatenation information for a single function call from across multiple streaming updates.</summary>
    private sealed class FunctionCallInfo
    {
        public string? CallId;
        public string? Name;
        public StringBuilder? Arguments;
    }

    private const string InvalidAuthorNamePattern = @"[^a-zA-Z0-9_]+";
    [GeneratedRegex(InvalidAuthorNamePattern)]
    private static partial Regex InvalidAuthorNameRegex();
}
