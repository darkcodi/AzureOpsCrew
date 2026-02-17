"use client"

import {
  User,
  Palette,
  Bell,
  Plug,
  Bot,
  GitFork,
  Wrench,
  ChevronRight,
} from "lucide-react"
import { cn } from "@/lib/utils"
import { ScrollArea } from "@/components/ui/scroll-area"

export type SettingsSection =
  | "account"
  | "appearance"
  | "notifications"
  | "providers"
  | "agents"
  | "routing"
  | "advanced"

interface SettingsGroup {
  label: string
  items: {
    id: SettingsSection
    label: string
    icon: React.ComponentType<{ className?: string }>
  }[]
}

const settingsTree: SettingsGroup[] = [
  {
    label: "USER SETTINGS",
    items: [
      { id: "account", label: "Account", icon: User },
      { id: "appearance", label: "Appearance", icon: Palette },
      { id: "notifications", label: "Notifications", icon: Bell },
    ],
  },
  {
    label: "AI",
    items: [
      { id: "providers", label: "Providers", icon: Plug },
      { id: "agents", label: "Agents", icon: Bot },
      { id: "routing", label: "Routing", icon: GitFork },
      { id: "advanced", label: "Advanced", icon: Wrench },
    ],
  },
]

interface SettingsSidebarProps {
  activeSection: SettingsSection
  onSectionChange: (section: SettingsSection) => void
}

export function SettingsSidebar({
  activeSection,
  onSectionChange,
}: SettingsSidebarProps) {
  return (
    <div
      className="flex h-full w-[220px] shrink-0 flex-col"
      style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
    >
      {/* Header */}
      <div
        className="flex h-12 shrink-0 items-center px-4 font-semibold shadow-md"
        style={{
          color: "hsl(210, 3%, 95%)",
          borderBottom: "1px solid hsl(228, 6%, 20%)",
        }}
      >
        Settings
      </div>

      {/* Navigation tree */}
      <ScrollArea className="flex-1">
        <nav className="flex flex-col gap-1 p-2">
          {settingsTree.map((group) => (
            <div key={group.label} className="mb-1">
              <div
                className="px-2 pb-1 pt-3 text-[11px] font-bold uppercase tracking-wider"
                style={{ color: "hsl(214, 5%, 55%)" }}
              >
                {group.label}
              </div>
              {group.items.map((item) => {
                const isActive = activeSection === item.id
                return (
                  <button
                    key={item.id}
                    type="button"
                    onClick={() => onSectionChange(item.id)}
                    className={cn(
                      "flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors",
                      "hover:bg-[hsl(228,6%,22%)]"
                    )}
                    style={{
                      backgroundColor: isActive
                        ? "hsl(228, 6%, 22%)"
                        : "transparent",
                      color: isActive
                        ? "hsl(210, 3%, 95%)"
                        : "hsl(214, 5%, 65%)",
                    }}
                  >
                    <item.icon className="h-4 w-4 shrink-0" />
                    <span className="flex-1 text-left">{item.label}</span>
                    {isActive && (
                      <ChevronRight
                        className="h-3 w-3 shrink-0"
                        style={{ color: "hsl(214, 5%, 55%)" }}
                      />
                    )}
                  </button>
                )
              })}
            </div>
          ))}
        </nav>
      </ScrollArea>

      {/* Version footer */}
      <div
        className="shrink-0 px-4 py-2 text-[11px]"
        style={{ color: "hsl(214, 5%, 45%)" }}
      >
        v0.1
      </div>
    </div>
  )
}
