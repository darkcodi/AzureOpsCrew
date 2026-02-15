"use client"

import { useState, useCallback, useRef } from "react"
import type { Channel, Agent, ChatMessage } from "@/lib/agents"
import { ChannelHeader } from "@/components/channel-header"
import { MessageList } from "@/components/message-list"
import { MessageInput } from "@/components/message-input"
import { MemberList } from "@/components/member-list"
import type { AGUIEvent, AGUI_EVENT_TYPES } from "@/lib/types/agui"

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

    try {
      const response = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ agentId }),
      })

      if (response.ok) {
        // Only update local state after successful backend call
        const newIds = isAdding
          ? [...channel.agentIds, agentId]
          : channel.agentIds.filter((id) => id !== agentId)
        onUpdateChannel({ ...channel, agentIds: newIds })
      } else {
        console.error("Failed to update agent in channel")
      }
    } catch (error) {
      console.error("Error updating agent in channel:", error)
    }
  }

  const sendToAgent = useCallback(
    async (
      agent: Agent,
      conversationHistory: { role: string; content: string }[],
      signal: AbortSignal
    ) => {
      setStreamingAgentId(agent.id)
      setStreamingContent("")

      const response = await fetch(`/api/channel-agui/${channel.id}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          messages: conversationHistory,
          agentId: agent.id,
        }),
        signal,
      })

      if (!response.ok || !response.body) {
        throw new Error("Failed to get AGUI response from " + agent.name)
      }

      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let fullContent = ""
      let buffer = ""
      const activeToolCalls: Map<string, { name: string; args: string }> = new Map()

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

              // Handle text content (streaming response)
              if (event.type === AGUI_EVENT_TYPES.TEXT_MESSAGE_CONTENT) {
                fullContent += event.delta
                setStreamingContent(fullContent)
              }
              // Handle tool call start
              else if (event.type === AGUI_EVENT_TYPES.TOOL_CALL_START) {
                activeToolCalls.set(event.toolCallId, {
                  name: event.toolCallName,
                  args: "",
                })
              }
              // Handle tool call args (streaming)
              else if (event.type === AGUI_EVENT_TYPES.TOOL_CALL_ARGS) {
                const existing = activeToolCalls.get(event.toolCallId)
                if (existing) {
                  existing.args += event.delta
                }
              }
              // Handle tool call end - display tool invocation
              else if (event.type === AGUI_EVENT_TYPES.TOOL_CALL_END) {
                const toolCall = activeToolCalls.get(event.toolCallId)
                if (toolCall) {
                  // Could display tool call in UI here
                  console.log(`Tool called: ${toolCall.name}`, toolCall.args)
                }
              }
              // Handle tool result
              else if (event.type === AGUI_EVENT_TYPES.TOOL_CALL_RESULT) {
                activeToolCalls.delete(event.toolCallId)
                // Tool result could be displayed in UI
              }
              // Handle run finished
              else if (event.type === AGUI_EVENT_TYPES.RUN_FINISHED) {
                // Run completed
              }
              // Handle run error
              else if (event.type === AGUI_EVENT_TYPES.RUN_ERROR) {
                console.error("AGUI run error:", event.message)
              }
            } catch {
              /* skip */
            }
          }
        }
      }

      return fullContent
    },
    [channel.id]
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

      const history = [
        ...messages.map((m) => ({
          role: m.role,
          content:
            m.role === "assistant" && m.agentId
              ? "[" +
                (allAgents.find((a) => a.id === m.agentId)?.name ?? "Agent") +
                "]: " +
                m.content
              : m.content,
        })),
        { role: "user", content: text },
      ]

      abortRef.current = new AbortController()

      for (const agent of activeAgents) {
        if (abortRef.current.signal.aborted) break

        try {
          const content = await sendToAgent(
            agent,
            history,
            abortRef.current.signal
          )

          if (content) {
            const agentMsg: ChatMessage = {
              id: "agent-" + agent.id + "-" + Date.now(),
              role: "assistant",
              content,
              agentId: agent.id,
              timestamp: new Date(),
            }
            setMessages((prev) => [...prev, agentMsg])

            history.push({
              role: "assistant",
              content: "[" + agent.name + "]: " + content,
            })
          }
        } catch (err) {
          if (err instanceof DOMException && err.name === "AbortError") break
        }
      }

      setStreamingAgentId(null)
      setStreamingContent("")
      setIsProcessing(false)
    },
    [isProcessing, activeAgents, messages, allAgents, sendToAgent]
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
        />
      )}
    </div>
  )
}
