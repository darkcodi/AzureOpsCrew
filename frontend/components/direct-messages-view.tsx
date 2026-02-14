"use client"

import { useCallback, useEffect, useState } from "react"
import { useCopilotContext } from "@copilotkit/react-core"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"
import { DirectMessagesSidebar } from "@/components/direct-messages-sidebar"
import { DirectMessagesArea } from "@/components/direct-messages-area"
import { DirectMessagesRightPane, HUMAN_ID } from "@/components/direct-messages-right-pane"
import type { Agent } from "@/lib/agents"

interface DirectMessagesViewProps {
  activeDMId: string | null
  setActiveDMId: (id: string | null) => void
  agents: Agent[]
  pendingDMMessage?: string | null
  onClearPendingDMMessage?: () => void
}

export function DirectMessagesView({
  activeDMId,
  setActiveDMId,
  agents,
  pendingDMMessage = null,
  onClearPendingDMMessage,
}: DirectMessagesViewProps) {
  const { setThreadId } = useCopilotContext()
  const { setAgentId } = useAgentRuntime()
  const effectiveId = activeDMId ?? agents[0]?.id ?? null
  const [selectedCardId, setSelectedCardId] = useState<string | null>(() => activeDMId ?? agents[0]?.id ?? null)

  useEffect(() => {
    setAgentId(activeDMId)
    return () => setAgentId(null)
  }, [activeDMId, setAgentId])

  const handleSelectDM = useCallback(
    (id: string) => {
      setSelectedCardId(id)
      if (id === HUMAN_ID) {
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
    if (activeDMId && activeDMId !== HUMAN_ID) {
      setSelectedCardId((prev) =>
        prev === HUMAN_ID ? prev : activeDMId
      )
    }
  }, [activeDMId])

  return (
    <>
      <DirectMessagesSidebar
        agents={agents}
        activeId={activeDMId}
        selectedCardId={selectedCardId}
        onSelect={handleSelectDM}
      />
      <DirectMessagesArea
        activeDMId={activeDMId}
        agents={agents}
        pendingDMMessage={pendingDMMessage}
        onClearPendingDMMessage={onClearPendingDMMessage}
      />
      <DirectMessagesRightPane
        selectedCardId={selectedCardId}
        agents={agents}
      />
    </>
  )
}
