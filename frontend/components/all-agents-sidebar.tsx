"use client"

import { Users } from "lucide-react"

export function AllAgentsSidebar() {
  return (
    <div
      className="flex h-full w-[220px] flex-col shrink-0"
      style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
    >
      <div
        className="flex h-12 items-center gap-2 px-4"
        style={{
          borderBottom: "1px solid hsl(228, 6%, 10%)",
        }}
      >
        <Users className="h-4 w-4 shrink-0" style={{ color: "hsl(214, 5%, 55%)" }} />
        <span
          className="text-xs font-bold uppercase tracking-wider"
          style={{ color: "hsl(214, 5%, 55%)" }}
        >
          All agents
        </span>
      </div>
    </div>
  )
}
