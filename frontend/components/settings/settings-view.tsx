"use client"

import { useState, useCallback, useMemo, useEffect } from "react"
import { SettingsSidebar, type SettingsSection } from "./settings-sidebar"
import { SettingsContent } from "./settings-content"
import { SettingsInfoPanel } from "./settings-info-panel"
import { type SettingsState, defaultSettings } from "./settings-types"

const SETTINGS_STORAGE_KEY = "azureopscrew-settings"

function loadPersistedSettings(): SettingsState {
  if (typeof window === "undefined") return defaultSettings
  try {
    const raw = localStorage.getItem(SETTINGS_STORAGE_KEY)
    if (!raw) return defaultSettings
    const loaded = JSON.parse(raw) as Partial<SettingsState>
    return {
      ...defaultSettings,
      ...loaded,
      account: { ...defaultSettings.account, ...loaded.account },
      appearance: { ...defaultSettings.appearance, ...loaded.appearance },
      notifications: { ...defaultSettings.notifications, ...loaded.notifications },
      routing: { ...defaultSettings.routing, ...loaded.routing },
      advanced: { ...defaultSettings.advanced, ...loaded.advanced },
      providers: Array.isArray(loaded.providers) ? loaded.providers : defaultSettings.providers,
    }
  } catch {
    return defaultSettings
  }
}

function persistSettings(state: SettingsState) {
  try {
    localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify(state))
  } catch {
    // ignore quota or parse errors
  }
}

/** Current user display name from persisted settings (for use outside Settings). */
export function getDisplayNameFromStorage(): string {
  return loadPersistedSettings().account.displayName || "User"
}

interface SettingsViewProps {
  onNavigateToAllAgents?: () => void
}

export function SettingsView({ onNavigateToAllAgents }: SettingsViewProps) {
  const [activeSection, setActiveSection] =
    useState<SettingsSection>("providers")
  const [settings, setSettings] = useState<SettingsState>(defaultSettings)
  const [savedSettings, setSavedSettings] =
    useState<SettingsState>(defaultSettings)
  const [selectedProviderId, setSelectedProviderId] = useState<string | null>(
    "openai"
  )

  useEffect(() => {
    const persisted = loadPersistedSettings()
    setSettings(persisted)
    setSavedSettings(persisted)
  }, [])

  const hasUnsavedChanges = useMemo(
    () => JSON.stringify(settings) !== JSON.stringify(savedSettings),
    [settings, savedSettings]
  )

  const handleSave = useCallback(() => {
    setSavedSettings(settings)
    persistSettings(settings)
  }, [settings])

  const handleReset = useCallback(() => {
    setSettings(savedSettings)
  }, [savedSettings])

  const selectedProvider = useMemo(
    () => settings.providers.find((p) => p.id === selectedProviderId) ?? null,
    [settings.providers, selectedProviderId]
  )

  return (
    <div className="flex min-h-0 min-w-0 flex-1 flex-col">
      <div className="flex min-h-0 flex-1">
        {/* Left pane: navigation tree */}
        <SettingsSidebar
          activeSection={activeSection}
          onSectionChange={setActiveSection}
        />

        {/* Center pane: settings content */}
        <SettingsContent
          activeSection={activeSection}
          settings={settings}
          onSettingsChange={setSettings}
          selectedProviderId={selectedProviderId}
          onSelectProvider={setSelectedProviderId}
          onNavigateToAllAgents={onNavigateToAllAgents}
        />

        {/* Right pane: contextual info */}
        <SettingsInfoPanel
          activeSection={activeSection}
          selectedProvider={selectedProvider}
        />
      </div>

      {/* Unsaved changes bar */}
      {hasUnsavedChanges && (
        <div
          className="flex shrink-0 items-center justify-between border-t px-6 py-3"
          style={{
            backgroundColor: "hsl(228, 7%, 14%)",
            borderColor: "hsl(228, 6%, 20%)",
          }}
        >
          <span
            className="text-sm font-medium"
            style={{ color: "hsl(210, 3%, 80%)" }}
          >
            Unsaved changes
          </span>
          <div className="flex gap-3">
            <button
              type="button"
              onClick={handleReset}
              className="rounded-md px-5 py-2 text-sm font-medium transition-opacity hover:opacity-90"
              style={{
                backgroundColor: "hsl(228, 6%, 30%)",
                color: "hsl(210, 3%, 90%)",
              }}
            >
              Reset
            </button>
            <button
              type="button"
              onClick={handleSave}
              className="rounded-md px-5 py-2 text-sm font-medium transition-opacity hover:opacity-90"
              style={{
                backgroundColor: "hsl(235, 86%, 65%)",
                color: "#fff",
              }}
            >
              Save
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
