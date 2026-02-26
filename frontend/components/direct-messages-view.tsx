"use client"

import { useCallback, useEffect, useState, useRef } from "react"
import { useCopilotContext, useCopilotChatInternal } from "@copilotkit/react-core"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"
import { DirectMessagesSidebar } from "@/components/direct-messages-sidebar"
import { DirectMessagesArea } from "@/components/direct-messages-area"
import { DirectMessagesRightPane } from "@/components/direct-messages-right-pane"
import { fetchWithErrorHandling } from "@/lib/fetch"
import type { Agent } from "@/lib/agents"
import type { HumanMember } from "@/lib/humans"
import { isHumanCardId } from "@/lib/humans"
import type { Message } from "@copilotkit/shared"

interface ChatHistoryMessage {
  id: string
  role: "user" | "assistant"
  content: string
  timestamp: string
}

interface DirectMessagesViewProps {
  activeDMId: string | null
  setActiveDMId: (id: string | null) => void
  agents: Agent[]
  humans: HumanMember[]
  pendingDMMessage?: string | null
  onClearPendingDMMessage?: () => void
}

export function DirectMessagesView({
  activeDMId,
  setActiveDMId,
  agents,
  humans,
  pendingDMMessage = null,
  onClearPendingDMMessage,
}: DirectMessagesViewProps) {
  const { setThreadId } = useCopilotContext()
  const { setMessages } = useCopilotChatInternal()
  const { setAgentId } = useAgentRuntime()
  const effectiveId = activeDMId ?? agents[0]?.id ?? null
  const [selectedCardId, setSelectedCardId] = useState<string | null>(() => activeDMId ?? agents[0]?.id ?? null)
  const [showRightPane, setShowRightPane] = useState(true)
  const [isLoadingHistory, setIsLoadingHistory] = useState(false)

  // Track which agents have had their history loaded
  const loadedAgentsRef = useRef<Set<string>>(new Set())

  useEffect(() => {
    setAgentId(activeDMId)
    return () => setAgentId(null)
  }, [activeDMId, setAgentId])

  // Load message history when switching to an agent
  // This MUST run before the threadId is set
  useEffect(() => {
    if (!activeDMId) {
      loadedAgentsRef.current.clear()
      return
    }

    // Skip if already loaded for this agent
    if (loadedAgentsRef.current.has(activeDMId)) {
      // Still need to set the threadId for already-loaded agents
      setThreadId(activeDMId)
      return
    }

    const agentId = activeDMId
    setIsLoadingHistory(true)

    async function loadHistory() {
      try {
        const response = await fetchWithErrorHandling(`/api/chat-history/agents/${agentId}`)
        if (response.ok) {
          const data = await response.json() as { messages: ChatHistoryMessage[] }

          // Convert to CopilotKit message format
          const copilotMessages: Message[] = data.messages.map(msg => ({
            id: msg.id,
            role: msg.role as "user" | "assistant",
            content: msg.content,
          }))

          // CRITICAL: Set messages BEFORE setting threadId
          setMessages(copilotMessages)
          loadedAgentsRef.current.add(agentId)

          // Now set the thread ID after messages are loaded
          setThreadId(agentId)
        }
      } catch (error) {
        console.error("Failed to load chat history:", error)
        // Still set threadId even if history load fails
        setThreadId(agentId)
      } finally {
        setIsLoadingHistory(false)
      }
    }

    loadHistory()
  }, [activeDMId, setMessages, setThreadId])

  const handleSelectDM = useCallback(
    (id: string) => {
      setSelectedCardId(id)
      if (isHumanCardId(id)) {
        return
      }
      setActiveDMId(id)
      // NOTE: setThreadId is now called in the useEffect after messages are loaded
    },
    [setActiveDMId]
  )

  useEffect(() => {
    if (activeDMId) {
      setSelectedCardId((prev) =>
        prev && isHumanCardId(prev) ? prev : activeDMId
      )
    }
  }, [activeDMId])

  return (
    <>
      <DirectMessagesSidebar
        agents={agents}
        humans={humans}
        activeId={activeDMId}
        selectedCardId={selectedCardId}
        onSelect={handleSelectDM}
      />
      <DirectMessagesArea
        activeDMId={activeDMId}
        agents={agents}
        pendingDMMessage={pendingDMMessage}
        onClearPendingDMMessage={onClearPendingDMMessage}
        showRightPane={showRightPane}
        onToggleRightPane={() => setShowRightPane((prev) => !prev)}
        isLoadingHistory={isLoadingHistory}
      />
      {showRightPane && (
        <DirectMessagesRightPane
          selectedCardId={selectedCardId}
          agents={agents}
          humans={humans}
        />
      )}
    </>
  )
}
