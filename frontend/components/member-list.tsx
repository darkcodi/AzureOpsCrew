"use client"

import { useState } from "react"
import type { Agent } from "@/lib/agents"
import { useToast } from "@/hooks/use-toast"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Input } from "@/components/ui/input"
import { AgentProfilePopover } from "@/components/agent-profile-popover"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuTrigger,
} from "@/components/ui/context-menu"
import { HUMANS } from "@/components/direct-messages-right-pane"
import { Search, User, Plus, Loader2 } from "lucide-react"

function MemberContextMenu({
  userId,
  displayName,
  onCopyId,
  onKickClick,
  onOpenInDM,
  children,
}: {
  userId: string
  displayName?: string
  onCopyId: (id: string) => void
  onKickClick?: (id: string, name: string) => void
  onOpenInDM?: (dmId: string) => void
  children: React.ReactNode
}) {
  return (
    <ContextMenu>
      <ContextMenuContent
        className="min-w-[180px] rounded-lg border-0 p-1 shadow-lg"
        style={{
          backgroundColor: "rgb(53, 54, 59)",
          color: "rgb(255, 255, 255)",
        }}
      >
        <ContextMenuItem
          className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white"
          onSelect={() => onOpenInDM?.(userId)}
        >
          Message
        </ContextMenuItem>
        {onKickClick != null && (
          <ContextMenuItem
            className="cursor-pointer rounded px-2 py-1.5 text-sm text-red-500 focus:bg-white/10 focus:text-red-500"
            onSelect={() => onKickClick(userId, displayName ?? userId)}
          >
            Kick
          </ContextMenuItem>
        )}
        <ContextMenuItem
          className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white"
          onSelect={() => onCopyId(userId)}
        >
          Copy User ID
        </ContextMenuItem>
      </ContextMenuContent>
      <ContextMenuTrigger asChild>{children}</ContextMenuTrigger>
    </ContextMenu>
  )
}

interface MemberListProps {
  allAgents: Agent[]
  activeAgentIds: string[]
  streamingAgentId?: string | null
  onToggleAgent: (agentId: string) => void | Promise<void>
  onOpenInDM?: (agentId: string, message?: string) => void
  onKickMember?: (agentId: string) => void | Promise<void>
}

