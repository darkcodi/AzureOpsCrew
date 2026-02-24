"use client"

import { useCallback, useEffect, useState } from "react"
import { useCopilotContext } from "@copilotkit/react-core"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"
import { DirectMessagesSidebar } from "@/components/direct-messages-sidebar"
import { DirectMessagesArea } from "@/components/direct-messages-area"
import { DirectMessagesRightPane } from "@/components/direct-messages-right-pane"
import type { Agent } from "@/lib/agents"
import type { HumanMember } from "@/lib/humans"
import { isHumanCardId } from "@/lib/humans"

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
  const { setAgentId } = useAgentRuntime()
  const effectiveId = activeDMId ?? agents[0]?.id ?? null
  const [selectedCardId, setSelectedCardId] = useState<string | null>(() => activeDMId ?? agents[0]?.id ?? null)
  const [showRightPane, setShowRightPane] = useState(true)

  useEffect(() => {
    setAgentId(activeDMId)
    return () => setAgentId(null)
  }, [activeDMId, setAgentId])

  const handleSelectDM = useCallback(
    (id: string) => {
      setSelectedCardId(id)
      if (isHumanCardId(id)) {
        return
      }
      setThreadId(id)
      setActiveDMId(id)
    },
    [setThreadId, setActiveDMId]
  )

  useEffect(() => {
    setThreadId(effectiveId ?? "")
  }, [effectiveId, setThreadId])

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
