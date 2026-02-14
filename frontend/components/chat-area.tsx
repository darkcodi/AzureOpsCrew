"use client"

import { useState, useCallback, useRef } from "react"
import type { Room, Agent, ChatMessage } from "@/lib/agents"
import { ChatHeader } from "@/components/chat-header"
import { MessageList } from "@/components/message-list"
import { MessageInput } from "@/components/message-input"
import { MemberList } from "@/components/member-list"

interface ChatAreaProps {
  room: Room
  allAgents: Agent[]
  onUpdateRoom: (room: Room) => void
  onAddAgent: (agent: Agent) => void
  onUpdateAgent: (agent: Agent) => void
  onDeleteAgent: (agentId: string) => void
  onOpenInDM?: (agentId: string, message?: string) => void
}

export function ChatArea({
  room,
  allAgents,
  onUpdateRoom,
  onAddAgent,
  onUpdateAgent,
  onDeleteAgent,
  onOpenInDM,
}: ChatAreaProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [streamingAgentId, setStreamingAgentId] = useState<string | null>(null)
  const [streamingContent, setStreamingContent] = useState("")
  const [isProcessing, setIsProcessing] = useState(false)
  const [showMembers, setShowMembers] = useState(true)
  const abortRef = useRef<AbortController | null>(null)

  const activeAgents = allAgents.filter((a) => room.agentIds.includes(a.id))

  const handleToggleAgent = (agentId: string) => {
    const newIds = room.agentIds.includes(agentId)
      ? room.agentIds.filter((id) => id !== agentId)
      : [...room.agentIds, agentId]
    onUpdateRoom({ ...room, agentIds: newIds })
  }

  const sendToAgent = useCallback(
    async (
      agent: Agent,
      conversationHistory: { role: string; content: string }[],
      signal: AbortSignal
    ) => {
      setStreamingAgentId(agent.id)
      setStreamingContent("")

      const response = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          agentId: agent.id,
          messages: conversationHistory,
          customSystemPrompt: agent.systemPrompt,
          customModel: agent.model,
        }),
        signal,
      })

      if (!response.ok || !response.body) {
        throw new Error("Failed to get response from " + agent.name)
      }

      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let fullContent = ""
      let buffer = ""

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
              const parsed = JSON.parse(data)
              if (
                parsed.type === "text-delta" &&
                typeof parsed.textDelta === "string"
              ) {
                fullContent += parsed.textDelta
                setStreamingContent(fullContent)
              }
            } catch {
              /* skip */
            }
          }
        }
      }

      return fullContent
    },
    []
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
        <ChatHeader
          room={room}
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
          roomName={room.name}
          onSend={handleSend}
          disabled={isProcessing}
        />
      </div>

      {showMembers && (
        <MemberList
          allAgents={allAgents}
          activeAgentIds={room.agentIds}
          streamingAgentId={streamingAgentId}
          onToggleAgent={handleToggleAgent}
          onOpenInDM={onOpenInDM}
        />
      )}
    </div>
  )
}
