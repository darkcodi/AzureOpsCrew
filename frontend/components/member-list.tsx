"use client"

import { useState } from "react"
import type { Agent } from "@/lib/agents"
import { ScrollArea } from "@/components/ui/scroll-area"
import { AgentProfilePopover } from "@/components/agent-profile-popover"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
  DropdownMenuCheckboxItem,
  DropdownMenuSeparator,
  DropdownMenuLabel,
} from "@/components/ui/dropdown-menu"
import { Settings } from "lucide-react"

interface MemberListProps {
  allAgents: Agent[]
  activeAgentIds: string[]
  streamingAgentId?: string | null
  onToggleAgent: (agentId: string) => void
  onOpenInDM?: (agentId: string, message?: string) => void
}

type StatusFilterMode = "all" | "free" | "working"

function AgentRow({
  agent,
  isInRoom,
  isWorking,
  onToggle,
  onOpenInDM,
}: {
  agent: Agent
  isInRoom: boolean
  isWorking: boolean
  onToggle: () => void
  onOpenInDM?: (agentId: string, message?: string) => void
}) {
  const handleClick = () => {
    if (!onOpenInDM) onToggle()
  }

  const row = (
    <div
      className="mb-1 flex items-center gap-3 rounded-md px-2 py-2 transition-colors cursor-pointer"
      onClick={handleClick}
      onMouseEnter={(e) => {
        e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.backgroundColor = "transparent"
      }}
    >
      <div
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-bold"
        style={{
          backgroundColor: agent.color,
          color: "#fff",
          opacity: isInRoom ? 1 : 0.4,
        }}
      >
        {agent.avatar}
      </div>
      <div className="flex min-w-0 flex-1 flex-col">
        <span
          className="truncate text-sm"
          style={{
            color: isInRoom ? "hsl(210, 3%, 90%)" : "hsl(214, 5%, 45%)",
          }}
        >
          {agent.name}
        </span>
        <span
          className="text-xs"
          style={{ color: "hsl(214, 5%, 55%)" }}
        >
          {isWorking ? "Working" : "Available"}
        </span>
      </div>
    </div>
  )

  if (onOpenInDM) {
    return (
      <AgentProfilePopover
        agent={agent}
        isWorking={isWorking}
        onOpenInDM={onOpenInDM}
      >
        {row}
      </AgentProfilePopover>
    )
  }
  return row
}