function AgentRow({
  agent,
  isInRoom,
  onToggle,
  onOpenInDM,
  onCopyId,
  onKickClick,
}: {
  agent: Agent
  isInRoom: boolean
  onToggle: () => void
  onOpenInDM?: (agentId: string, message?: string) => void
  onCopyId: (id: string) => void
  onKickClick?: (id: string, name: string) => void
}) {
  const handleClick = () => {
    if (!onOpenInDM) onToggle()
  }

  const rowContent = (
    <div className="flex min-w-0 flex-1 items-center gap-3">
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

  const wrapper = (
    <div
      className="mb-1 flex items-center gap-3 rounded-md px-2 py-2 transition-colors cursor-pointer"
      onClick={!onOpenInDM ? handleClick : undefined}
      onMouseEnter={(e) => {
        e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.backgroundColor = "transparent"
      }}
    >
      {onOpenInDM ? (
        <AgentProfilePopover agent={agent} onOpenInDM={onOpenInDM}>
          {rowContent}
        </AgentProfilePopover>
      ) : (
        rowContent
      )}
    </div>
  )

  return (
    <MemberContextMenu
      userId={agent.id}
      displayName={agent.name}
      onCopyId={onCopyId}
      onKickClick={onKickClick}
      onOpenInDM={onOpenInDM}
    >
      {wrapper}
    </MemberContextMenu>
  )
}

export function MemberList({
  allAgents,
  activeAgentIds,
  streamingAgentId = null,
  onToggleAgent,
  onOpenInDM,
  onKickMember,
}: MemberListProps) {
  const [searchQuery, setSearchQuery] = useState("")
  const [addAgentsOpen, setAddAgentsOpen] = useState(false)
  const [addingAgentIds, setAddingAgentIds] = useState<Set<string>>(new Set())
  const [kickPending, setKickPending] = useState<{ id: string; name: string } | null>(null)
  const [isKicking, setIsKicking] = useState(false)
  const { toast } = useToast()

  const handleKickClick = (id: string, name: string) => {
    setKickPending({ id, name })
  }

  const handleCopyId = (id: string) => {
    navigator.clipboard.writeText(id).then(
      () => toast({ title: "User ID copied to clipboard" }),
      () => toast({ title: "Failed to copy", variant: "destructive" })
    )
  }

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
  const filteredHumans = HUMANS.filter(
    (h) => !query || h.name.toLowerCase().includes(query)
  )
  const matchesHuman = filteredHumans.length > 0

  return (
    <div
      className="flex h-full w-[280px] flex-col shrink-0"
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
                onCopyId={handleCopyId}
                onKickClick={onKickMember ? handleKickClick : undefined}
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
                onCopyId={handleCopyId}
                onKickClick={onKickMember ? handleKickClick : undefined}
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
            {filteredHumans.map((human) =>
              human.name === "You" ? (
                <MemberContextMenu
                  key={human.id}
                  userId={human.id}
                  onCopyId={handleCopyId}
                  onOpenInDM={onOpenInDM}
                >
                  <div
                    className="mb-1 flex w-full items-center gap-3 rounded-md px-2 py-2 cursor-pointer"
                    style={{ color: "hsl(210, 3%, 90%)" }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.backgroundColor = "transparent"
                    }}
                  >
                  <Popover>
                    <PopoverTrigger asChild>
                      <button
                        type="button"
                        className="flex w-full items-center gap-3 rounded-md py-0 text-left transition-colors cursor-pointer min-w-0"
                        style={{ color: "inherit", backgroundColor: "transparent", border: "none" }}
                        onMouseEnter={(e) => {
                          e.currentTarget.style.backgroundColor = "transparent"
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
                          className="inline-flex items-center gap-1.5 text-xs"
                          style={{ color: "hsl(214, 5%, 55%)" }}
                        >
                          <span className="h-2 w-2 shrink-0 rounded-full bg-green-500" />
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
                  </div>
                </MemberContextMenu>
              ) : (
                <MemberContextMenu
                  key={human.id}
                  userId={human.id}
                  onCopyId={handleCopyId}
                  onOpenInDM={onOpenInDM}
                >
                  <div
                    className="mb-1 flex w-full items-center gap-3 rounded-md px-2 py-2 cursor-pointer"
                    style={{ color: "hsl(210, 3%, 90%)" }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.backgroundColor = "transparent"
                    }}
                  >
                  <Popover>
                    <PopoverTrigger asChild>
                      <button
                        type="button"
                        className="flex w-full items-center gap-3 rounded-md py-0 text-left transition-colors cursor-pointer min-w-0"
                        style={{ color: "inherit", backgroundColor: "transparent", border: "none" }}
                        onMouseEnter={(e) => {
                          e.currentTarget.style.backgroundColor = "transparent"
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
                        <span className="truncate text-sm">{human.name}</span>
                        <span
                          className="inline-flex items-center gap-1.5 text-xs"
                          style={{ color: "hsl(214, 5%, 55%)" }}
                        >
                          {human.status === "Online" && (
                            <span className="h-2 w-2 shrink-0 rounded-full bg-green-500" />
                          )}
                          {human.status}
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
                            {human.status}
                          </div>
                        </div>
                      </div>
                      <div className="px-4 pt-14 pb-4">
                        <h3 className="text-xl font-bold" style={{ color: "hsl(210, 3%, 98%)" }}>
                          {human.name}
                        </h3>
                        <p className="text-sm" style={{ color: "hsl(214, 5%, 55%)" }}>
                          Human
                        </p>
                        <p
                          className="mt-3 text-sm leading-snug"
                          style={{ color: "hsl(210, 3%, 80%)" }}
                        >
                          Profile and direct message threads.
                        </p>
                        <div className="mt-3 flex flex-wrap gap-1.5">
                          <span
                            className="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
                            style={{
                              backgroundColor: "hsl(228, 6%, 22%)",
                              color: "hsl(210, 3%, 90%)",
                            }}
                          >
                            <span className="h-2 w-2 rounded-full bg-gray-500" />
                            Human
                          </span>
                        </div>
                      </div>
                    </div>
                  </PopoverContent>
                </Popover>
                  </div>
                </MemberContextMenu>
              )
            )}
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

      <Dialog
        open={addAgentsOpen}
        onOpenChange={(open) => {
          setAddAgentsOpen(open)
          if (!open) setAddingAgentIds(new Set())
        }}
      >
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
                      disabled={addingAgentIds.has(agent.id)}
                      onClick={async (e) => {
                        e.stopPropagation()
                        setAddingAgentIds((prev) => new Set(prev).add(agent.id))
                        try {
                          await onToggleAgent(agent.id)
                        } catch {
                          toast({
                            title: "Failed to add agent to chat",
                            variant: "destructive",
                          })
                        } finally {
                          setAddingAgentIds((prev) => {
                            const next = new Set(prev)
                            next.delete(agent.id)
                            return next
                          })
                        }
                      }}
                      className="flex shrink-0 items-center justify-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-90 disabled:pointer-events-none disabled:opacity-70"
                      style={{
                        backgroundColor: "hsl(235, 86%, 65%)",
                        color: "#fff",
                      }}
                    >
                      {addingAgentIds.has(agent.id) ? (
                        <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      ) : (
                        "Add"
                      )}
                    </button>
                  </div>
                </AgentProfilePopover>
              ))
            )}
          </ScrollArea>
        </DialogContent>
      </Dialog>

      <Dialog
        open={!!kickPending}
        onOpenChange={(open) => {
          if (!open) {
            setKickPending(null)
            setIsKicking(false)
          }
        }}
      >
        <DialogContent
          className="rounded-lg border-0 p-6 shadow-lg"
          style={{
            backgroundColor: "rgb(49, 51, 56)",
            color: "rgb(255, 255, 255)",
          }}
        >
          <DialogHeader className="space-y-2 text-left">
            <DialogTitle className="text-lg font-semibold text-white">
              Kick Member
            </DialogTitle>
            <DialogDescription asChild>
              <div
                className="space-y-1 text-sm"
                style={{ color: "rgb(163, 163, 163)" }}
              >
                <p>
                  Are you sure you want to kick{" "}
                  <span className="font-medium text-white">
                    {kickPending?.name ?? ""}
                  </span>{" "}
                  from this channel?
                </p>
                <p>This cannot be undone.</p>
              </div>
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="flex flex-row justify-end gap-2 sm:justify-end">
            <button
              type="button"
              onClick={() => setKickPending(null)}
              className="rounded-md px-4 py-2 text-sm font-medium transition-colors hover:opacity-90"
              style={{
                backgroundColor: "rgb(64, 66, 72)",
                color: "rgb(255, 255, 255)",
              }}
            >
              Cancel
            </button>
            <button
              type="button"
              disabled={isKicking}
              onClick={async () => {
                if (!kickPending || !onKickMember) return
                setIsKicking(true)
                try {
                  await onKickMember(kickPending.id)
                  setKickPending(null)
                } catch {
                  toast({
                    title: "Failed to kick member",
                    variant: "destructive",
                  })
                } finally {
                  setIsKicking(false)
                }
              }}
              className="flex items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-medium text-white transition-colors hover:opacity-90 disabled:pointer-events-none disabled:opacity-70"
              style={{ backgroundColor: "rgb(220, 53, 69)" }}
            >
              {isKicking ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                "Kick"
              )}
            </button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
