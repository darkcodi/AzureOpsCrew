namespace AzureOpsCrew.Api.Endpoints.Dtos.Channels;

/// <summary>
/// Event type constants for channel events broadcast via SignalR.
/// </summary>
public static class ChannelEventTypes
{
    /// <summary>
    /// A new message was posted to the channel.
    /// </summary>
    public const string MessageAdded = "MESSAGE_ADDED";

    /// <summary>
    /// An agent started processing (thinking).
    /// </summary>
    public const string AgentThinkingStart = "AGENT_THINKING_START";

    /// <summary>
    /// An agent finished processing (thinking).
    /// </summary>
    public const string AgentThinkingEnd = "AGENT_THINKING_END";

    /// <summary>
    /// Streaming text content from an agent.
    /// </summary>
    public const string AgentTextContent = "AGENT_TEXT_CONTENT";

    /// <summary>
    /// An agent started executing a tool.
    /// </summary>
    public const string ToolCallStart = "TOOL_CALL_START";

    /// <summary>
    /// An agent finished executing a tool.
    /// </summary>
    public const string ToolCallEnd = "TOOL_CALL_END";

    /// <summary>
    /// Typing indicator status update for an agent.
    /// </summary>
    public const string TypingIndicator = "TYPING_INDICATOR";

    /// <summary>
    /// User presence status update (online/offline).
    /// </summary>
    public const string UserPresence = "USER_PRESENCE";
}
