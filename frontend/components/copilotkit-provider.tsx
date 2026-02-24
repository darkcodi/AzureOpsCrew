"use client"

import { CopilotKit } from "@copilotkit/react-core"
import "@copilotkit/react-ui/styles.css"
import type { ReactNode } from "react"
import { usePathname } from "next/navigation"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"

function CopilotKitInner({ children }: { children: ReactNode }) {
  const pathname = usePathname()
  const { agentId } = useAgentRuntime()

  // Copilot runtime requires auth cookie. On public auth pages we disable
  // Copilot to avoid runtime sync errors before user signs in.
  if (pathname === "/login" || pathname === "/signup") {
    return <>{children}</>
  }

  const runtimeUrl = agentId
    ? `/api/copilotkit/${agentId}`
    : "/api/copilotkit"

  return (
    <CopilotKit
      runtimeUrl={runtimeUrl}
      agent="aguiAgent"
      showDevConsole={false}
      enableInspector={false}
    >
      {children}
    </CopilotKit>
  )
}

export function CopilotKitProvider({ children }: { children: ReactNode }) {
  return <CopilotKitInner>{children}</CopilotKitInner>
}
