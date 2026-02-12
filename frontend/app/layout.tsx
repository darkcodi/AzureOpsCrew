import React from "react"
import type { Metadata, Viewport } from "next"
import { Inter } from "next/font/google"

import { AgentRuntimeProvider } from "@/contexts/agent-runtime-context"
import { CopilotKitProvider } from "@/components/copilotkit-provider"
import "./globals.css"

const _inter = Inter({ subsets: ["latin"] })

export const metadata: Metadata = {
  title: "AgentHub - AI Chat",
  description: "Chat with AI agents in a Discord-like interface",
}

export const viewport: Viewport = {
  themeColor: "#1e1f22",
}

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html lang="en">
      <body className="font-sans antialiased overflow-hidden">
        <AgentRuntimeProvider>
          <CopilotKitProvider>{children}</CopilotKitProvider>
        </AgentRuntimeProvider>
      </body>
    </html>
  )
}
