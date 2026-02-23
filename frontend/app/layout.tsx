import React from "react"
import type { Metadata, Viewport } from "next"

import { AgentRuntimeProvider } from "@/contexts/agent-runtime-context"
import { CopilotKitProvider } from "@/components/copilotkit-provider"
import { Toaster } from "@/components/ui/toaster"
import "./globals.css"

export const metadata: Metadata = {
  title: "AgentHub - AI Chat",
  description: "Chat with AI agents in a Discord-like interface",
  icons: {
    icon: "/placeholder-logo.svg",
  },
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
    <html lang="en" suppressHydrationWarning>
      <body className="font-sans antialiased overflow-hidden">
        <AgentRuntimeProvider>
          <CopilotKitProvider>{children}</CopilotKitProvider>
          <Toaster />
        </AgentRuntimeProvider>
      </body>
    </html>
  )
}
