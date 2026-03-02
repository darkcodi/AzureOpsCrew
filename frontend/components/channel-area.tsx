"use client"

import { useState, useCallback, useEffect, useRef } from "react"
import type { Channel, Agent, ChatMessage } from "@/lib/agents"
import { ChannelHeader } from "@/components/channel-header"
import { MessageList } from "@/components/message-list"
import { MessageInput } from "@/components/message-input"
import { MemberList } from "@/components/member-list"
import type { HumanMember } from "@/lib/humans"
import { fetchWithErrorHandling } from "@/lib/fetch"
import { ChannelEventsClient, type MessageAddedEvent, type AgentThinkingStartEvent, type AgentThinkingEndEvent, type AgentTextContentEvent, type TypingIndicatorEvent } from "@/lib/signalr-client"

interface ChannelAreaProps {
  channel: Channel
  allAgents: Agent[]
  humans: HumanMember[]
  username: string
  onUpdateChannel: (channel: Channel) => void
  onAddAgent: (agent: Agent) => void
  onUpdateAgent: (agent: Agent) => void
  onDeleteAgent: (agentId: string) => void
  onOpenInDM?: (agentId: string, message?: string) => void
}

export function ChannelArea({
  channel,
  allAgents,
  humans,
  username,
  onUpdateChannel,
  onAddAgent,
  onUpdateAgent,
  onDeleteAgent,
  onOpenInDM,
}: ChannelAreaProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [showMembers, setShowMembers] = useState(true)
  const [streamingAgentId, setStreamingAgentId] = useState<string | null>(null)
  const [streamingContent, setStreamingContent] = useState("")
  const [typingAgentIds, setTypingAgentIds] = useState<Set<string>>(new Set())

  // Track the SignalR client connection
  const signalRClientRef = useRef<ChannelEventsClient | null>(null)

  const activeAgents = allAgents.filter((a) => channel.agentIds.includes(a.id))

  // Helper to convert backend message to frontend ChatMessage format
  const toChatMessage = useCallback((m: {
    id: string
    text?: string
    content?: string
    userId?: string
    agentId?: string
    senderId?: string
    postedAt?: string
  }): ChatMessage => {
    const isUser = !m.agentId
    return {
      id: m.id,
      role: isUser ? 'user' : 'assistant',
      content: m.text ?? m.content ?? '',
      ...(isUser ? {} : { agentId: m.agentId }),
      timestamp: m.postedAt ? new Date(m.postedAt) : new Date(),
    }
  }, [])

  // Check if message already exists in state
  const messageExists = useCallback((messageId: string) => {
    return messages.some(m => m.id === messageId)
  }, [messages])

  // Set up SignalR connection when channel changes
  useEffect(() => {
    // Clean up previous connection
    const cleanup = async () => {
      if (signalRClientRef.current) {
        try {
          await signalRClientRef.current.stop()
        } catch (err) {
          console.error("Error stopping SignalR connection:", err)
        }
        signalRClientRef.current = null
      }
      // Reset streaming state
      setStreamingAgentId(null)
      setStreamingContent("")
      setTypingAgentIds(new Set())
    }

    // Create new connection
    const setupConnection = async () => {
      await cleanup()

      const client = new ChannelEventsClient(channel.id)
      signalRClientRef.current = client

      // Register event handlers
      client.onMessageAdded((event: MessageAddedEvent) => {
        // Don't add if message already exists (could happen due to optimistic update)
        if (!messageExists(event.message.id)) {
          const chatMessage = toChatMessage(event.message)
          setMessages(prev => [...prev, chatMessage])
        }
      })

      client.onAgentThinkingStart((event: AgentThinkingStartEvent) => {
        console.log(`Agent ${event.agentName} started thinking`)
        setTypingAgentIds(prev => new Set(prev).add(event.agentId))
      })

      client.onAgentThinkingEnd((event: AgentThinkingEndEvent) => {
        console.log(`Agent ${event.agentName} stopped thinking`)
        setTypingAgentIds(prev => {
          const next = new Set(prev)
          next.delete(event.agentId)
          return next
        })
        // Clear streaming state when agent finishes thinking
        if (streamingAgentId === event.agentId) {
          setStreamingAgentId(null)
          setStreamingContent("")
        }
      })

      client.onAgentTextContent((event: AgentTextContentEvent) => {
        // Note: We currently don't use this for streaming display
        // The final message comes via MESSAGE_ADDED when the agent finishes
        console.log(`Agent ${event.agentName} text content: ${event.content.substring(0, 50)}...`)
      })

      client.onTypingIndicator((event: TypingIndicatorEvent) => {
        if (event.isTyping) {
          setTypingAgentIds(prev => new Set(prev).add(event.agentId))
        } else {
          setTypingAgentIds(prev => {
            const next = new Set(prev)
            next.delete(event.agentId)
            return next
          })
        }
      })

      // Start the connection
      try {
        await client.start()
        console.log(`SignalR connected to channel ${channel.id}`)
      } catch (err) {
        console.error(`Failed to connect SignalR for channel ${channel.id}:`, err)
      }
    }

    setupConnection()

    // Cleanup on unmount or channel change
    return () => {
      cleanup()
    }
  }, [channel.id, toChatMessage, messageExists, streamingAgentId])

  // Load messages when channel changes
  useEffect(() => {
    const loadMessages = async () => {
      try {
        const response = await fetchWithErrorHandling(`/api/channels/${channel.id}/messages`)
        if (response.ok) {
          const data = await response.json()
          // Transform backend messages to frontend ChatMessage format
          const chatMessages: ChatMessage[] = data.map(toChatMessage)
          setMessages(chatMessages)
        }
      } catch (err) {
        console.error("Failed to load channel messages:", err)
      }
    }

    loadMessages()
  }, [channel.id, toChatMessage])

  const handleToggleAgent = async (agentId: string) => {
    const isAdding = !channel.agentIds.includes(agentId)
    const endpoint = isAdding
      ? `/api/channels/${channel.id}/add-agent`
      : `/api/channels/${channel.id}/remove-agent`

    const response = await fetchWithErrorHandling(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ agentId }),
    })

    if (!response.ok) {
      const data = await response.json().catch(() => ({}))
      throw new Error(data.error ?? "Failed to update agent in channel")
    }

    const newIds = isAdding
      ? [...channel.agentIds, agentId]
      : channel.agentIds.filter((id) => id !== agentId)
    onUpdateChannel({ ...channel, agentIds: newIds })
  }

  const handleKickMember = useCallback(
    async (agentId: string) => {
      const response = await fetchWithErrorHandling(
        `/api/channels/${channel.id}/remove-agent`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ agentId }),
        }
      )

      if (!response.ok) {
        const data = await response.json().catch(() => ({}))
        throw new Error(data.error ?? "Failed to kick member from channel")
      }

      const newIds = channel.agentIds.filter((id) => id !== agentId)
      onUpdateChannel({ ...channel, agentIds: newIds })
    },
    [channel, onUpdateChannel]
  )

  const handleSend = useCallback(
    async (text: string) => {
      // Optimistically add user message
      const userMsg: ChatMessage = {
        id: "user-" + Date.now(),
        role: "user",
        content: text,
        timestamp: new Date(),
      }
      setMessages((prev) => [...prev, userMsg])

      try {
        const response = await fetchWithErrorHandling(`/api/channels/${channel.id}/messages`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ Content: text }),
        })

        if (response.ok) {
          const message = await response.json()
          // Update the optimistic message with the real ID
          setMessages((prev) =>
            prev.map((m) =>
              m.id === userMsg.id ? { ...m, id: message.id } : m
            )
          )
        } else {
          // Remove optimistic message on failure
          setMessages((prev) => prev.filter((m) => m.id !== userMsg.id))
        }
      } catch (err) {
        console.error("Error sending channel message:", err)
        // Remove optimistic message on error
        setMessages((prev) => prev.filter((m) => m.id !== userMsg.id))
      }
    },
    [channel.id]
  )

  return (
    <div className="flex flex-1 overflow-hidden">
      <div
        className="flex flex-1 flex-col"
        style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
      >
        <ChannelHeader
          channel={channel}
          onManageAgents={() => {/* gear icon in header - no-op for now, wrench handles it */}}
          showMembers={showMembers}
          onToggleMembers={() => setShowMembers((prev) => !prev)}
        />

        <MessageList
          messages={messages}
          agents={allAgents}
          streamingAgentId={streamingAgentId}
          streamingContent={streamingContent}
        />

        <MessageInput
          channelName={channel.name}
          onSend={handleSend}
        />
      </div>

      {showMembers && (
        <MemberList
          allAgents={allAgents}
          humans={humans}
          activeAgentIds={channel.agentIds}
          streamingAgentId={typingAgentIds.size > 0 ? Array.from(typingAgentIds)[0] : null}
          username={username}
          onToggleAgent={handleToggleAgent}
          onOpenInDM={onOpenInDM}
          onKickMember={handleKickMember}
        />
      )}
    </div>
  )
}
