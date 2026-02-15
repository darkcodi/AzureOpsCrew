"use client"

import { useState, type KeyboardEvent } from "react"
import { cn } from "@/lib/utils"
import type { Channel } from "@/lib/agents"
import { Hash, Plus } from "lucide-react"
import { ScrollArea } from "@/components/ui/scroll-area"
import { useToast } from "@/hooks/use-toast"
import {
  ContextMenu,
  ContextMenuContent,
  ContextMenuItem,
  ContextMenuSeparator,
  ContextMenuSub,
  ContextMenuSubContent,
  ContextMenuSubTrigger,
  ContextMenuTrigger,
} from "@/components/ui/context-menu"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"

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
  const [deleteChannelPending, setDeleteChannelPending] = useState<Channel | null>(null)
  const { toast } = useToast()

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
    <>
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
            <ContextMenu key={channel.id}>
              <ContextMenuContent
                className="min-w-[220px] rounded-lg border-0 p-1 shadow-lg"
                style={{
                  backgroundColor: "rgb(53, 54, 59)",
                  color: "rgb(255, 255, 255)",
                }}
                onCloseAutoFocus={(e) => e.preventDefault()}
              >
                <ContextMenuItem
                  className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white"
                  onSelect={(e) => e.preventDefault()}
                >
                  Mark As Read
                </ContextMenuItem>
                <ContextMenuSeparator className="my-1 bg-white/15" />
                <ContextMenuItem
                  className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white"
                  onSelect={(e) => e.preventDefault()}
                >
                  Invite to Channel
                </ContextMenuItem>
                <ContextMenuItem
                  className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white"
                  onSelect={(e) => e.preventDefault()}
                >
                  Copy Link
                </ContextMenuItem>
                <ContextMenuSeparator className="my-1 bg-white/15" />
                <ContextMenuSub>
                  <ContextMenuSubTrigger
                    className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white data-[state=open]:bg-white/10"
                    onSelect={(e) => e.preventDefault()}
                  >
                    Mute Channel
                  </ContextMenuSubTrigger>
                  <ContextMenuSubContent
                    className="rounded-lg border-0 p-1 shadow-lg"
                    style={{
                      backgroundColor: "rgb(53, 54, 59)",
                      color: "rgb(255, 255, 255)",
                    }}
                  >
                    <ContextMenuItem
                      className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10"
                      onSelect={(e) => e.preventDefault()}
                    >
                      (placeholder)
                    </ContextMenuItem>
                  </ContextMenuSubContent>
                </ContextMenuSub>
                <ContextMenuSub>
                  <ContextMenuSubTrigger
                    className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white data-[state=open]:bg-white/10"
                    onSelect={(e) => e.preventDefault()}
                  >
                    <span className="flex flex-col items-start">
                      <span>Notification Settings</span>
                      <span
                        className="text-xs"
                        style={{ color: "rgb(163, 163, 163)" }}
                      >
                        All Messages
                      </span>
                    </span>
                  </ContextMenuSubTrigger>
                  <ContextMenuSubContent
                    className="rounded-lg border-0 p-1 shadow-lg"
                    style={{
                      backgroundColor: "rgb(53, 54, 59)",
                      color: "rgb(255, 255, 255)",
                    }}
                  >
                    <ContextMenuItem
                      className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10"
                      onSelect={(e) => e.preventDefault()}
                    >
                      (placeholder)
                    </ContextMenuItem>
                  </ContextMenuSubContent>
                </ContextMenuSub>
                <ContextMenuItem
                  className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white"
                  onSelect={(e) => e.preventDefault()}
                >
                  Edit Channel
                </ContextMenuItem>
                <ContextMenuItem
                  className="cursor-pointer rounded px-2 py-1.5 text-sm text-red-500 focus:bg-white/10 focus:text-red-500"
                  onSelect={() => setDeleteChannelPending(channel)}
                >
                  Delete Channel
                </ContextMenuItem>
                <ContextMenuItem
                  className="cursor-pointer rounded px-2 py-1.5 text-sm focus:bg-white/10 focus:text-white"
                  onSelect={() => {
                    navigator.clipboard.writeText(channel.id).then(
                      () => toast({ title: "Channel ID copied to clipboard" }),
                      () => toast({ title: "Failed to copy", variant: "destructive" })
                    )
                  }}
                >
                  <span className="flex flex-1">Copy Channel ID</span>
                  <span
                    className="ml-2 rounded px-1.5 py-0.5 text-xs font-medium"
                    style={{
                      backgroundColor: "rgb(70, 71, 76)",
                      color: "rgb(163, 163, 163)",
                    }}
                  >
                    ID
                  </span>
                </ContextMenuItem>
              </ContextMenuContent>
              <ContextMenuTrigger asChild>
              <button
                type="button"
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
              </ContextMenuTrigger>
            </ContextMenu>
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

    <Dialog open={!!deleteChannelPending} onOpenChange={(open) => !open && setDeleteChannelPending(null)}>
      <DialogContent
        className="rounded-lg border-0 p-6 shadow-lg"
        style={{
          backgroundColor: "rgb(49, 51, 56)",
          color: "rgb(255, 255, 255)",
        }}
      >
        <DialogHeader className="space-y-2 text-left">
          <DialogTitle className="text-lg font-semibold text-white">
            Delete Channel
          </DialogTitle>
          <DialogDescription asChild>
            <div className="space-y-1 text-sm" style={{ color: "rgb(163, 163, 163)" }}>
              <p>
                Are you sure you want to delete{" "}
                <span className="font-medium text-white">
                  #{deleteChannelPending?.name ?? ""}
                </span>
                ?
              </p>
              <p>This cannot be undone.</p>
            </div>
          </DialogDescription>
        </DialogHeader>
        <DialogFooter className="flex flex-row justify-end gap-2 sm:justify-end">
          <button
            type="button"
            onClick={() => setDeleteChannelPending(null)}
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
            onClick={() => setDeleteChannelPending(null)}
            className="rounded-md px-4 py-2 text-sm font-medium text-white transition-colors hover:opacity-90"
            style={{ backgroundColor: "rgb(220, 53, 69)" }}
          >
            Delete Channel
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
    </>
  )
}
