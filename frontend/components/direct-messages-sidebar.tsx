"use client"

import { useState, useMemo } from "react"
import { Search, Plus, User } from "lucide-react"
import { ScrollArea } from "@/components/ui/scroll-area"
import { cn } from "@/lib/utils"
import type { Agent } from "@/lib/agents"
import { HUMANS, HUMAN_ID } from "@/components/direct-messages-right-pane"

interface DirectMessagesSidebarProps {
  agents: Agent[]
  /** Currently selected conversation (agent id) for chat. */
  activeId: string | null
  /** Currently selected item for the right pane (agent id or HUMAN_ID). */
  selectedCardId: string | null
  onSelect: (id: string) => void
}

export function DirectMessagesSidebar({
  agents,
  activeId,
  selectedCardId,
  onSelect,
}: DirectMessagesSidebarProps) {
  const [search, setSearch] = useState("")

  const conversations = useMemo(() => {
    return agents.map((a) => ({
      id: a.id,
      name: a.name,
      avatar: a.avatar,
      color: a.color,
    }))
  }, [agents])

  const filtered = useMemo(() => {
    if (!search.trim()) return conversations
    const q = search.toLowerCase()
    return conversations.filter((c) => c.name.toLowerCase().includes(q))
  }, [conversations, search])

  const filteredHumans = useMemo(() => {
    const others = HUMANS.filter((h) => h.id !== HUMAN_ID)
    if (!search.trim()) return others
    const q = search.toLowerCase()
    return others.filter((h) => h.name.toLowerCase().includes(q))
  }, [search])

  return (
    <div
      className="flex h-full w-[220px] flex-col"
      style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
    >
      {/* Search */}
      <div className="px-2 pt-3 pb-2">
        <div
          className="flex items-center gap-2 rounded-md px-2 py-1.5 text-sm"
          style={{
            backgroundColor: "hsl(228, 6%, 18%)",
            color: "hsl(214, 5%, 55%)",
          }}
        >
          <Search className="h-4 w-4 shrink-0" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Find or start a conversation"
            className="min-w-0 flex-1 bg-transparent outline-none placeholder:text-current"
          />
        </div>
      </div>

      {/* Direct Messages header */}
      <div
        className="flex h-9 items-center justify-between px-2"
        style={{
          borderBottom: "1px solid hsl(228, 6%, 10%)",
        }}
      >
        <button
          type="button"
          className="flex flex-1 items-center gap-1 text-sm font-semibold outline-none"
          style={{ color: "hsl(210, 3%, 90%)" }}
        >
          Direct Messages
        </button>
        <button
          type="button"
          className="flex h-6 w-6 items-center justify-center rounded text-current hover:bg-white/10"
          style={{ color: "hsl(214, 5%, 55%)" }}
          aria-label="New conversation"
        >
          <Plus className="h-4 w-4" />
        </button>
      </div>

      {/* Conversation list: AI agents */}
      <ScrollArea className="flex-1 py-2">
        <div className="px-2">
          <div
            className="mb-1 mt-1 px-2 py-1 text-xs font-semibold uppercase tracking-wider"
            style={{ color: "hsl(214, 5%, 55%)" }}
          >
            AI Agents
          </div>
          {filtered.map((conv) => {
            const isActive = activeId === conv.id
            return (
              <button
                key={conv.id}
                type="button"
                onClick={() => onSelect(conv.id)}
                className={cn(
                  "flex w-full items-center gap-3 rounded-md px-2 py-2 text-left text-sm transition-colors"
                )}
                style={{
                  backgroundColor: isActive ? "hsl(228, 6%, 22%)" : "transparent",
                  color: isActive
                    ? "hsl(0, 0%, 100%)"
                    : "hsl(210, 3%, 85%)",
                }}
                onMouseEnter={(e) => {
                  if (!isActive) {
                    e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
                  }
                }}
                onMouseLeave={(e) => {
                  if (!isActive) {
                    e.currentTarget.style.backgroundColor = "transparent"
                  }
                }}
              >
                <div
                  className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-medium"
                  style={{
                    backgroundColor: conv.color,
                    color: "#fff",
                  }}
                >
                  {conv.avatar}
                </div>
                <span className="truncate">{conv.name}</span>
                <span
                  className="ml-auto text-xs"
                  style={{ color: "hsl(214, 5%, 55%)" }}
                >
                  {agents.find((a) => a.id === conv.id)?.status ?? "Idle"}
                </span>
              </button>
            )
          })}
          <div
            className="mb-1 mt-6 px-2 py-1 text-xs font-semibold uppercase tracking-wider"
            style={{ color: "hsl(214, 5%, 55%)" }}
          >
            Humans
          </div>
          {filteredHumans.map((human) => {
            const isSelected = selectedCardId === human.id
            return (
              <button
                key={human.id}
                type="button"
                onClick={() => onSelect(human.id)}
                className={cn(
                  "flex w-full items-center gap-3 rounded-md px-2 py-2 text-left text-sm transition-colors"
                )}
                style={{
                  backgroundColor: isSelected ? "hsl(228, 6%, 22%)" : "transparent",
                  color: isSelected
                    ? "hsl(0, 0%, 100%)"
                    : "hsl(210, 3%, 85%)",
                }}
                onMouseEnter={(e) => {
                  if (!isSelected) {
                    e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
                  }
                }}
                onMouseLeave={(e) => {
                  if (!isSelected) {
                    e.currentTarget.style.backgroundColor = "transparent"
                  }
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
                <span className="truncate">{human.name}</span>
                <span
                  className="ml-auto inline-flex items-center gap-1.5 text-xs"
                  style={{ color: "hsl(214, 5%, 55%)" }}
                >
                  {human.status === "Online" && (
                    <span className="h-2 w-2 shrink-0 rounded-full bg-green-500" />
                  )}
                  {human.status}
                </span>
              </button>
            )
          })}
        </div>
      </ScrollArea>
    </div>
  )
}
