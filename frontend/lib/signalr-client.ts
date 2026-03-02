// Type definitions for SignalR types
interface RetryContext {
  previousRetryCount: number
}

type HubConnectionState = "Connecting" | "Connected" | "Disconnected" | "Disconnecting"

interface HubConnection {
  state: HubConnectionState
  start(): Promise<void>
  stop(): Promise<void>
  on(event: string, callback: (...args: any[]) => void): void
  off(event: string, callback?: (...args: any[]) => void): void
  onreconnecting(callback: (error?: Error) => void): void
  onreconnected(callback: (connectionId?: string) => void): void
  onclose(callback: (error?: Error) => void): void
  invoke(method: string, ...args: any[]): Promise<any>
}

interface HubConnectionBuilder {
  withUrl(url: string, options?: any): HubConnectionBuilder
  withAutomaticReconnect(options?: any): HubConnectionBuilder
  configureLogging(logLevel: any): HubConnectionBuilder
  build(): HubConnection
}

// Try to import SignalR, use a stub if not available
let signalR: {
  HubConnectionBuilder: new () => HubConnectionBuilder
  HubConnectionState: typeof HubConnectionState
  HttpTransportType: any
  LogLevel: any
} | null = null

try {
  // @ts-ignore - Package may not be installed
  signalR = require("@microsoft/signalr")
} catch {
  console.warn("@microsoft/signalr package is not installed. Real-time features will be disabled.")
  console.warn("Install it with: pnpm install @microsoft/signalr")
}

export interface ChannelEvent {
  type: string
  timestamp: string
}

export interface MessageAddedEvent extends ChannelEvent {
  type: "MESSAGE_ADDED"
  message: {
    id: string
    text: string
    postedAt: string
    userId?: string
    agentId?: string
    authorName?: string
    channelId?: string
  }
}

export interface AgentThinkingStartEvent extends ChannelEvent {
  type: "AGENT_THINKING_START"
  agentId: string
  agentName: string
}

export interface AgentThinkingEndEvent extends ChannelEvent {
  type: "AGENT_THINKING_END"
  agentId: string
  agentName: string
}

export interface AgentTextContentEvent extends ChannelEvent {
  type: "AGENT_TEXT_CONTENT"
  agentId: string
  agentName: string
  content: string
  isDelta: boolean
}

export interface ToolCallStartEvent extends ChannelEvent {
  type: "TOOL_CALL_START"
  agentId: string
  agentName: string
  toolName: string
  toolCallId: string
}

export interface ToolCallEndEvent extends ChannelEvent {
  type: "TOOL_CALL_END"
  agentId: string
  agentName: string
  toolName: string
  toolCallId: string
  success: boolean
  errorMessage?: string
}

export interface TypingIndicatorEvent extends ChannelEvent {
  type: "TYPING_INDICATOR"
  agentId: string
  agentName: string
  isTyping: boolean
}

export type ChannelEventHandler = (event: ChannelEvent) => void

/**
 * SignalR client for real-time channel events.
 * Connects to the channel events hub and receives real-time updates.
 *
 * Note: Requires @microsoft/signalr package to be installed.
 * Install with: pnpm install @microsoft/signalr
 */
export class ChannelEventsClient {
  private connection: HubConnection | null = null
  private channelId: string
  private reconnectAttempts = 0
  private maxReconnectAttempts = 5
  private eventHandlers: Map<string, Set<ChannelEventHandler>> = new Map()
  private isStub: boolean = false

  constructor(channelId: string) {
    this.channelId = channelId
    this.isStub = !signalR
    if (this.isStub) {
      console.warn(`ChannelEventsClient created in stub mode (package not installed) for channel ${channelId}`)
    }
  }

  /**
   * Start the connection and join the channel group.
   */
  async start(): Promise<void> {
    if (this.isStub) {
      console.warn("Cannot start SignalR connection: @microsoft/signalr package not installed")
      return
    }

    if (this.connection?.state === "Connected") {
      return
    }

    // Build the connection URL
    const hubUrl = `/api/channels/${this.channelId}/events`

    this.connection = new signalR!.HubConnectionBuilder()
      .withUrl(hubUrl, {
        skipNegotiation: false,
        transport: signalR!.HttpTransportType.WebSockets | signalR!.HttpTransportType.ServerSentEvents
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext: RetryContext) => {
          // Exponential backoff: 0s, 2s, 10s, 30s, then 60s
          const delays = [0, 2000, 10000, 30000, 60000]
          const index = Math.min(retryContext.previousRetryCount, delays.length - 1)
          return delays[index]
        }
      })
      .configureLogging(signalR!.LogLevel.Information)
      .build() as HubConnection

    // Set up the main event handler
    this.connection.on("event", (event: ChannelEvent) => {
      this.handleEvent(event)
    })

    // Set up connection event handlers
    this.connection.onreconnecting((error?: Error) => {
      console.warn(`SignalR reconnecting for channel ${this.channelId}:`, error)
    })

    this.connection.onreconnected((connectionId?: string) => {
      console.log(`SignalR reconnected for channel ${this.channelId} with connection ID: ${connectionId}`)
      this.reconnectAttempts = 0
      // Re-join the channel group after reconnection
      this.joinChannel()
    })

    this.connection.onclose((error?: Error) => {
      console.error(`SignalR connection closed for channel ${this.channelId}:`, error)
    })

