"use client"

import { CopilotChat } from "@copilotkit/react-ui"
import type { Agent } from "@/lib/agents"

const DEFAULT_INSTRUCTIONS =
  "You are a helpful AI assistant for AzureOpsCrew. Respond in a direct, conversational way."

interface DirectMessagesAreaProps {
  activeDMId: string | null
  agents: Agent[]
}

export function DirectMessagesArea({ activeDMId, agents }: DirectMessagesAreaProps) {
  const threadId = activeDMId ?? "assistant"
  const selectedAgent = agents.find((a) => a.id === activeDMId)
  const instructions = selectedAgent
    ? selectedAgent.systemPrompt
    : DEFAULT_INSTRUCTIONS
  const placeholder = selectedAgent
    ? `Message @${selectedAgent.name}...`
    : "Message @AzureOpsCrew Assistant..."

  return (
    <div
      className="direct-messages-area flex flex-1 flex-col overflow-hidden"
      style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
    >
      {/* Header */}
      <div
        className="flex h-12 shrink-0 items-center border-b px-4"
        style={{
          borderColor: "hsl(228, 6%, 18%)",
          color: "hsl(0, 0%, 100%)",
        }}
      >
        <h1 className="text-base font-semibold">Direct Messages</h1>
      </div>

      {/* Chat: key forces remount when switching DM so messages/context switch */}
      <div className="flex min-h-0 flex-1 flex-col">
        <CopilotChat
          key={threadId}
          instructions={instructions}
          labels={{
            title: "Direct Messages",
            initial: "Send a message to start the conversation...",
            placeholder,
          }}
          className="h-full min-h-0 flex-1"
        />
      </div>
    </div>
  )
}
