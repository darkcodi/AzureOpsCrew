import React from "react"
import type { Metadata, Viewport } from "next"
import { Inter } from "next/font/google"
import { CopilotKit } from "@copilotkit/react-core"
import { CopilotPopup } from "@copilotkit/react-ui"
import "@copilotkit/react-ui/styles.css"

import "./globals.css"

const _inter = Inter({ subsets: ["latin"] })

export const metadata: Metadata = {
  title: "AgentHub - AI Chat",
  description: "Chat with AI agents in a Discord-like interface",
}

export const viewport: Viewport = {
  themeColor: "#1e1f22",
}

const copilotInstructions = `You are a helpful AI assistant for AgentHub. You can use tools to show rich UI:
- showStatusCard: display status (success, warning, error, loading) with title and description
- showDataTable: display structured data in a table (headers and rows)
- createTaskList: create a task list with completed/pending items
Use these tools when it helps the user (e.g. summarizing steps, showing metrics, listing tasks). Be concise and helpful.`

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html lang="en">
      <body className="font-sans antialiased overflow-hidden">
        <CopilotKit
          runtimeUrl="/api/copilotkit"
          showDevConsole={false}
        >
          {children}
          <CopilotPopup
            instructions={copilotInstructions}
            labels={{
              title: "Assistant",
              initial: "Need help? Ask me anything. I can show status cards, tables, and task lists.",
              placeholder: "Type a message...",
            }}
          />
        </CopilotKit>
      </body>
    </html>
  )
}
