"use client"

import { Users, Settings, MessageCircle, Hash } from "lucide-react"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"

export type ViewMode = "channels" | "direct-messages" | "all-agents"

interface IconSidebarProps {
  viewMode: ViewMode
  onViewChange: (view: ViewMode) => void
}

export function IconSidebar({
  viewMode,
  onViewChange,
}: IconSidebarProps) {
  const topItems = [
    { icon: Hash, label: "Channels", onClick: () => onViewChange("channels"), active: viewMode === "channels" },
    { icon: MessageCircle, label: "Direct messages", onClick: () => onViewChange("direct-messages"), active: viewMode === "direct-messages" },
    { icon: Users, label: "All agents", onClick: () => onViewChange("all-agents"), active: viewMode === "all-agents" },
  ]

  const bottomItems = [{ icon: Settings, label: "Settings" }]

  return (
    <TooltipProvider>
      <div
        className="flex h-full w-[52px] flex-col items-center justify-between py-3"
        style={{ backgroundColor: "hsl(228, 7%, 10%)" }}
      >
        <div className="flex flex-col items-center gap-2">
          {topItems.map((item) => {
            const button = (
              <button
                key={item.label}
                type="button"
                onClick={item.onClick}
                className={cn(
                  "flex h-10 w-10 items-center justify-center rounded-2xl transition-all hover:rounded-xl",
                  item.active && "rounded-xl"
                )}
                style={{
                  backgroundColor: item.active ? "hsl(235, 86%, 65%)" : "hsl(228, 6%, 22%)",
                  color: item.active ? "#fff" : "hsl(210, 3%, 80%)",
                }}
                aria-label={item.label}
              >
                <item.icon className="h-5 w-5" />
              </button>
            )

            return (
              <Tooltip key={item.label}>
                <TooltipTrigger asChild>{button}</TooltipTrigger>
                <TooltipContent side="right">{item.label}</TooltipContent>
              </Tooltip>
            )
          })}
        </div>

        <div className="flex flex-col items-center gap-2">
          {bottomItems.map((item) => (
            <button
              key={item.label}
              type="button"
              className="flex h-10 w-10 items-center justify-center rounded-2xl transition-all hover:rounded-xl"
              style={{
                backgroundColor: "hsl(228, 6%, 22%)",
                color: "hsl(210, 3%, 80%)",
              }}
              aria-label={item.label}
            >
              <item.icon className="h-5 w-5" />
            </button>
          ))}
        </div>
      </div>
    </TooltipProvider>
  )
}