export function MemberList({
  allAgents,
  activeAgentIds,
  streamingAgentId = null,
  onToggleAgent,
  onOpenInDM,
}: MemberListProps) {
  const [statusFilter, setStatusFilter] = useState<StatusFilterMode>("all")

  const agentsInRoom = allAgents
    .filter((a) => activeAgentIds.includes(a.id))
    .sort((a, b) => a.name.localeCompare(b.name))

  const workingAgents = agentsInRoom.filter((a) => a.id === streamingAgentId)
  const availableAgents = agentsInRoom.filter((a) => a.id !== streamingAgentId)

  const popupAgents = allAgents
    .slice()
    .sort((a, b) => a.name.localeCompare(b.name))
    .filter((agent) => {
      const isWorking = agent.id === streamingAgentId
      if (statusFilter === "working") return isWorking
      if (statusFilter === "free") return !isWorking
      return true
    })

  return (
    <div
      className="flex h-full w-[220px] flex-col"
      style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
    >
      <div
        className="flex h-12 items-center justify-between px-4"
        style={{ borderBottom: "1px solid hsl(228, 6%, 10%)" }}
      >
        <span
          className="text-xs font-bold uppercase tracking-wider"
          style={{ color: "hsl(214, 5%, 55%)" }}
        >
          Members
        </span>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button
              type="button"
              className="flex items-center justify-center rounded-md p-1 transition-colors hover:opacity-80"
              style={{ color: "hsl(214, 5%, 55%)" }}
              aria-label="Settings and filter"
            >
              <Settings className="h-4 w-4" />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            align="end"
            className="max-h-[400px] overflow-y-auto"
            style={{
              backgroundColor: "hsl(228, 7%, 14%)",
              borderColor: "hsl(228, 6%, 10%)",
              color: "hsl(210, 3%, 90%)",
              minWidth: "240px",
            }}
          >
            <DropdownMenuLabel
              style={{
                color: "hsl(214, 5%, 55%)",
                fontSize: "11px",
                fontWeight: 600,
                textTransform: "uppercase",
                letterSpacing: "0.05em",
              }}
            >
              Status
            </DropdownMenuLabel>
            <DropdownMenuRadioGroup
              value={statusFilter}
              onValueChange={(value) => setStatusFilter(value as StatusFilterMode)}
            >
              <DropdownMenuRadioItem
                value="all"
                onSelect={(e) => e.preventDefault()}
                style={{ color: "hsl(210, 3%, 90%)" }}
              >
                All
              </DropdownMenuRadioItem>
              <DropdownMenuRadioItem
                value="free"
                onSelect={(e) => e.preventDefault()}
                style={{ color: "hsl(210, 3%, 90%)" }}
              >
                Available
              </DropdownMenuRadioItem>
              <DropdownMenuRadioItem
                value="working"
                onSelect={(e) => e.preventDefault()}
                style={{ color: "hsl(210, 3%, 90%)" }}
              >
                Working
              </DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
            <DropdownMenuSeparator
              style={{
                backgroundColor: "hsl(228, 6%, 10%)",
                marginTop: "8px",
                marginBottom: "8px",
              }}
            />
            <DropdownMenuLabel
              style={{
                color: "hsl(214, 5%, 55%)",
                fontSize: "11px",
                fontWeight: 600,
                textTransform: "uppercase",
                letterSpacing: "0.05em",
              }}
            >
              Agents
            </DropdownMenuLabel>
            {popupAgents.map((agent) => {
              const isInRoom = activeAgentIds.includes(agent.id)
              const isWorking = agent.id === streamingAgentId
              return (
                <DropdownMenuCheckboxItem
                  key={agent.id}
                  checked={isInRoom}
                  onCheckedChange={() => onToggleAgent(agent.id)}
                  onSelect={(e) => e.preventDefault()}
                  style={{ color: "hsl(210, 3%, 90%)" }}
                  className="flex items-center gap-2"
                >
                  <div
                    className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                    style={{
                      backgroundColor: agent.color,
                      color: "#fff",
                      opacity: isInRoom ? 1 : 0.4,
                    }}
                  >
                    {agent.avatar}
                  </div>
                  <span className="flex-1">{agent.name}</span>
                  <span
                    className="text-xs"
                    style={{ color: "hsl(214, 5%, 55%)" }}
                  >
                    {isWorking ? "Working" : "Available"}
                  </span>
                </DropdownMenuCheckboxItem>
              )
            })}
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <ScrollArea className="flex-1 px-3 pt-4">
          {workingAgents.length > 0 && (
            <>
              <div
                className="mb-1 mt-1 px-2 py-1 text-xs font-semibold uppercase tracking-wider"
                style={{ color: "hsl(214, 5%, 55%)" }}
              >
                Working
              </div>
              {workingAgents.map((agent) => (
                <AgentRow
                  key={agent.id}
                  agent={agent}
                  isInRoom
                  isWorking
                  onToggle={() => onToggleAgent(agent.id)}
                  onOpenInDM={onOpenInDM}
                />
              ))}
            </>
          )}
          {availableAgents.length > 0 && (
            <>
              <div
                className="mb-1 mt-2 px-2 py-1 text-xs font-semibold uppercase tracking-wider"
                style={{ color: "hsl(214, 5%, 55%)" }}
              >
                Available
              </div>
              {availableAgents.map((agent) => (
                <AgentRow
                  key={agent.id}
                  agent={agent}
                  isInRoom
                  isWorking={false}
                  onToggle={() => onToggleAgent(agent.id)}
                  onOpenInDM={onOpenInDM}
                />
              ))}
            </>
          )}
          {agentsInRoom.length === 0 && (
            <div
              className="px-2 py-4 text-center text-sm"
              style={{ color: "hsl(214, 5%, 55%)" }}
            >
              No agents in chat. Add them via the settings menu.
            </div>
          )}
        </ScrollArea>
    </div>
  )
}
