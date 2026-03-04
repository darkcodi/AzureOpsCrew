"use client"

import { PanelRightClose, PanelRightOpen } from "lucide-react"
import { ManualChatContainer } from "@/components/manual-chat-container"
import type { Agent } from "@/lib/agents"

interface DirectMessagesAreaProps {
  activeDMId: string | null
  agents: Agent[]
  showRightPane?: boolean
  onToggleRightPane?: () => void
}

export function DirectMessagesArea({
  activeDMId,
  agents,
  showRightPane = true,
  onToggleRightPane,
}: DirectMessagesAreaProps) {
  // Show empty state if no agent selected
  if (!activeDMId) {
    return (
      <div
        className="direct-messages-area flex flex-1 flex-col items-center justify-center"
        style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
      >
        <div style={{ color: "hsl(0, 0%, 100%)" }}>Select an agent to start messaging</div>
      </div>
    )
  }

  return (
    <div
      className="direct-messages-area flex flex-1 flex-col overflow-hidden"
      style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
    >
      {/* Header */}
      <header
        className="flex h-12 shrink-0 items-center gap-2 border-b px-4 shadow-sm"
        style={{
          borderColor: "hsl(228, 6%, 18%)",
          backgroundColor: "hsl(228, 6%, 22%)",
          color: "hsl(0, 0%, 100%)",
        }}
      >
        <h1 className="text-base font-semibold">Direct Messages</h1>
        {onToggleRightPane && (
          <div className="ml-auto flex items-center gap-1">
            <button
              type="button"
              onClick={onToggleRightPane}
              className="flex items-center justify-center rounded-md p-1.5 transition-colors hover:opacity-80"
              style={{
                color: showRightPane ? "hsl(0, 0%, 100%)" : "hsl(214, 5%, 55%)",
              }}
              aria-label={showRightPane ? "Hide agents panel" : "Show agents panel"}
            >
              {showRightPane ? (
                <PanelRightClose className="h-5 w-5" />
              ) : (
                <PanelRightOpen className="h-5 w-5" />
              )}
            </button>
          </div>
        )}
      </header>

      {/* Manual chat container with key for clean remount on agent switch */}
      <ManualChatContainer key={activeDMId} activeDMId={activeDMId} agents={agents} />
    </div>
  )
}
