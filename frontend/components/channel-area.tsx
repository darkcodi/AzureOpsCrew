"use client"

import { useState, useCallback, useEffect } from "react"
import type { Channel, Agent, ChatMessage } from "@/lib/agents"
import { ChannelHeader } from "@/components/channel-header"
import { MessageList } from "@/components/message-list"
import { MessageInput } from "@/components/message-input"
import { MemberList } from "@/components/member-list"
import type { HumanMember } from "@/lib/humans"
import { fetchWithErrorHandling } from "@/lib/fetch"

interface ChannelAreaProps {
  channel: Channel
  allAgents: Agent[]
  humans: HumanMember[]
  displayName: string
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
  displayName,
  onUpdateChannel,
  onAddAgent,
  onUpdateAgent,
  onDeleteAgent,
  onOpenInDM,
}: ChannelAreaProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [showMembers, setShowMembers] = useState(true)

  const activeAgents = allAgents.filter((a) => channel.agentIds.includes(a.id))

  // Load messages when channel changes
  useEffect(() => {
    const loadMessages = async () => {
      try {
        const response = await fetchWithErrorHandling(`/api/channels/${channel.id}/messages`)
        if (response.ok) {
          const data = await response.json()
          // Transform backend messages to frontend ChatMessage format
          const meResponse = await fetchWithErrorHandling('/api/auth/me')
          const user = meResponse.ok ? await meResponse.json() : null

          const chatMessages: ChatMessage[] = data.map((m: {
            id: string
            chatId: string
            content: string
            senderId: string
            postedAt: string
          }) => {
            const isUser = m.senderId === user?.id
            return {
              id: m.id,
              role: isUser ? 'user' : 'assistant',
              content: m.content,
              ...(isUser ? {} : { agentId: m.senderId }),
            }
          })
          setMessages(chatMessages)
        }
      } catch (err) {
        console.error("Failed to load channel messages:", err)
      }
    }

    loadMessages()
  }, [channel.id])

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
          streamingAgentId={null}
          streamingContent=""
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
          streamingAgentId={null}
          displayName={displayName}
          onToggleAgent={handleToggleAgent}
          onOpenInDM={onOpenInDM}
          onKickMember={handleKickMember}
        />
      )}
    </div>
  )
}
