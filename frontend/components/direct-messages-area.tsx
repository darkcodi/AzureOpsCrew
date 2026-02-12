"use client"

import { CopilotChat } from "@copilotkit/react-ui"

export function DirectMessagesArea() {
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

      {/* Chat */}
      <div className="flex min-h-0 flex-1 flex-col">
        <CopilotChat
          instructions="You are a helpful AI assistant for AzureOpsCrew. Respond in a direct, conversational way."
          labels={{
            title: "Direct Messages",
            initial: "Send a message to start the conversation...",
            placeholder: "Message @AzureOpsCrew Assistant...",
          }}
          className="h-full min-h-0 flex-1"
        />
      </div>
    </div>
  )
}
