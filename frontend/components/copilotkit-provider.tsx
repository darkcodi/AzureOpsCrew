"use client"

import { CopilotKit } from "@copilotkit/react-core"
import { CopilotPopup } from "@copilotkit/react-ui"
import "@copilotkit/react-ui/styles.css"
import type { ReactNode } from "react"

const runtimeUrl =
  typeof window !== "undefined"
    ? `${window.location.origin}/api/copilotkit`
    : "/api/copilotkit"

export function CopilotKitProvider({ children }: { children: ReactNode }) {
  return (
    <CopilotKit
      runtimeUrl={runtimeUrl}
      agent="aguiAgent"
      showDevConsole={process.env.NODE_ENV === "development"}
    >
      {children}
      <CopilotPopup
        instructions="You are a helpful AI assistant for the AzureOpsCrew team."
        labels={{
          title: "AzureOpsCrew Assistant",
          initial: "Ask me anything...",
        }}
      />
    </CopilotKit>
  )
}
