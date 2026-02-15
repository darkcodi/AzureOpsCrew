"use client"

import { useState, type KeyboardEvent } from "react"
import { cn } from "@/lib/utils"
import type { Channel } from "@/lib/agents"
import { Hash, Plus } from "lucide-react"
import { ScrollArea } from "@/components/ui/scroll-area"

interface ChannelSidebarProps {
  channels: Channel[]
  activeChannelId: string
  onChannelSelect: (channelId: string) => void
  onCreateChannel: (name: string) => void | Promise<void>
}

export function ChannelSidebar({
  channels,
  activeChannelId,
  onChannelSelect,
  onCreateChannel,
}: ChannelSidebarProps) {
  const [newChannelName, setNewChannelName] = useState("")

  const handleCreate = () => {
    const trimmed = newChannelName.trim()
    if (!trimmed) return
    onCreateChannel(trimmed)
    setNewChannelName("")
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      handleCreate()
    }
  }

  return (
    <div
      className="flex h-full w-[220px] flex-col"
      style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
    >
      {/* Header */}
      <div
        className="flex h-12 items-center px-4"
        style={{
          borderBottom: "1px solid hsl(228, 6%, 10%)",
        }}
      >
        <span
          className="text-xs font-bold uppercase tracking-wider"
          style={{ color: "hsl(214, 5%, 55%)" }}
        >
          Channels
        </span>
      </div>

      {/* Channel list */}
      <ScrollArea className="flex-1 px-2 pt-3">
        {[...channels]
          .sort((a, b) => {
            const dateA = a.dateCreated ? new Date(a.dateCreated).getTime() : 0
            const dateB = b.dateCreated ? new Date(b.dateCreated).getTime() : 0
            return dateA - dateB
          })
          .map((channel) => {
          const isActive = activeChannelId === channel.id
          return (
            <button
              type="button"
              key={channel.id}
              onClick={() => onChannelSelect(channel.id)}
              className={cn(
                "flex w-full items-center gap-1.5 rounded-md px-2 py-1.5 text-sm transition-colors mb-0.5"
              )}
              style={{
                backgroundColor: isActive
                  ? "hsl(228, 6%, 22%)"
                  : "transparent",
                color: isActive
                  ? "hsl(0, 0%, 100%)"
                  : "hsl(214, 5%, 55%)",
                fontWeight: isActive ? 500 : 400,
              }}
              onMouseEnter={(e) => {
                if (!isActive) {
                  e.currentTarget.style.backgroundColor =
                    "hsl(228, 6%, 18%)"
                  e.currentTarget.style.color = "hsl(210, 3%, 80%)"
                }
              }}
              onMouseLeave={(e) => {
                if (!isActive) {
                  e.currentTarget.style.backgroundColor = "transparent"
                  e.currentTarget.style.color = "hsl(214, 5%, 55%)"
                }
              }}
            >
              <Hash className="h-4 w-4 shrink-0 opacity-60" />
              <span className="truncate">{channel.name}</span>
            </button>
          )
        })
        }

        {/* Create channel */}
        <div className="mt-4 px-1">
          <input
            type="text"
            value={newChannelName}
            onChange={(e) => setNewChannelName(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Enter channel name..."
            className="mb-2 w-full rounded-md px-2 py-1.5 text-sm outline-none placeholder:text-sm"
            style={{
              backgroundColor: "hsl(228, 6%, 18%)",
              color: "hsl(210, 3%, 90%)",
              border: "1px solid hsl(228, 6%, 26%)",
            }}
          />
          <button
            type="button"
            onClick={handleCreate}
            className="flex w-full items-center justify-center gap-1.5 rounded-md py-1.5 text-sm transition-colors hover:opacity-80"
            style={{
              color: "hsl(214, 5%, 55%)",
            }}
          >
            <Plus className="h-4 w-4" />
            <span>Create Channel</span>
          </button>
        </div>
      </ScrollArea>
    </div>
  )
}
