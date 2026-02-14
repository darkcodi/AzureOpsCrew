using System.Buffers;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AzureOpsCrew.Api.Endpoints.Dtos.AGUI;

public class AGUIServerSentEventsResult : IResult, IDisposable
{
    private readonly IAsyncEnumerable<BaseEvent> _events;
    private readonly ILogger<AGUIServerSentEventsResult> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private Utf8JsonWriter? _jsonWriter;

    public AGUIServerSentEventsResult(
        IAsyncEnumerable<BaseEvent> events,
        ILogger<AGUIServerSentEventsResult> logger,
        JsonSerializerOptions jsonSerializerOptions)
    {
        _events = events;
        _logger = logger;
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        if (httpContext == null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache,no-store";
        httpContext.Response.Headers.Pragma = "no-cache";

        var body = httpContext.Response.Body;
        var cancellationToken = httpContext.RequestAborted;

        try
        {
            await SseFormatter.WriteAsync(
                WrapEventsAsSseItemsAsync(_events, cancellationToken),
                body,
                SerializeEvent,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error occurred while streaming server-sent events.");
            // If an error occurs during streaming, try to send an error event before closing
            try
            {
                var errorEvent = new RunErrorEvent
                {
                    Code = "StreamingError",
                    Message = ex.Message
                };
                await SseFormatter.WriteAsync(
                    WrapEventsAsSseItemsAsync([errorEvent]),
                    body,
                    SerializeEvent,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception sendErrorEx)
            {
                // If we can't send the error event, just let the connection close
                _logger.LogError(sendErrorEx, "Failed to send error event after streaming failure.");
            }
        }

        await body.FlushAsync(httpContext.RequestAborted).ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<SseItem<BaseEvent>> WrapEventsAsSseItemsAsync(
        IAsyncEnumerable<BaseEvent> events,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (BaseEvent evt in events.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return new SseItem<BaseEvent>(evt);
        }
    }

    private static async IAsyncEnumerable<SseItem<BaseEvent>> WrapEventsAsSseItemsAsync(
        IEnumerable<BaseEvent> events)
    {
        foreach (BaseEvent evt in events)
        {
            yield return new SseItem<BaseEvent>(evt);
        }
    }

    private void SerializeEvent(SseItem<BaseEvent> item, IBufferWriter<byte> writer)
    {
        if (_jsonWriter == null)
        {
            _jsonWriter = new Utf8JsonWriter(writer);
        }
        else
        {
            _jsonWriter.Reset(writer);
        }
        JsonSerializer.Serialize(_jsonWriter, item.Data, item.Data.GetType(), _jsonSerializerOptions);
    }

    public void Dispose()
    {
        _jsonWriter?.Dispose();
    }
}