    try {
      await this.connection.start()
      console.log(`SignalR connected to channel ${this.channelId}`)
      // Join the channel group after connection
      await this.joinChannel()
    } catch (err) {
      console.error(`Error starting SignalR connection for channel ${this.channelId}:`, err)
      throw err
    }
  }

  /**
   * Stop the connection and leave the channel group.
   */
  async stop(): Promise<void> {
    if (this.isStub) {
      return
    }

    if (this.connection) {
      try {
        await this.leaveChannel()
        await this.connection.stop()
        console.log(`SignalR disconnected from channel ${this.channelId}`)
      } catch (err) {
        console.error(`Error stopping SignalR connection for channel ${this.channelId}:`, err)
      }
      this.connection = null
    }
  }

  /**
   * Register a handler for a specific event type.
   */
  on(eventType: string, handler: ChannelEventHandler): void {
    if (!this.eventHandlers.has(eventType)) {
      this.eventHandlers.set(eventType, new Set())
    }
    this.eventHandlers.get(eventType)!.add(handler)
  }

  /**
   * Unregister a handler for a specific event type.
   */
  off(eventType: string, handler: ChannelEventHandler): void {
    const handlers = this.eventHandlers.get(eventType)
    if (handlers) {
      handlers.delete(handler)
      if (handlers.size === 0) {
        this.eventHandlers.delete(eventType)
      }
    }
  }

  /**
   * Register a handler for message added events.
   */
  onMessageAdded(handler: (event: MessageAddedEvent) => void): void {
    this.on("MESSAGE_ADDED", handler as ChannelEventHandler)
  }

  /**
   * Register a handler for agent thinking start events.
   */
  onAgentThinkingStart(handler: (event: AgentThinkingStartEvent) => void): void {
    this.on("AGENT_THINKING_START", handler as ChannelEventHandler)
  }

  /**
   * Register a handler for agent thinking end events.
   */
  onAgentThinkingEnd(handler: (event: AgentThinkingEndEvent) => void): void {
    this.on("AGENT_THINKING_END", handler as ChannelEventHandler)
  }

  /**
   * Register a handler for agent text content events.
   */
  onAgentTextContent(handler: (event: AgentTextContentEvent) => void): void {
    this.on("AGENT_TEXT_CONTENT", handler as ChannelEventHandler)
  }

  /**
   * Register a handler for tool call start events.
   */
  onToolCallStart(handler: (event: ToolCallStartEvent) => void): void {
    this.on("TOOL_CALL_START", handler as ChannelEventHandler)
  }

  /**
   * Register a handler for tool call end events.
   */
  onToolCallEnd(handler: (event: ToolCallEndEvent) => void): void {
    this.on("TOOL_CALL_END", handler as ChannelEventHandler)
  }

  /**
   * Register a handler for typing indicator events.
   */
  onTypingIndicator(handler: (event: TypingIndicatorEvent) => void): void {
    this.on("TYPING_INDICATOR", handler as ChannelEventHandler)
  }

  /**
   * Get the current connection state.
   */
  get ConnectionState(): HubConnectionState | null {
    if (this.isStub || !this.connection) {
      return null
    }
    return this.connection?.state ?? null
  }

  /**
   * Check if the connection is active.
   */
  get isConnected(): boolean {
    if (this.isStub || !this.connection) {
      return false
    }
    return this.connection?.state === "Connected"
  }

  private async joinChannel(): Promise<void> {
    if (this.isStub || !this.connection) {
      return
    }

    if (this.connection.state === "Connected") {
      try {
        await this.connection.invoke("JoinChannel", this.channelId)
        console.log(`Joined channel group: ${this.channelId}`)
      } catch (err) {
        console.error(`Error joining channel ${this.channelId}:`, err)
      }
    }
  }

  private async leaveChannel(): Promise<void> {
    if (this.isStub || !this.connection) {
      return
    }

    if (this.connection.state === "Connected") {
      try {
        await this.connection.invoke("LeaveChannel", this.channelId)
        console.log(`Left channel group: ${this.channelId}`)
      } catch (err) {
        console.error(`Error leaving channel ${this.channelId}:`, err)
      }
    }
  }

  private handleEvent(event: ChannelEvent): void {
    const handlers = this.eventHandlers.get(event.type)
    if (handlers) {
      handlers.forEach((handler) => {
        try {
          handler(event)
        } catch (err) {
          console.error(`Error handling event ${event.type}:`, err)
        }
      })
    }
  }
}

/**
 * React hook for using the SignalR channel events client.
 */
export function useChannelEvents(channelId: string | null) {
  const [client, setClient] = React.useState<ChannelEventsClient | null>(null)
  const [isConnected, setIsConnected] = React.useState(false)

  React.useEffect(() => {
    if (!channelId) return

    const eventsClient = new ChannelEventsClient(channelId)
    setClient(eventsClient)

    eventsClient.start()
      .then(() => setIsConnected(true))
      .catch((err) => {
        console.error("Failed to connect to channel events:", err)
        setIsConnected(false)
      })

    return () => {
      eventsClient.stop()
      setIsConnected(false)
    }
  }, [channelId])

  // Update connection state when client reconnects
  React.useEffect(() => {
    if (!client) return

    const checkConnection = setInterval(() => {
      setIsConnected(client.isConnected)
    }, 1000)

    return () => clearInterval(checkConnection)
  }, [client])

  return { client, isConnected }
}

// Import React for the hook
import React from "react"
