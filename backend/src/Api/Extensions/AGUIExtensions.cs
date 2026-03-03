using AzureOpsCrew.Api.Endpoints.Dtos.AGUI;
using Microsoft.Extensions.AI;
using Serilog;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AzureOpsCrew.Api.Extensions;

public static class AGUIExtensions
{
    private static readonly MediaTypeHeaderValue? s_jsonPatchMediaType = new("application/json-patch+json");
    private static readonly MediaTypeHeaderValue? s_json = new("application/json");

    /// <summary>
    /// Debug interceptor: logs every ChatResponseUpdate flowing through the pipeline.
    /// Insert between pipeline stages to trace where events are lost.
    /// </summary>
    public static async IAsyncEnumerable<ChatResponseUpdate> WithDebugLogging(
        this IAsyncEnumerable<ChatResponseUpdate> updates,
        string label,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int count = 0;
        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            count++;
            var contentSummary = update.Contents?.Count > 0
                ? string.Join(", ", update.Contents.Select(c => c.GetType().Name))
                : "empty";
            var textPreview = update.Contents?.OfType<TextContent>().FirstOrDefault()?.Text;
            if (textPreview is { Length: > 80 })
                textPreview = textPreview[..80] + "...";

            // Capture ErrorContent details
            var errorDetail = update.Contents?.OfType<Microsoft.Extensions.AI.ErrorContent>().FirstOrDefault();
            if (errorDetail is not null)
            {
                Log.Error("[{Label}] ERROR in update #{Count}: Author={Author}, ErrorMessage={ErrorMsg}, Exception={Ex}",
                    label, count, update.AuthorName ?? "(null)",
                    errorDetail.Message ?? "(no message)",
                    errorDetail.Details ?? "(no details)");
            }

            Log.Debug("[{Label}] Update #{Count}: Author={Author}, Role={Role}, MsgId={MsgId}, Finish={Finish}, Contents=[{Contents}], Text={Text}",
                label, count, update.AuthorName ?? "(null)", update.Role?.Value ?? "(null)",
                update.MessageId ?? "(null)", update.FinishReason?.ToString() ?? "(null)",
                contentSummary, textPreview ?? "(none)");

            yield return update;
        }
        Log.Information("[{Label}] Stream completed with {Count} total updates", label, count);
    }

    public static async IAsyncEnumerable<ChatResponseUpdate> FilterServerToolsFromMixedToolInvocationsAsync(
        this IAsyncEnumerable<ChatResponseUpdate> updates,
        List<AITool>? clientTools,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (clientTools is null || clientTools.Count == 0)
        {
            await foreach (var update in updates.WithCancellation(cancellationToken))
            {
                yield return update;
            }
            yield break;
        }

        var set = new HashSet<string>(clientTools.Count);
        foreach (var tool in clientTools)
        {
            set.Add(tool.Name);
        }

        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            if (update.FinishReason == ChatFinishReason.ToolCalls)
            {
                var containsClientTools = false;
                var containsServerTools = false;
                for (var i = update.Contents.Count - 1; i >= 0; i--)
                {
                    var content = update.Contents[i];
                    if (content is FunctionCallContent functionCallContent)
                    {
                        containsClientTools |= set.Contains(functionCallContent.Name);
                        containsServerTools |= !set.Contains(functionCallContent.Name);
                        if (containsClientTools && containsServerTools)
                        {
                            break;
                        }
                    }
                }

                if (containsClientTools && containsServerTools)
                {
                    var newContents = new List<AIContent>();
                    for (var i = update.Contents.Count - 1; i >= 0; i--)
                    {
                        var content = update.Contents[i];
                        if (content is not FunctionCallContent fcc ||
                            set.Contains(fcc.Name))
                        {
                            newContents.Add(content);
                        }
                    }

                    yield return new ChatResponseUpdate(update.Role, newContents)
                    {
                        ConversationId = update.ConversationId,
                        ResponseId = update.ResponseId,
                        FinishReason = update.FinishReason,
                        AdditionalProperties = update.AdditionalProperties,
                        AuthorName = update.AuthorName,
                        CreatedAt = update.CreatedAt,
                        MessageId = update.MessageId,
                        ModelId = update.ModelId
                    };
                }
                else
                {
                    yield return update;
                }
            }
            else
            {
                yield return update;
            }
        }
    }

    public static async IAsyncEnumerable<BaseEvent> AsAGUIEventStreamAsync(
        this IAsyncEnumerable<ChatResponseUpdate> updates,
        string threadId,
        string runId,
        JsonSerializerOptions jsonSerializerOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new RunStartedEvent
        {
            ThreadId = threadId,
            RunId = runId
        };

        string? currentMessageId = null;
        string? currentOriginalMessageId = null; // Track original ID for comparison
        await foreach (var chatResponse in updates.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (chatResponse is { Contents.Count: > 0 } &&
                chatResponse.Contents[0] is TextContent &&
                !string.Equals(currentOriginalMessageId, chatResponse.MessageId, StringComparison.Ordinal))
            {
                // End the previous message if there was one
                if (currentMessageId is not null)
                {
                    yield return new TextMessageEndEvent
                    {
                        MessageId = currentMessageId
                    };
                }

                // Prepend AuthorName to message ID with "|" separator for agent identification
                var messageIdWithAuthor = !string.IsNullOrEmpty(chatResponse.AuthorName)
                    ? $"{chatResponse.AuthorName}|{chatResponse.MessageId}"
                    : chatResponse.MessageId!;

                // Start the new message
                yield return new TextMessageStartEvent
                {
                    MessageId = messageIdWithAuthor,
                    Role = chatResponse.Role!.Value.Value
                };

                currentMessageId = messageIdWithAuthor;
                currentOriginalMessageId = chatResponse.MessageId;
            }

            // Emit text content if present
            if (chatResponse is { Contents.Count: > 0 } && chatResponse.Contents[0] is TextContent textContent &&
                !string.IsNullOrEmpty(textContent.Text))
            {
                // Use the currentMessageId which already has the author prefix
                yield return new TextMessageContentEvent
                {
                    MessageId = currentMessageId!,
                    Delta = textContent.Text
                };
            }

            // Emit tool call events and tool result events
            if (chatResponse is { Contents.Count: > 0 })
            {
                foreach (var content in chatResponse.Contents)
                {
                    if (content is FunctionCallContent functionCallContent)
                    {
                        yield return new ToolCallStartEvent
                        {
                            ToolCallId = functionCallContent.CallId,
                            ToolCallName = functionCallContent.Name,
                            ParentMessageId = chatResponse.MessageId
                        };

                        yield return new ToolCallArgsEvent
                        {
                            ToolCallId = functionCallContent.CallId,
                            Delta = JsonSerializer.Serialize(
                                functionCallContent.Arguments,
                                jsonSerializerOptions.GetTypeInfo(typeof(IDictionary<string, object?>)))
                        };

                        yield return new ToolCallEndEvent
                        {
                            ToolCallId = functionCallContent.CallId
                        };
                    }
                    else if (content is FunctionResultContent functionResultContent)
                    {
                        yield return new ToolCallResultEvent
                        {
                            MessageId = chatResponse.MessageId,
                            ToolCallId = functionResultContent.CallId,
                            Content = SerializeResultContent(functionResultContent, jsonSerializerOptions) ?? "",
                            Role = AGUIRoles.Tool
                        };
                    }
                    else if (content is DataContent dataContent)
                    {
                        if (MediaTypeHeaderValue.TryParse(dataContent.MediaType, out var mediaType) && mediaType.Equals(s_json))
                        {
                            // State snapshot event
                            yield return new StateSnapshotEvent
                            {
                                Snapshot = (JsonElement?)JsonSerializer.Deserialize(
                                dataContent.Data.Span,
                                jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)))
                            };
                        }
                        else if (mediaType is { } && mediaType.Equals(s_jsonPatchMediaType))
                        {
                            // State snapshot patch event must be a valid JSON patch,
                            // but its not up to us to validate that here.
                            yield return new StateDeltaEvent
                            {
                                Delta = (JsonElement?)JsonSerializer.Deserialize(
                                dataContent.Data.Span,
                                jsonSerializerOptions.GetTypeInfo(typeof(JsonElement)))
                            };
                        }
                        else
                        {
                            // Text content event
                            yield return new TextMessageContentEvent
                            {
                                MessageId = chatResponse.MessageId!,
                                Delta = Encoding.UTF8.GetString(dataContent.Data.Span)
                            };
                        }
                    }
                    else if (content is ErrorContent errorContent)
                    {
                        // Surface errors as visible text messages so user sees what went wrong
                        var errorMsgId = $"error|{chatResponse.MessageId ?? Guid.NewGuid().ToString("N")}";
                        var errorText = $"⚠️ Error: {errorContent.Message ?? "Unknown error"}";
                        Log.Warning("AGUI: Surfacing ErrorContent as message: {Error}", errorText);

                        yield return new TextMessageStartEvent
                        {
                            MessageId = errorMsgId,
                            Role = "assistant"
                        };
                        yield return new TextMessageContentEvent
                        {
                            MessageId = errorMsgId,
                            Delta = errorText
                        };
                        yield return new TextMessageEndEvent
                        {
                            MessageId = errorMsgId
                        };
                    }
                }
            }
        }

        // End the last message if there was one
        if (currentMessageId is not null)
        {
            yield return new TextMessageEndEvent
            {
                MessageId = currentMessageId
            };
        }

        yield return new RunFinishedEvent
        {
            ThreadId = threadId,
            RunId = runId,
        };
    }

    public static string? SerializeResultContent(FunctionResultContent functionResultContent, JsonSerializerOptions options)
    {
        return functionResultContent.Result switch
        {
            null => null,
            string str => str,
            JsonElement jsonElement => jsonElement.GetRawText(),
            _ => JsonSerializer.Serialize(functionResultContent.Result, options.GetTypeInfo(functionResultContent.Result.GetType())),
        };
    }
}
