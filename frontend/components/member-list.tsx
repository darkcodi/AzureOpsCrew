"use client"

import { useState } from "react"
import type { Agent } from "@/lib/agents"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Input } from "@/components/ui/input"
import { AgentProfilePopover } from "@/components/agent-profile-popover"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"
import { Search, User, Plus } from "lucide-react"

interface MemberListProps {
  allAgents: Agent[]
  activeAgentIds: string[]
  streamingAgentId?: string | null
  onToggleAgent: (agentId: string) => void
  onOpenInDM?: (agentId: string, message?: string) => void
}

function AgentRow({
  agent,
  isInRoom,
  onToggle,
  onOpenInDM,
}: {
  agent: Agent
  isInRoom: boolean
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
          {agent.status ?? "Idle"}
        </span>
      </div>
    </div>
  )

  if (onOpenInDM) {
    return (
      <AgentProfilePopover
        agent={agent}
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
  const [searchQuery, setSearchQuery] = useState("")
  const [addAgentsOpen, setAddAgentsOpen] = useState(false)

  const agentsInRoom = allAgents
    .filter((a) => activeAgentIds.includes(a.id))
    .sort((a, b) => a.name.localeCompare(b.name))

  const agentsNotInRoom = allAgents
    .filter((a) => !activeAgentIds.includes(a.id))
    .sort((a, b) => a.name.localeCompare(b.name))

  const workingAgents = agentsInRoom.filter((a) => a.id === streamingAgentId)
  const availableAgents = agentsInRoom.filter((a) => a.id !== streamingAgentId)

  const query = searchQuery.trim().toLowerCase()
  const matchesSearch = (agent: Agent) =>
    !query || agent.name.toLowerCase().includes(query)
  const filteredWorking = workingAgents.filter(matchesSearch)
  const filteredAvailable = availableAgents.filter(matchesSearch)
  const humanLabel = "You"
  const matchesHuman = !query || humanLabel.toLowerCase().includes(query)

  return (
    <div
      className="flex h-full w-[220px] flex-col shrink-0"
      style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
    >
      {/* Header – same height, padding and border as left sidebar for alignment */}
      <div
        className="flex h-12 shrink-0 items-center px-4"
        style={{
          borderBottom: "1px solid hsl(228, 6%, 10%)",
        }}
      >
        <div className="relative w-full">
          <Search
            className="absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2"
            style={{ color: "hsl(214, 5%, 55%)" }}
          />
          <Input
            type="search"
            placeholder="Search members..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="h-9 pl-8 pr-3 text-sm"
            style={{
              backgroundColor: "hsl(228, 6%, 18%)",
              borderColor: "hsl(228, 6%, 10%)",
              color: "hsl(210, 3%, 90%)",
            }}
          />
        </div>
      </div>
      <ScrollArea className="flex-1 px-2 pt-3">
        {filteredWorking.length > 0 && (
          <>
            <div
              className="mb-1 mt-1 px-2 py-1 text-xs font-semibold uppercase tracking-wider"
              style={{ color: "hsl(214, 5%, 55%)" }}
            >
              Working
            </div>
            {filteredWorking.map((agent) => (
              <AgentRow
                key={agent.id}
                agent={agent}
                isInRoom
                onToggle={() => onToggleAgent(agent.id)}
                onOpenInDM={onOpenInDM}
              />
            ))}
          </>
        )}
        {(filteredAvailable.length > 0 || agentsInRoom.length === 0) && (
          <>
            <div
              className="mb-1 mt-2 px-2 py-1 text-xs font-semibold uppercase tracking-wider"
              style={{ color: "hsl(214, 5%, 55%)" }}
            >
              AI Agents
            </div>
            {filteredAvailable.map((agent) => (
              <AgentRow
                key={agent.id}
                agent={agent}
                isInRoom
                onToggle={() => onToggleAgent(agent.id)}
                onOpenInDM={onOpenInDM}
              />
            ))}
            {filteredAvailable.length === 0 && agentsInRoom.length === 0 && (
              <div
                className="px-2 py-2 text-center text-sm"
                style={{ color: "hsl(214, 5%, 55%)" }}
              >
                No agents in chat.
              </div>
            )}
            <button
              type="button"
              onClick={() => setAddAgentsOpen(true)}
              className="mb-1 flex w-full items-center justify-center gap-2 rounded-lg border-2 border-dashed px-3 py-2.5 text-left text-sm font-medium transition-all"
              style={{
                color: "hsl(214, 5%, 65%)",
                borderColor: "hsl(228, 6%, 32%)",
                backgroundColor: "transparent",
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
                e.currentTarget.style.borderColor = "hsl(235, 86%, 65%)"
                e.currentTarget.style.color = "hsl(210, 3%, 90%)"
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.backgroundColor = "transparent"
                e.currentTarget.style.borderColor = "hsl(228, 6%, 32%)"
                e.currentTarget.style.color = "hsl(214, 5%, 65%)"
              }}
            >
              <Plus className="h-4 w-4 shrink-0" />
              <span>Add more agents</span>
            </button>
          </>
        )}
        {matchesHuman && (
          <>
            <div
              className="mb-1 mt-8 px-2 py-1 text-xs font-semibold uppercase tracking-wider"
              style={{ color: "hsl(214, 5%, 55%)" }}
            >
              Humans
            </div>
            <Popover>
              <PopoverTrigger asChild>
                <button
                  type="button"
                  className="mb-1 flex w-full items-center gap-3 rounded-md px-2 py-2 text-left transition-colors cursor-pointer"
                  style={{ color: "hsl(210, 3%, 90%)" }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.backgroundColor = "transparent"
                  }}
                >
                  <div
                    className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs"
                    style={{
                      backgroundColor: "hsl(228, 6%, 24%)",
                      color: "hsl(214, 5%, 55%)",
                    }}
                  >
                    <User className="h-4 w-4" />
                  </div>
                  <div className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-sm">You</span>
                    <span
                      className="text-xs"
                      style={{ color: "hsl(214, 5%, 55%)" }}
                    >
                      Online
                    </span>
                  </div>
                </button>
              </PopoverTrigger>
              <PopoverContent
                align="start"
                side="left"
                sideOffset={16}
                className="z-[100] w-[340px] overflow-hidden rounded-xl border-0 p-0 bg-[hsl(228,7%,14%)] text-[hsl(210,3%,90%)] shadow-xl"
              >
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
                        Online
                      </div>
                    </div>
                  </div>
                  <div className="px-4 pt-14 pb-4">
                    <h3 className="text-xl font-bold" style={{ color: "hsl(210, 3%, 98%)" }}>
                      You
                    </h3>
                    <p className="text-sm" style={{ color: "hsl(214, 5%, 55%)" }}>
                      Human
                    </p>
                    <p
                      className="mt-3 text-sm leading-snug"
                      style={{ color: "hsl(210, 3%, 80%)" }}
                    >
                      Your profile and direct message threads.
                    </p>
                    <div className="mt-3 flex flex-wrap gap-1.5">
                      <span
                        className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
                        style={{
                          backgroundColor: "hsl(228, 6%, 22%)",
                          color: "hsl(210, 3%, 90%)",
                        }}
                      >
                        <span className="h-2 w-2 rounded-full bg-green-500" />
                        Human
                      </span>
                    </div>
                  </div>
                </div>
              </PopoverContent>
            </Popover>
          </>
        )}
        {query &&
          filteredWorking.length === 0 &&
          filteredAvailable.length === 0 &&
          !matchesHuman && (
            <div
              className="px-2 py-4 text-center text-sm"
              style={{ color: "hsl(214, 5%, 55%)" }}
            >
              No members match your search.
            </div>
          )}
      </ScrollArea>

      <Dialog open={addAgentsOpen} onOpenChange={setAddAgentsOpen}>
        <DialogContent
          className="max-w-md gap-0 border-0 p-0"
          style={{
            backgroundColor: "hsl(228, 6%, 20%)",
            color: "hsl(210, 3%, 90%)",
          }}
        >
          <DialogHeader
            className="px-5 py-4"
            style={{ borderBottom: "1px solid hsl(228, 6%, 28%)" }}
          >
            <DialogTitle className="text-lg font-semibold">
              Add agents to chat
            </DialogTitle>
          </DialogHeader>
          <ScrollArea className="max-h-[60vh] px-2 py-3">
            {agentsNotInRoom.length === 0 ? (
              <div
                className="px-3 py-6 text-center text-sm"
                style={{ color: "hsl(214, 5%, 55%)" }}
              >
                All agents are already in this chat.
              </div>
            ) : (
              agentsNotInRoom.map((agent) => (
                <AgentProfilePopover
                  key={agent.id}
                  agent={agent}
                >
                  <div
                    className="mb-1 flex w-full cursor-pointer items-center gap-3 rounded-md px-3 py-3 transition-colors"
                    onMouseEnter={(e) => {
                      e.currentTarget.style.backgroundColor = "hsl(228, 6%, 26%)"
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.backgroundColor = "transparent"
                    }}
                  >
                    <div
                      className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-sm font-bold"
                      style={{ backgroundColor: agent.color, color: "#fff" }}
                    >
                      {agent.avatar}
                    </div>
                    <div className="min-w-0 flex-1">
                      <span className="truncate text-sm font-medium">
                        {agent.name}
                      </span>
                    </div>
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation()
                        onToggleAgent(agent.id)
                      }}
                      className="shrink-0 rounded-md px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-90"
                      style={{
                        backgroundColor: "hsl(235, 86%, 65%)",
                        color: "#fff",
                      }}
                    >
                      Add
                    </button>
                  </div>
                </AgentProfilePopover>
              ))
            )}
          </ScrollArea>
        </DialogContent>
      </Dialog>
    </div>
  )
}
