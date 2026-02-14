"use client"

import { useState } from "react"
import { UserPlus, MoreHorizontal, Moon, Send } from "lucide-react"
import type { Agent } from "@/lib/agents"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"

interface AgentProfilePopoverProps {
  agent: Agent
  children: React.ReactNode
  onOpenInDM?: (agentId: string, message?: string) => void
}

export function AgentProfilePopover({
  agent,
  children,
  onOpenInDM,
}: AgentProfilePopoverProps) {
  const [message, setMessage] = useState("")
  const [open, setOpen] = useState(false)

  const bio =
    agent.systemPrompt.length > 120
      ? agent.systemPrompt.slice(0, 120).trim() + "..."
      : agent.systemPrompt

  const handleSend = () => {
    const text = message.trim()
    if (text) {
      onOpenInDM?.(agent.id, text)
      setMessage("")
      setOpen(false)
    }
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>{children}</PopoverTrigger>
      <PopoverContent
        align="start"
        side="left"
        sideOffset={16}
        className={cn(
          "z-[100] w-[340px] overflow-hidden rounded-xl border-0 p-0",
          "bg-[hsl(228,7%,14%)] text-[hsl(210,3%,90%)]",
          "shadow-xl"
        )}
      >
        {/* Banner + avatar */}
        <div className="relative">
          <div
            className="h-20 w-full rounded-t-xl"
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
          <div className="absolute right-4 top-3 flex flex-col items-end gap-2">
            <div className="flex items-center gap-2">
              <button
                type="button"
                className="flex h-8 w-8 items-center justify-center rounded-full transition-colors hover:bg-white/20"
                style={{ color: "#fff" }}
                aria-label="Add"
              >
                <UserPlus className="h-4 w-4" />
              </button>
              <button
                type="button"
                className="flex h-8 w-8 items-center justify-center rounded-full transition-colors hover:bg-white/20"
                style={{ color: "#fff" }}
                aria-label="More options"
              >
                <MoreHorizontal className="h-4 w-4" />
              </button>
            </div>
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

        {/* Profile info */}
        <div className="px-4 pt-14 pb-4">
          <h3 className="text-xl font-bold" style={{ color: "hsl(210, 3%, 98%)" }}>
            {agent.name}
          </h3>
          <p
            className="text-sm"
            style={{ color: "hsl(214, 5%, 55%)" }}
          >
            {agent.id}
          </p>
          <p
            className="mt-1 flex items-center gap-1.5 text-xs"
            style={{ color: "hsl(214, 5%, 55%)" }}
          >
            <Moon className="h-3.5 w-3.5" />
            In this room
          </p>
          <p
            className="mt-3 line-clamp-3 text-sm leading-snug"
            style={{ color: "hsl(210, 3%, 80%)" }}
          >
            {bio}
          </p>

          {/* Badges */}
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

        {/* Message input */}
        <div
          className="flex items-center gap-2 border-t px-4 py-3"
          style={{
            borderColor: "hsl(228, 6%, 18%)",
            backgroundColor: "hsl(228, 6%, 12%)",
          }}
        >
          <Input
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            placeholder={`Message @${agent.name}`}
            className="flex-1 border-0 bg-[hsl(228,6%,22%)] text-sm placeholder:text-[hsl(214,5%,55%)] focus-visible:ring-1"
            onKeyDown={(e) => {
              if (e.key === "Enter") handleSend()
            }}
          />
          <button
            type="button"
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full transition-colors hover:bg-[hsl(228,6%,26%)] disabled:opacity-50"
            style={{ color: "hsl(214, 5%, 55%)" }}
            aria-label="Send message"
            onClick={handleSend}
            disabled={!message.trim()}
          >
            <Send className="h-4 w-4" />
          </button>
        </div>
      </PopoverContent>
    </Popover>
  )
}
