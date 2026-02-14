"use client"

import { CopilotKit } from "@copilotkit/react-core"
import "@copilotkit/react-ui/styles.css"
import type { ReactNode } from "react"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"

function CopilotKitInner({ children }: { children: ReactNode }) {
  const { agentId } = useAgentRuntime()
  const runtimeUrl = agentId
    ? `/api/copilotkit/${agentId}`
    : "/api/copilotkit"
  return (
    <CopilotKit
      runtimeUrl={runtimeUrl}
      agent="aguiAgent"
      showDevConsole={process.env.NODE_ENV === "development"}
    >
      {children}
    </CopilotKit>
  )
}

export function CopilotKitProvider({ children }: { children: ReactNode }) {
  return <CopilotKitInner>{children}</CopilotKitInner>
}
