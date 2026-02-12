"use client"

import { useCallback, useEffect } from "react"
import { useCopilotContext } from "@copilotkit/react-core"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"
import { DirectMessagesSidebar } from "@/components/direct-messages-sidebar"
import { DirectMessagesArea } from "@/components/direct-messages-area"
import type { Agent } from "@/lib/agents"

interface DirectMessagesViewProps {
  activeDMId: string | null
  setActiveDMId: (id: string | null) => void
  agents: Agent[]
}

export function DirectMessagesView({
  activeDMId,
  setActiveDMId,
  agents,
}: DirectMessagesViewProps) {
  const { setThreadId } = useCopilotContext()
  const { setAgentId } = useAgentRuntime()
  const effectiveId = activeDMId ?? agents[0]?.id ?? null

  useEffect(() => {
    setAgentId(activeDMId)
    return () => setAgentId(null)
  }, [activeDMId, setAgentId])

  const handleSelectDM = useCallback(
    (id: string) => {
      setThreadId(id)
      setActiveDMId(id)
    },
    [setThreadId, setActiveDMId]
  )

  useEffect(() => {
    setThreadId(effectiveId ?? "")
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
