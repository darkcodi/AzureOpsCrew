"use client"

import { useState, useCallback, useRef } from "react"
import type { Channel, Agent, ChatMessage } from "@/lib/agents"
import { ChannelHeader } from "@/components/channel-header"
import { MessageList } from "@/components/message-list"
import { MessageInput } from "@/components/message-input"
import { MemberList } from "@/components/member-list"
import type { AGUIEvent } from "@ag-ui/core"
import { EventType } from "@ag-ui/core"

interface ChannelAreaProps {
  channel: Channel
  allAgents: Agent[]
  onUpdateChannel: (channel: Channel) => void
  onAddAgent: (agent: Agent) => void
  onUpdateAgent: (agent: Agent) => void
  onDeleteAgent: (agentId: string) => void
  onOpenInDM?: (agentId: string, message?: string) => void
}

export function ChannelArea({
  channel,
  allAgents,
  onUpdateChannel,
  onAddAgent,
  onUpdateAgent,
  onDeleteAgent,
  onOpenInDM,
}: ChannelAreaProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [streamingAgentId, setStreamingAgentId] = useState<string | null>(null)
  const [streamingContent, setStreamingContent] = useState("")
  const [isProcessing, setIsProcessing] = useState(false)
  const [showMembers, setShowMembers] = useState(true)
  const abortRef = useRef<AbortController | null>(null)

  const activeAgents = allAgents.filter((a) => channel.agentIds.includes(a.id))

  const handleToggleAgent = async (agentId: string) => {
    const isAdding = !channel.agentIds.includes(agentId)
    const endpoint = isAdding
      ? `/api/channels/${channel.id}/add-agent`
      : `/api/channels/${channel.id}/remove-agent`

    const response = await fetch(endpoint, {
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
      const response = await fetch(
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
      if (isProcessing || activeAgents.length === 0) return

      setIsProcessing(true)

      const userMsg: ChatMessage = {
        id: "user-" + Date.now(),
        role: "user",
        content: text,
        timestamp: new Date(),
      }

      setMessages((prev) => [...prev, userMsg])

      // Build conversation history
      // The backend's workflow agent handles context across agents
      const history = [
        ...messages.map((m) => ({
          role: m.role,
          content: m.content,
        })),
        { role: "user", content: text },
      ]

      abortRef.current = new AbortController()

      try {
        // SINGLE call to backend - no loop!
        // The backend workflow handles running all agents in sequence
        const response = await fetch(`/api/channel-agui/${channel.id}`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            messages: history,
          }),
          signal: abortRef.current.signal,
        })

        if (!response.ok || !response.body) {
          throw new Error("Failed to get AGUI response")
        }

        const reader = response.body.getReader()
        const decoder = new TextDecoder()
        let buffer = ""

        // Track the current message being streamed
        let currentMessageId: string | null = null
        let currentContent = ""
        let currentAgentName: string | null = null
        let messageIndex = 0

        while (true) {
          const { done, value } = await reader.read()
          if (done) break

          buffer += decoder.decode(value, { stream: true })
          const lines = buffer.split("\n")
          buffer = lines.pop() || ""

          for (const line of lines) {
            const trimmed = line.trim()
            if (trimmed.startsWith("data:")) {
              const data = trimmed.slice(5).trim()
              if (data === "[DONE]") continue
              try {
                const event: AGUIEvent = JSON.parse(data)

                // TEXT_MESSAGE_START: A new message starts
                if (event.type === EventType.TEXT_MESSAGE_START) {
                  currentMessageId = event.messageId
                  currentContent = ""
                  // Extract authorName from message ID (format: "AuthorName|OriginalMessageId")
                  const pipeIndex = event.messageId.indexOf("|")
                  currentAgentName = pipeIndex !== -1 ? event.messageId.slice(0, pipeIndex) : null

                  // Show typing indicator for the agent
                  const agent = currentAgentName
                    ? activeAgents.find((a) => a.name === currentAgentName)
                    : null
                  setStreamingAgentId(agent?.id ?? "channel")
                  setStreamingContent("")
                }
                // TEXT_MESSAGE_CONTENT: Streaming content
                else if (event.type === EventType.TEXT_MESSAGE_CONTENT) {
                  if (event.messageId === currentMessageId) {
                    currentContent += event.delta
                    setStreamingContent(currentContent)
                  }
                }
                // TEXT_MESSAGE_END: Message finished
                else if (event.type === EventType.TEXT_MESSAGE_END) {
                  if (event.messageId === currentMessageId && currentContent) {
                    // Map authorName to agentId
                    const agent = currentAgentName
                      ? activeAgents.find((a) => a.name === currentAgentName)
                      : null
                    const agentMsg: ChatMessage = {
                      id: "channel-" + channel.id + "-" + messageIndex++,
                      role: "assistant",
                      content: currentContent,
                      timestamp: new Date(),
                      agentId: agent?.id,
                    }
                    setMessages((prev) => [...prev, agentMsg])

                    // Reset for next message
                    currentMessageId = null
                    currentContent = ""
                    currentAgentName = null
                    setStreamingAgentId(null)
                    setStreamingContent("")
                  }
                }
                // RUN_FINISHED: All done
                else if (event.type === EventType.RUN_FINISHED) {
                  setStreamingAgentId(null)
                  setStreamingContent("")
                }
                // RUN_ERROR: Error occurred
                else if (event.type === EventType.RUN_ERROR) {
                  console.error("AGUI run error:", event.message)
                }
              } catch {
                /* skip invalid events */
              }
            }
          }
        }
      } catch (err) {
        if (!(err instanceof DOMException && err.name === "AbortError")) {
          console.error("Error processing channel stream:", err)
        }
      } finally {
        setStreamingAgentId(null)
        setStreamingContent("")
        setIsProcessing(false)
      }
    },
    [isProcessing, activeAgents, messages, channel.id, channel.name]
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
          disabled={isProcessing}
        />
      </div>

      {showMembers && (
        <MemberList
          allAgents={allAgents}
          activeAgentIds={channel.agentIds}
          streamingAgentId={streamingAgentId}
          onToggleAgent={handleToggleAgent}
          onOpenInDM={onOpenInDM}
          onKickMember={handleKickMember}
        />
      )}
    </div>
  )
}
