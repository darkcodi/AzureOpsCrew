"use client"

import { useState } from "react"
import { User, Brain } from "lucide-react"
import type { Agent } from "@/lib/agents"
import type { HumanMember } from "@/lib/humans"
import { AgentMindModal } from "@/components/agent-mind-modal"

interface DirectMessagesRightPaneProps {
  /** Agent id or human id (e.g. "human:1"). null to show nothing. */
  selectedCardId: string | null
  agents: Agent[]
  humans: HumanMember[]
}

function AgentCard({ agent }: { agent: Agent }) {
  const bio =
    agent.systemPrompt.length > 120
      ? agent.systemPrompt.slice(0, 120).trim() + "..."
      : agent.systemPrompt

  return (
    <div
      className="flex flex-col overflow-hidden rounded-lg"
      style={{
        backgroundColor: "hsl(228, 7%, 14%)",
        color: "hsl(210, 3%, 90%)",
      }}
    >
      <div className="relative">
        <div
          className="h-20 w-full rounded-t-lg"
          style={{ backgroundColor: agent.color }}
        />
        <div className="absolute left-4 top-10 flex items-center">
          <div
            className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full border-4 text-xl font-bold"
            style={{
              backgroundColor: agent.color,
              borderColor: "hsl(228, 7%, 14%)",
              color: "#fff",
            }}
          >
            {agent.avatar}
          </div>
        </div>
        <div className="absolute right-4 top-3">
          <div
            className="flex h-6 shrink-0 items-center justify-center rounded-full px-3 text-xs font-medium leading-none"
            style={{
              backgroundColor: "hsla(0,0%,0%,0.4)",
              color: "#fff",
            }}
          >
            {agent.status ?? "Idle"}
          </div>
        </div>
      </div>
      <div className="px-4 pt-14 pb-4">
        <h3 className="text-xl font-bold" style={{ color: "hsl(210, 3%, 98%)" }}>
          {agent.name}
        </h3>
        <p className="text-sm" style={{ color: "hsl(214, 5%, 55%)" }}>
          {agent.id}
        </p>
        <p
          className="mt-3 line-clamp-3 text-sm leading-snug"
          style={{ color: "hsl(210, 3%, 80%)" }}
        >
          {bio}
        </p>
        <div className="mt-3 flex flex-wrap gap-1.5">
          <span
            className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
            style={{
              backgroundColor: "hsl(228, 6%, 22%)",
              color: "hsl(210, 3%, 90%)",
            }}
          >
            <span
              className="h-2 w-2 rounded-full"
              style={{ backgroundColor: "hsl(235, 86%, 65%)" }}
            />
            Agent
          </span>
          <span
            className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
            style={{
              backgroundColor: "hsl(228, 6%, 22%)",
              color: "hsl(214, 5%, 55%)",
            }}
          >
            {agent.model.split("/").pop() ?? agent.model}
          </span>
        </div>
      </div>
    </div>
  )
}

function HumanCard({
  name = "You",
  status = "Online",
  description = "Your profile and direct message threads.",
}: {
  name?: string
  status?: string
  description?: string
}) {
  const isOnline = status === "Online"
  return (
    <div
      className="flex flex-col overflow-hidden rounded-lg"
      style={{
        backgroundColor: "hsl(228, 7%, 14%)",
        color: "hsl(210, 3%, 90%)",
      }}
    >
      <div className="relative">
        <div
          className="h-20 w-full rounded-t-lg"
          style={{ backgroundColor: "hsl(228, 6%, 24%)" }}
        />
        <div className="absolute left-4 top-10 flex items-center">
          <div
            className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full border-4 text-xl"
            style={{
              backgroundColor: "hsl(228, 6%, 24%)",
              borderColor: "hsl(228, 7%, 14%)",
              color: "hsl(214, 5%, 55%)",
            }}
          >
            <User className="h-8 w-8" />
          </div>
        </div>
        <div className="absolute right-4 top-3">
          <div
            className="flex h-6 shrink-0 items-center justify-center rounded-full px-3 text-xs font-medium leading-none"
            style={{
              backgroundColor: "hsla(0,0%,0%,0.4)",
              color: "#fff",
            }}
          >
            {status}
          </div>
        </div>
      </div>
      <div className="px-4 pt-14 pb-4">
        <h3 className="text-xl font-bold" style={{ color: "hsl(210, 3%, 98%)" }}>
          {name}
        </h3>
        <p className="text-sm" style={{ color: "hsl(214, 5%, 55%)" }}>
          Human
        </p>
        <p
          className="mt-3 text-sm leading-snug"
          style={{ color: "hsl(210, 3%, 80%)" }}
        >
          {description}
        </p>
        <div className="mt-3 flex flex-wrap gap-1.5">
          <span
            className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
            style={{
              backgroundColor: "hsl(228, 6%, 22%)",
              color: "hsl(210, 3%, 90%)",
            }}
          >
            <span
              className={`h-2 w-2 rounded-full ${isOnline ? "bg-green-500" : "bg-gray-500"}`}
            />
            Human
          </span>
        </div>
      </div>
    </div>
  )
}

export function DirectMessagesRightPane({
  selectedCardId,
  agents,
  humans,
}: DirectMessagesRightPaneProps) {
  const [showAgentMindModal, setShowAgentMindModal] = useState(false)

  if (!selectedCardId) {
    return (
      <div
        className="flex h-full w-[220px] flex-col shrink-0 items-center justify-center px-4"
        style={{
          backgroundColor: "hsl(228, 7%, 14%)",
          color: "hsl(214, 5%, 55%)",
        }}
      >
        <p className="text-center text-sm">Select a conversation to view profile.</p>
      </div>
    )
  }

  const selectedHuman = humans.find((h) => h.id === selectedCardId)
  if (selectedHuman) {
    return (
      <div
        className="flex h-full w-[280px] flex-col shrink-0 overflow-auto px-3 pt-4"
        style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
      >
        <HumanCard
          name={selectedHuman.name}
          status={selectedHuman.status}
          description={
            selectedHuman.isCurrentUser
              ? "Your profile and direct message threads."
              : "Profile and direct message threads."
          }
        />
      </div>
    )
  }

  const agent = agents.find((a) => a.id === selectedCardId)
  if (!agent) {
    return (
      <div
        className="flex h-full w-[220px] flex-col shrink-0 items-center justify-center px-4"
        style={{
          backgroundColor: "hsl(228, 7%, 14%)",
          color: "hsl(214, 5%, 55%)",
        }}
      >
        <p className="text-center text-sm">Select a conversation to view profile.</p>
      </div>
    )
  }

  return (
    <div
      className="flex h-full w-[280px] flex-col shrink-0 overflow-auto px-3 pt-4"
      style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
    >
      <button
        type="button"
        onClick={() => setShowAgentMindModal(true)}
        className="mb-3 flex w-full items-center justify-center gap-2 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors hover:opacity-90"
        style={{
          backgroundColor: "hsl(228, 6%, 22%)",
          color: "hsl(210, 3%, 98%)",
        }}
      >
        <Brain className="h-4 w-4 shrink-0" />
        View Agent Mind
      </button>
      <AgentCard agent={agent} />
      <AgentMindModal
        open={showAgentMindModal}
        onOpenChange={setShowAgentMindModal}
        agentId={agent.id}
        agent={agent}
      />
    </div>
  )
}
