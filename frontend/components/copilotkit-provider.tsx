"use client"

import { CopilotKit } from "@copilotkit/react-core"
import "@copilotkit/react-ui/styles.css"
import type { ReactNode } from "react"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"

function CopilotKitInner({ children }: { children: ReactNode }) {
  const { agentId } = useAgentRuntime()
  const base =
    typeof window !== "undefined"
      ? window.location.origin
      : ""
  const runtimeUrl = agentId
    ? `${base}/api/copilotkit/${agentId}`
    : `${base}/api/copilotkit`
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
