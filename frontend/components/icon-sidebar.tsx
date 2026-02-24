"use client"

import { Settings, MessageCircle, Hash, LogOut } from "lucide-react"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"
import { cn } from "@/lib/utils"

export type ViewMode = "channels" | "direct-messages" | "settings"

interface IconSidebarProps {
  viewMode: ViewMode
  onViewChange: (view: ViewMode) => void
  onLogout: () => void | Promise<void>
}

export function IconSidebar({
  viewMode,
  onViewChange,
  onLogout,
}: IconSidebarProps) {
  const topItems = [
    { icon: Hash, label: "Direct messages", onClick: () => onViewChange("direct-messages"), active: viewMode === "direct-messages" },
    { icon: MessageCircle, label: "Channels", onClick: () => onViewChange("channels"), active: viewMode === "channels" },
  ]

  const isSettingsActive = viewMode === "settings"

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
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                type="button"
                onClick={() => onViewChange("settings")}
                className={cn(
                  "flex h-10 w-10 items-center justify-center rounded-2xl transition-all hover:rounded-xl",
                  isSettingsActive && "rounded-xl"
                )}
                style={{
                  backgroundColor: isSettingsActive ? "hsl(235, 86%, 65%)" : "hsl(228, 6%, 22%)",
                  color: isSettingsActive ? "#fff" : "hsl(210, 3%, 80%)",
                }}
                aria-label="Settings"
              >
                <Settings className="h-5 w-5" />
              </button>
            </TooltipTrigger>
            <TooltipContent side="right">Settings</TooltipContent>
          </Tooltip>
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                type="button"
                onClick={() => void onLogout()}
                className="flex h-10 w-10 items-center justify-center rounded-2xl transition-all hover:rounded-xl"
                style={{
                  backgroundColor: "hsl(228, 6%, 22%)",
                  color: "hsl(210, 3%, 80%)",
                }}
                aria-label="Logout"
              >
                <LogOut className="h-5 w-5" />
              </button>
            </TooltipTrigger>
            <TooltipContent side="right">Logout</TooltipContent>
          </Tooltip>
        </div>
      </div>
    </TooltipProvider>
  )
}
