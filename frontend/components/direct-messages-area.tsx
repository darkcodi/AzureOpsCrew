"use client"

import { useEffect, useRef } from "react"
import { useCopilotChatInternal } from "@copilotkit/react-core"
import { CopilotChat } from "@copilotkit/react-ui"
import { PanelRightClose, PanelRightOpen } from "lucide-react"
import { CopilotActions } from "@/components/copilot-actions"
import { DMMessages } from "@/components/dm-messages"
import { MessageInputAdapter } from "@/components/message-input"
import type { Agent } from "@/lib/agents"

const DEFAULT_INSTRUCTIONS =
  "You are a helpful AI assistant for AzureOpsCrew. Respond in a direct, conversational way."

interface DirectMessagesAreaProps {
  activeDMId: string | null
  agents: Agent[]
  pendingDMMessage?: string | null
  onClearPendingDMMessage?: () => void
  showRightPane?: boolean
  onToggleRightPane?: () => void
  isLoadingHistory?: boolean
}

function SendPendingMessage({
  pendingDMMessage,
  activeDMId,
  onClear,
}: {
  pendingDMMessage: string
  activeDMId: string
  onClear: () => void
}) {
  const { sendMessage } = useCopilotChatInternal()
  const sentRef = useRef(false)

  useEffect(() => {
    if (sentRef.current || !pendingDMMessage || !activeDMId) return
    sentRef.current = true
    const id = crypto.randomUUID()
    const timer = setTimeout(() => {
      sendMessage({
        id,
        role: "user",
        content: pendingDMMessage,
      })
        .then(() => onClear())
        .catch(() => {})
        .finally(() => {
          sentRef.current = false
        })
    }, 250)
    return () => clearTimeout(timer)
  }, [pendingDMMessage, activeDMId, sendMessage, onClear])

  return null
}

export function DirectMessagesArea({
  activeDMId,
  agents,
  pendingDMMessage = null,
  onClearPendingDMMessage,
  showRightPane = true,
  onToggleRightPane,
  isLoadingHistory = false,
}: DirectMessagesAreaProps) {
  const threadId = activeDMId ?? "assistant"
  const selectedAgent = agents.find((a) => a.id === activeDMId)
  const instructions = selectedAgent
    ? selectedAgent.systemPrompt
    : DEFAULT_INSTRUCTIONS
  const placeholder = selectedAgent
    ? `Message @${selectedAgent.name}...`
    : "Message @AzureOpsCrew Assistant..."

  // Show loading indicator while fetching history
  if (isLoadingHistory && activeDMId) {
    return (
      <div
        className="direct-messages-area flex flex-1 flex-col items-center justify-center"
        style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
      >
        <div style={{ color: "hsl(0, 0%, 100%)" }}>Loading conversation...</div>
      </div>
    )
  }

  return (
    <div
      className="direct-messages-area flex flex-1 flex-col overflow-hidden"
      style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
    >
      {pendingDMMessage && activeDMId && onClearPendingDMMessage && (
        <SendPendingMessage
          pendingDMMessage={pendingDMMessage}
          activeDMId={activeDMId}
          onClear={onClearPendingDMMessage}
        />
      )}
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

      {/* Register dynamic UI actions (pipeline, work items, resources, etc.) */}
      <CopilotActions />

      {/* Chat: NO KEY PROP - let CopilotKit handle thread switching */}
      <div className="flex min-h-0 flex-1 flex-col">
        <CopilotChat
          instructions={instructions}
          labels={{
            title: "Direct Messages",
            placeholder,
          }}
          className="h-full min-h-0 flex-1"
          Messages={DMMessages}
          Input={(props) => (
            <MessageInputAdapter {...props} placeholder={placeholder} />
          )}
        />
      </div>
    </div>
  )
}
