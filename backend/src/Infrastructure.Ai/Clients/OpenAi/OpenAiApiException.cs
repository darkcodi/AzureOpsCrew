using System.ComponentModel;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;

/// <summary>
/// Exception thrown when the OpenAI API returns an error response
/// </summary>
public class OpenAiApiException : Exception
{
    /// <summary>
    /// Gets the HTTP status code from the API response
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets the OpenAI error code (e.g., "invalid_api_key", "rate_limit_exceeded")
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets the OpenAI error type (e.g., "invalid_request_error", "authentication_error")
    /// </summary>
    public string? ErrorType { get; set; }

    /// <summary>
    /// Gets the parameter that caused the error (if applicable)
    /// </summary>
    public string? Param { get; set; }

    /// <summary>
    /// Creates a new OpenAiApiException with a message
    /// </summary>
    public OpenAiApiException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new OpenAiApiException with a message and error code
    /// </summary>
    public OpenAiApiException(string message, string? errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a new OpenAiApiException with a message, error code, and inner exception
    /// </summary>
    public OpenAiApiException(string message, string? errorCode, Exception? innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates an OpenAiApiException from an OpenAI error response
    /// </summary>
    public static OpenAiApiException FromErrorResponse(OpenAiError errorResponse, int statusCode)
    {
        var error = errorResponse.Error;
        return new OpenAiApiException(error.Message, error.Code)
        {
            StatusCode = statusCode,
            ErrorType = error.Type,
            Param = error.Param
        };
    }

    /// <summary>
    /// Creates an OpenAiApiException from an HTTP response content
    /// </summary>
    public static OpenAiApiException FromHttpContent(string content, int statusCode)
    {
        try
        {
            var errorResponse = JsonSerializer.Deserialize<OpenAiError>(content);
            if (errorResponse?.Error != null)
            {
                return FromErrorResponse(errorResponse, statusCode);
            }
        }
        catch (JsonException)
        {
            // Not valid JSON error response
        }

        return new OpenAiApiException(
            $"OpenAI API request failed with status code {statusCode}: {content}",
            null)
        {
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Checks if this exception represents an authentication error
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsAuthenticationError() =>
        ErrorType == "authentication_error" ||
        ErrorCode == "invalid_api_key" ||
        ErrorCode == "invalid_organization" ||
        StatusCode == 401;

    /// <summary>
    /// Checks if this exception represents a rate limit error
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsRateLimitError() =>
        ErrorType == "rate_limit_error" ||
        ErrorCode == "rate_limit_exceeded" ||
        StatusCode == 429;

    /// <summary>
    /// Checks if this exception represents an invalid request error
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsInvalidRequestError() =>
        ErrorType == "invalid_request_error" ||
        StatusCode == 400;

    /// <summary>
    /// Checks if this exception represents a context length exceeded error
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsContextLengthExceeded() =>
        ErrorCode == "context_length_exceeded" ||
        Message.Contains("context length", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if this exception is retryable (rate limit or temporary server error)
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsRetryable() =>
        IsRateLimitError() ||
        StatusCode == 503 || // Service unavailable
        StatusCode == 502 || // Bad gateway
        StatusCode == 504; // Gateway timeout
}
