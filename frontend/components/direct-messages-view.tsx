"use client"

import { useCallback, useEffect } from "react"
import { useCopilotContext } from "@copilotkit/react-core"
import { DirectMessagesSidebar } from "@/components/direct-messages-sidebar"
import { DirectMessagesArea } from "@/components/direct-messages-area"
import type { Agent } from "@/lib/agents"

interface DirectMessagesViewProps {
  activeDMId: string | null
  setActiveDMId: (id: string | null) => void
  agents: Agent[]
}

/**
 * Wraps DM sidebar + area and syncs CopilotKit threadId with selected DM.
 * When user clicks a DM, we set threadId first so the chat uses the correct thread/context.
 */
export function DirectMessagesView({
  activeDMId,
  setActiveDMId,
  agents,
}: DirectMessagesViewProps) {
  const { setThreadId } = useCopilotContext()
  const effectiveId = activeDMId ?? "assistant"

  const handleSelectDM = useCallback(
    (id: string) => {
      setThreadId(id)
      setActiveDMId(id)
    },
    [setThreadId, setActiveDMId]
  )

  useEffect(() => {
    setThreadId(effectiveId)
  }, [effectiveId, setThreadId])

  return (
    <>
      <DirectMessagesSidebar
        agents={agents}
        activeId={activeDMId}
        onSelect={handleSelectDM}
      />
      <DirectMessagesArea activeDMId={activeDMId} agents={agents} />
    </>
  )
}
