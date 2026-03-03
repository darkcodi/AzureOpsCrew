"use client"

import { CopilotKit } from "@copilotkit/react-core"
import "@copilotkit/react-ui/styles.css"
import { useEffect, useState, type ReactNode } from "react"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"

function CopilotKitInner({ children }: { children: ReactNode }) {
  const { agentId } = useAgentRuntime()
  const [isReady, setIsReady] = useState(false)

  // Ensure auth cookie exists before CopilotKit tries to sync
  useEffect(() => {
    let cancelled = false

    async function ensureAuth() {
      try {
        // Check if already authenticated
        const meResp = await fetch("/api/auth/me")
        if (meResp.ok) {
          if (!cancelled) setIsReady(true)
          return
        }

        // Not authenticated — trigger auto-login to set cookie
        const loginResp = await fetch("/api/auth/auto-login", { method: "POST" })
        if (!cancelled) {
          setIsReady(loginResp.ok)
        }
      } catch {
        // Even on error, let CopilotKit render so the page isn't blank forever
        if (!cancelled) setIsReady(true)
      }
    }

    void ensureAuth()
    return () => { cancelled = true }
  }, [])

  // Don't render CopilotKit until auth cookie is established
  if (!isReady) {
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
