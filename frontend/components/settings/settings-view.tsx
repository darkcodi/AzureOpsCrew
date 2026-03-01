"use client"

import { useState, useCallback, useMemo, useEffect } from "react"
import type { Agent } from "@/lib/agents"
import { SettingsSidebar, type SettingsSection } from "./settings-sidebar"
import { SettingsContent } from "./settings-content"
import { SettingsInfoPanel } from "./settings-info-panel"
import { type SettingsState, type ProviderTestResult, defaultSettings } from "./settings-types"
import { useToast } from "@/hooks/use-toast"
import { fetchWithErrorHandling } from "@/lib/fetch"

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
  allAgents: Agent[]
  onAddAgent: (agent: Agent) => void | Promise<void>
  onUpdateAgent: (agent: Agent) => void
  onDeleteAgent: (agentId: string) => void | Promise<void>
}

export function SettingsView({
  allAgents,
  onAddAgent,
  onUpdateAgent,
  onDeleteAgent,
}: SettingsViewProps) {
  const [activeSection, setActiveSection] =
    useState<SettingsSection>("providers")
  const [settings, setSettings] = useState<SettingsState>(() => ({
    ...defaultSettings,
    providers: [],
  }))
  const [savedSettings, setSavedSettings] = useState<SettingsState>(() => ({
    ...defaultSettings,
    providers: [],
  }))
  const [selectedProviderId, setSelectedProviderId] = useState<string | null>(null)

  useEffect(() => {
    const persisted = loadPersistedSettings()

    fetchWithErrorHandling("/api/providers")
      .then((res) => {
        if (!res.ok) return null
        return res.json()
      })
      .then((apiProviders: SettingsState["providers"] | null) => {
        if (apiProviders == null) {
          setSettings(persisted)
          setSavedSettings(persisted)
          setSelectedProviderId(persisted.providers[0]?.id ?? null)
          return
        }
        const list = Array.isArray(apiProviders) ? apiProviders : []
        const sorted = [...list].sort((a, b) => {
          const tA = a.dateCreated ? new Date(a.dateCreated).getTime() : Infinity
          const tB = b.dateCreated ? new Date(b.dateCreated).getTime() : Infinity
          return tA - tB
        })
        const nextSettings: SettingsState = { ...persisted, providers: sorted }
        setSettings(nextSettings)
        setSavedSettings(nextSettings)
        persistSettings(nextSettings)
        setSelectedProviderId(sorted[0]?.id ?? null)
      })
      .catch(() => {
        setSettings(persisted)
        setSavedSettings(persisted)
        setSelectedProviderId(persisted.providers[0]?.id ?? null)
      })
  }, [])

  const hasUnsavedChanges = useMemo(
    () => JSON.stringify(settings) !== JSON.stringify(savedSettings),
    [settings, savedSettings]
  )

  const modifiedProviderIds = useMemo(() => {
    const savedById = new Map(savedSettings.providers.map((p) => [p.id, p]))
    return new Set(
      settings.providers
        .filter((p) => {
          const saved = savedById.get(p.id)
          if (!saved || !p.backendId) return false
          return (
            p.name !== saved.name ||
            p.status !== saved.status ||
            p.apiKey !== saved.apiKey ||
            p.baseUrl !== saved.baseUrl ||
            p.defaultModel !== saved.defaultModel ||
            p.isDefault !== saved.isDefault ||
            JSON.stringify(p.selectedModels ?? []) !== JSON.stringify(saved.selectedModels ?? [])
          )
        })
        .map((p) => p.id)
    )
  }, [settings.providers, savedSettings.providers])

  const [saveError, setSaveError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)
  const [isTesting, setIsTesting] = useState(false)
  const [providerTestResults, setProviderTestResults] = useState<Record<string, ProviderTestResult>>({})
  const { toast } = useToast()

  const mergeSaveResults = useCallback(
    (
      state: SettingsState,
      data: { providers?: Array<{ id: string; backendId: string }> }
    ): SettingsState => {
      if (!data.providers?.length) return state
      const byId = new Map(data.providers.map((r) => [r.id, r.backendId]))
      return {
        ...state,
        providers: state.providers.map((p) => {
          if (!byId.has(p.id)) return p
          const backendId = byId.get(p.id)!
          const status = p.status === "disabled" ? "disabled" : "enabled"
          return { ...p, backendId, status }
        }),
      }
    },
    []
  )

  /** Save all providers (used by footer Save). */
  const handleSave = useCallback(async () => {
    setSaveError(null)
    setIsSaving(true)
    try {
      const res = await fetchWithErrorHandling("/api/settings", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ providers: settings.providers }),
      })
      if (!res.ok) {
        const data = await res.json().catch(() => ({}))
        throw new Error(data.error ?? "Failed to save settings")
      }
      const data = (await res.json()) as { providers?: Array<{ id: string; backendId: string }> }
      const nextState = mergeSaveResults(settings, data)
      setSettings(nextState)
      setSavedSettings(nextState)
      persistSettings(nextState)
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Failed to save")
    } finally {
      setIsSaving(false)
    }
  }, [settings, mergeSaveResults])

  /** Save only the currently selected provider (used by provider-detail Save). */
  const handleSaveCurrentProvider = useCallback(async () => {
    if (!selectedProviderId) return
    const current = settings.providers.find((p) => p.id === selectedProviderId)
    if (!current) return
    setSaveError(null)
    setIsSaving(true)
    try {
      const res = await fetchWithErrorHandling("/api/settings", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ providers: [current] }),
      })
      if (!res.ok) {
        const data = await res.json().catch(() => ({}))
        throw new Error(data.error ?? "Failed to save provider")
      }
      const data = (await res.json()) as { providers?: Array<{ id: string; backendId: string }> }
      const nextState = mergeSaveResults(settings, data)
      setSettings(nextState)
      // Only update the saved snapshot for the provider we just saved; leave other providers' saved state unchanged so they stay "Modified" if they have local changes
      const savedProvider = nextState.providers.find((p) => p.id === selectedProviderId)
      if (savedProvider) {
        setSavedSettings((prev) => {
          const idx = prev.providers.findIndex((p) => p.id === selectedProviderId)
          if (idx >= 0) {
            return {
              ...prev,
              providers: prev.providers.map((p) =>
                p.id === selectedProviderId ? savedProvider : p
              ),
            }
          }
          return { ...prev, providers: [...prev.providers, savedProvider] }
        })
      }
      persistSettings(nextState)
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : "Failed to save provider")
    } finally {
      setIsSaving(false)
    }
  }, [settings, selectedProviderId, mergeSaveResults])

  /** Test connection for the currently selected provider (works for both saved and draft). Always uses /api/providers/test with body. */
  const handleTestProvider = useCallback(async () => {
    if (!selectedProviderId) return
    const current = settings.providers.find((p) => p.id === selectedProviderId)
    if (!current) return
    setIsTesting(true)
    setSaveError(null)
    try {
      const res = await fetchWithErrorHandling("/api/providers/test", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          providerId: current.backendId ?? null,
          providerType: current.providerType ?? current.name,
          name: current.name,
          apiKey: current.apiKey,
          baseUrl: current.baseUrl,
          defaultModel: current.defaultModel,
        }),
      })
      const data = await res.json().catch(() => ({})) as ProviderTestResult & { success?: boolean; message?: string }
      if (!res.ok) {
        toast({
          title: "Test failed",
          description: data.message ?? "Could not test connection",
          variant: "destructive",
        })
        return
      }
      setProviderTestResults((prev) => ({
        ...prev,
        [selectedProviderId]: {
          success: data.success ?? false,
          message: data.message,
          latencyMs: data.latencyMs,
          checkedAt: data.checkedAt,
          quota: data.quota,
          availableModels: data.availableModels,
        },
      }))
      if (data.success) {
        toast({ title: "Connection successful", description: data.message ?? undefined })
      } else {
        toast({
          title: "Connection failed",
          description: data.message ?? undefined,
          variant: "destructive",
        })
      }
    } catch {
      toast({
        title: "Test failed",
        description: "Network or server error",
        variant: "destructive",
      })
    } finally {
      setIsTesting(false)
    }
  }, [settings.providers, selectedProviderId, toast])

  /** Remove the given provider. Draft (no backendId): FE-only. Saved: call BE delete then update FE. */
  const handleRemoveProvider = useCallback(
    async (providerId: string) => {
      const provider = settings.providers.find((p) => p.id === providerId)
      if (!provider) return
      setSaveError(null)
      const isDraft = !provider.backendId
      if (isDraft) {
        // Draft: never call BE; remove from state and persist only.
        const deletedIndex = settings.providers.findIndex((p) => p.id === providerId)
        const remaining = settings.providers.filter((p) => p.id !== providerId)
        const nextSelected =
          deletedIndex > 0 ? remaining[deletedIndex - 1] : remaining[0]
        const nextSettings: SettingsState = { ...settings, providers: remaining }
        const nextSaved: SettingsState = {
          ...savedSettings,
          providers: savedSettings.providers.filter((p) => p.id !== providerId),
        }
        setSettings(nextSettings)
        setSavedSettings(nextSaved)
        setSelectedProviderId(nextSelected?.id ?? null)
        persistSettings(nextSettings)
        return
      }
      setIsSaving(true)
      try {
        const res = await fetchWithErrorHandling(`/api/providers/${provider.backendId}`, {
          method: "DELETE",
        })
        if (!res.ok) {
          const data = await res.json().catch(() => ({}))
          throw new Error(data.error ?? "Failed to remove provider")
        }
        const deletedIndex = settings.providers.findIndex((p) => p.id === providerId)
        const remaining = settings.providers.filter((p) => p.id !== providerId)
        const nextSelected =
          deletedIndex > 0 ? remaining[deletedIndex - 1] : remaining[0]
        const nextSettings: SettingsState = { ...settings, providers: remaining }
        const nextSaved: SettingsState = {
          ...savedSettings,
          providers: savedSettings.providers.filter((p) => p.id !== providerId),
        }
        setSettings(nextSettings)
        setSavedSettings(nextSaved)
        setSelectedProviderId(nextSelected?.id ?? null)
        persistSettings(nextSettings)
      } catch (err) {
        setSaveError(err instanceof Error ? err.message : "Failed to remove provider")
        throw err
      } finally {
        setIsSaving(false)
      }
    },
    [settings, savedSettings]
  )

  /** Toggle a model in the selected provider's selectedModels list. */
  const handleToggleSelectedModel = useCallback(
    (modelId: string) => {
      if (!selectedProviderId) return
      setSettings((prev) => ({
        ...prev,
        providers: prev.providers.map((p) => {
          if (p.id !== selectedProviderId) return p
          const current = p.selectedModels ?? []
          const next = current.includes(modelId)
            ? current.filter((id) => id !== modelId)
            : [...current, modelId]
          return { ...p, selectedModels: next }
        }),
      }))
    },
    [selectedProviderId]
  )

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
          savedSettings={savedSettings}
          modifiedProviderIds={modifiedProviderIds}
          hasUnsavedChanges={hasUnsavedChanges}
          onSettingsChange={setSettings}
          selectedProviderId={selectedProviderId}
          onSelectProvider={setSelectedProviderId}
          allAgents={allAgents}
          onAddAgent={onAddAgent}
          onUpdateAgent={onUpdateAgent}
          onDeleteAgent={onDeleteAgent}
          onSave={handleSave}
          onSaveCurrentProvider={handleSaveCurrentProvider}
          onTestProvider={handleTestProvider}
          onRemoveProvider={handleRemoveProvider}
          onToggleSelectedModel={handleToggleSelectedModel}
          isSaving={isSaving}
          isTesting={isTesting}
          saveError={saveError}
        />

        {/* Right pane: contextual info */}
        <SettingsInfoPanel
          activeSection={activeSection}
          selectedProvider={selectedProvider}
          selectedModels={selectedProvider?.selectedModels ?? []}
          onToggleSelectedModel={handleToggleSelectedModel}
          providerTestResult={
            selectedProviderId ? providerTestResults[selectedProviderId] ?? null : null
          }
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
          <div className="flex items-center gap-3">
            {saveError && (
              <span className="text-sm" style={{ color: "hsl(0, 70%, 55%)" }}>
                {saveError}
              </span>
            )}
            <button
              type="button"
              onClick={handleReset}
              disabled={isSaving}
              className="rounded-md px-5 py-2 text-sm font-medium transition-opacity hover:opacity-90 disabled:opacity-50"
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
              disabled={isSaving}
              className="rounded-md px-5 py-2 text-sm font-medium transition-opacity hover:opacity-90 disabled:opacity-50"
              style={{
                backgroundColor: "hsl(235, 86%, 65%)",
                color: "#fff",
              }}
            >
              {isSaving ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
