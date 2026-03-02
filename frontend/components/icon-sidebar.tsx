"use client"

import { MessageCircle, LogOut } from "lucide-react"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"

export type ViewMode = "channels"

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
  return (
    <TooltipProvider>
      <div
        className="flex h-full w-[52px] flex-col items-center justify-between py-3"
        style={{ backgroundColor: "hsl(228, 7%, 10%)" }}
      >
        <div className="flex flex-col items-center gap-2">
          <Tooltip>
            <TooltipTrigger asChild>
              <button
                type="button"
                onClick={() => onViewChange("channels")}
                className="flex h-10 w-10 items-center justify-center rounded-xl transition-all"
                style={{
                  backgroundColor: "hsl(235, 86%, 65%)",
                  color: "#fff",
                }}
                aria-label="Channels"
              >
                <MessageCircle className="h-5 w-5" />
              </button>
            </TooltipTrigger>
            <TooltipContent side="right">Channels</TooltipContent>
          </Tooltip>
        </div>

        <div className="flex flex-col items-center gap-2">
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
