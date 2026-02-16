"use client"

import type { Channel } from "@/lib/agents"
import { Hash, PanelRightClose, PanelRightOpen } from "lucide-react"

interface ChannelHeaderProps {
  channel: Channel
  onManageAgents: () => void
  showMembers: boolean
  onToggleMembers: () => void
}

export function ChannelHeader({
  channel,
  onManageAgents,
  showMembers,
  onToggleMembers,
}: ChannelHeaderProps) {
  const agentCount = channel.agentIds.length

  return (
    <header
      className="flex h-12 items-center gap-2 px-4 shadow-sm"
      style={{
        borderBottom: "1px solid hsl(228, 6%, 14%)",
        backgroundColor: "hsl(228, 6%, 22%)",
      }}
    >
      <Hash className="h-5 w-5 shrink-0" style={{ color: "hsl(214, 5%, 55%)" }} />
      <span className="font-semibold" style={{ color: "hsl(0, 0%, 100%)" }}>
        {channel.name}
      </span>

      <span className="ml-2 text-sm" style={{ color: "hsl(214, 5%, 55%)" }}>
        {agentCount + " active agent" + (agentCount !== 1 ? "s" : "")}
      </span>

      <div className="ml-auto flex items-center gap-1">
        <button
          type="button"
          onClick={onToggleMembers}
          className="flex items-center justify-center rounded-md p-1.5 transition-colors hover:opacity-80"
          style={{
            color: showMembers ? "hsl(0, 0%, 100%)" : "hsl(214, 5%, 55%)",
          }}
          aria-label={showMembers ? "Hide agents panel" : "Show agents panel"}
        >
          {showMembers ? (
            <PanelRightClose className="h-5 w-5" />
          ) : (
            <PanelRightOpen className="h-5 w-5" />
          )}
        </button>
      </div>
    </header>
  )
}
