"use client"

import { Users, Settings } from "lucide-react"
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip"

interface IconSidebarProps {
  onOpenAgentManager: () => void
}

export function IconSidebar({ onOpenAgentManager }: IconSidebarProps) {
  const topItems = [
    // "All agents" opens the agent manager popup
    { icon: Users, label: "All agents", onClick: onOpenAgentManager },
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
                className="flex h-10 w-10 items-center justify-center rounded-2xl transition-all hover:rounded-xl"
                style={{
                  backgroundColor: "hsl(228, 6%, 22%)",
                  color: "hsl(210, 3%, 80%)",
                }}
                aria-label={item.label}
              >
                <item.icon className="h-5 w-5" />
              </button>
            )

            // Tooltip only for "All agents" button
            return (
              <Tooltip key={item.label}>
                <TooltipTrigger asChild>{button}</TooltipTrigger>
                <TooltipContent side="right">All agents</TooltipContent>
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
