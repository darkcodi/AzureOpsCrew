"use client"

import { useState } from "react"
import {
  Search,
  Circle,
  XCircle,
  Eye,
  EyeOff,
  Plus,
  Sparkles,
  Brain,
  Server,
  FileEdit,
  Pencil,
} from "lucide-react"
import { cn } from "@/lib/utils"
import { ScrollArea } from "@/components/ui/scroll-area"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog"
import { type SettingsSection } from "./settings-sidebar"
import {
  type ProviderConfig,
  type SettingsState,
  type AccountConfig,
  type AppearanceConfig,
  type NotificationConfig,
  type RoutingConfig,
  type AdvancedConfig,
  defaultSettings,
} from "./settings-types"

const KNOWN_PROVIDERS: {
  id: string
  name: string
  Icon: React.ComponentType<{ className?: string }>
  template: ProviderConfig
}[] = [
  {
    id: "openai",
    name: "OpenAI",
    Icon: Sparkles,
    template: defaultSettings.providers.find((p) => p.id === "openai")!,
  },
  {
    id: "anthropic",
    name: "Anthropic",
    Icon: Brain,
    template: defaultSettings.providers.find((p) => p.id === "anthropic")!,
  },
  {
    id: "ollama",
    name: "Ollama",
    Icon: Server,
    template: defaultSettings.providers.find((p) => p.id === "ollama")!,
  },
].filter((p) => p.template != null)

interface SettingsContentProps {
  activeSection: SettingsSection
  settings: SettingsState
  savedSettings: SettingsState
  modifiedProviderIds: Set<string>
  onSettingsChange: (settings: SettingsState) => void
  selectedProviderId: string | null
  onSelectProvider: (id: string) => void
  onNavigateToAllAgents?: () => void
  onSave?: () => void
  isSaving?: boolean
  saveError?: string | null
}

export function SettingsContent({
  activeSection,
  settings,
  savedSettings,
  modifiedProviderIds,
  onSettingsChange,
  selectedProviderId,
  onSelectProvider,
  onNavigateToAllAgents,
  onSave,
  isSaving,
  saveError,
}: SettingsContentProps) {
  return (
    <div
      className="flex min-w-0 flex-1 flex-col"
      style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
    >
      <ScrollArea className="flex-1">
        <div className="mx-auto max-w-3xl px-8 py-6">
          {activeSection === "providers" && (
            <ProvidersSection
              providers={settings.providers}
              modifiedProviderIds={modifiedProviderIds}
              selectedProviderId={selectedProviderId}
              onSelectProvider={onSelectProvider}
              onProvidersChange={(providers) =>
                onSettingsChange({ ...settings, providers })
              }
              onSave={onSave}
              isSaving={isSaving}
              saveError={saveError}
            />
          )}
          {activeSection === "agents" && (
            <AgentsSection onNavigateToAllAgents={onNavigateToAllAgents} />
          )}
          {activeSection === "routing" && (
            <RoutingSection
              routing={settings.routing}
              onRoutingChange={(routing) =>
                onSettingsChange({ ...settings, routing })
              }
            />
          )}
          {activeSection === "advanced" && (
            <AdvancedSection
              advanced={settings.advanced}
              onAdvancedChange={(advanced) =>
                onSettingsChange({ ...settings, advanced })
              }
            />
          )}
          {activeSection === "account" && (
            <AccountSection
              account={settings.account}
              onAccountChange={(account) =>
                onSettingsChange({ ...settings, account })
              }
            />
          )}
          {activeSection === "appearance" && (
            <AppearanceSection
              appearance={settings.appearance}
              onAppearanceChange={(appearance) =>
                onSettingsChange({ ...settings, appearance })
              }
            />
          )}
          {activeSection === "notifications" && (
            <NotificationsSection
              notifications={settings.notifications}
              onNotificationsChange={(notifications) =>
                onSettingsChange({ ...settings, notifications })
              }
            />
          )}

          {(activeSection === "routing" ||
            activeSection === "appearance" ||
            activeSection === "notifications") && (
            <div
              className="mt-10 rounded-lg border py-3 px-4 text-center text-sm"
              style={{
                borderColor: "hsl(228, 7%, 28%)",
                color: "hsl(214, 5%, 55%)",
                backgroundColor: "hsl(228, 7%, 18%)",
              }}
            >
              This settings page is still in development (WIP).
            </div>
          )}
        </div>
      </ScrollArea>
    </div>
  )
}

/* ─────────────────────────────────────────────────
 * Shared UI components
 * ───────────────────────────────────────────────── */

function SectionHeader({
  title,
  description,
  tabs,
  activeTab,
  onTabChange,
}: {
  title: string
  description: string
  tabs?: { id: string; label: string }[]
  activeTab?: string
  onTabChange?: (tab: string) => void
}) {
  return (
    <div className="mb-6">
      <div className="mb-1 flex items-center gap-4">
        <h2
          className="text-xl font-semibold"
          style={{ color: "hsl(210, 3%, 95%)" }}
        >
          {title}
        </h2>
        {tabs && (
          <div className="flex gap-1 rounded-lg p-0.5" style={{ backgroundColor: "hsl(228, 7%, 14%)" }}>
            {tabs.map((tab) => (
              <button
                key={tab.id}
                type="button"
                onClick={() => onTabChange?.(tab.id)}
                className={cn(
                  "rounded-md px-3 py-1 text-xs font-medium transition-colors"
                )}
                style={{
                  backgroundColor:
                    activeTab === tab.id
                      ? "hsl(235, 86%, 65%)"
                      : "transparent",
                  color:
                    activeTab === tab.id ? "#fff" : "hsl(214, 5%, 65%)",
                }}
              >
                {tab.label}
              </button>
            ))}
          </div>
        )}
      </div>
      <p className="text-sm" style={{ color: "hsl(214, 5%, 55%)" }}>
        {description}
      </p>
    </div>
  )
}

function SearchBar({
  value,
  onChange,
  placeholder,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
}) {
  return (
    <div className="relative mb-4">
      <Search
        className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2"
        style={{ color: "hsl(214, 5%, 55%)" }}
      />
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder ?? "Search settings..."}
        className="h-9 w-full rounded-md border pl-9 pr-3 text-sm outline-none transition-colors focus:ring-1"
        style={{
          backgroundColor: "hsl(228, 7%, 14%)",
          borderColor: "hsl(228, 6%, 30%)",
          color: "hsl(210, 3%, 90%)",
        }}
      />
    </div>
  )
}

function FormField({
  label,
  children,
  description,
}: {
  label: string
  children: React.ReactNode
  description?: string
}) {
  return (
    <div className="mb-4">
      <label
        className="mb-1.5 block text-sm font-medium"
        style={{ color: "hsl(210, 3%, 80%)" }}
      >
        {label}
      </label>
      {children}
      {description && (
        <p
          className="mt-1 text-xs"
          style={{ color: "hsl(214, 5%, 55%)" }}
        >
          {description}
        </p>
      )}
    </div>
  )
}

function TextInput({
  value,
  onChange,
  placeholder,
  type = "text",
  rightElement,
}: {
  value: string
  onChange: (v: string) => void
  placeholder?: string
  type?: string
  rightElement?: React.ReactNode
}) {
  return (
    <div className="relative flex">
      <input
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className="h-9 w-full rounded-md border px-3 text-sm outline-none transition-colors focus:ring-1"
        style={{
          backgroundColor: "hsl(228, 7%, 14%)",
          borderColor: "hsl(228, 6%, 30%)",
          color: "hsl(210, 3%, 90%)",
          paddingRight: rightElement ? "5rem" : undefined,
        }}
      />
      {rightElement && (
        <div className="absolute right-1 top-1/2 -translate-y-1/2">
          {rightElement}
        </div>
      )}
    </div>
  )
}

function NumberInput({
  value,
  onChange,
  min,
  max,
}: {
  value: number
  onChange: (v: number) => void
  min?: number
  max?: number
}) {
  return (
    <input
      type="number"
      value={value}
      onChange={(e) => onChange(Number(e.target.value))}
      min={min}
      max={max}
      className="h-9 w-full rounded-md border px-3 text-sm outline-none transition-colors focus:ring-1"
      style={{
        backgroundColor: "hsl(228, 7%, 14%)",
        borderColor: "hsl(228, 6%, 30%)",
        color: "hsl(210, 3%, 90%)",
      }}
    />
  )
}

function SelectInput({
  value,
  onChange,
  options,
  disabled,
}: {
  value: string
  onChange: (v: string) => void
  options: { value: string; label: string }[]
  disabled?: boolean
}) {
  return (
    <select
      value={value}
      onChange={(e) => onChange(e.target.value)}
      disabled={disabled}
      className="h-9 w-full rounded-md border px-3 text-sm outline-none transition-colors focus:ring-1 disabled:cursor-not-allowed disabled:opacity-70"
      style={{
        backgroundColor: "hsl(228, 7%, 14%)",
        borderColor: "hsl(228, 6%, 30%)",
        color: "hsl(210, 3%, 90%)",
      }}
    >
      {options.map((opt) => (
        <option key={opt.value} value={opt.value}>
          {opt.label}
        </option>
      ))}
    </select>
  )
}

function ToggleSwitch({
  checked,
  onChange,
}: {
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className="relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors"
      style={{
        backgroundColor: checked
          ? "hsl(235, 86%, 65%)"
          : "hsl(228, 6%, 30%)",
      }}
    >
      <span
        className="inline-block h-4 w-4 rounded-full bg-white transition-transform"
        style={{
          transform: checked ? "translateX(24px)" : "translateX(4px)",
        }}
      />
    </button>
  )
}

function ActionButton({
  variant = "secondary",
  onClick,
  children,
  disabled,
}: {
  variant?: "primary" | "secondary" | "danger"
  onClick?: () => void
  children: React.ReactNode
  disabled?: boolean
}) {
  const styles = {
    primary: {
      backgroundColor: "hsl(235, 86%, 65%)",
      color: "#fff",
    },
    secondary: {
      backgroundColor: "hsl(228, 6%, 30%)",
      color: "hsl(210, 3%, 90%)",
    },
    danger: {
      backgroundColor: "hsl(0, 70%, 45%)",
      color: "#fff",
    },
  }

  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className="rounded-md px-4 py-2 text-sm font-medium transition-opacity hover:opacity-90 disabled:opacity-50"
      style={styles[variant]}
    >
      {children}
    </button>
  )
}

/* ─────────────────────────────────────────────────
 * Providers Section
 * ───────────────────────────────────────────────── */

function ProviderStatusBadge({
  status,
  isModified,
}: {
  status: ProviderConfig["status"]
  isModified?: boolean
}) {
  const config = {
    draft: {
      icon: FileEdit,
      color: "hsl(214, 60%, 55%)",
      label: "Draft",
    },
    enabled: { icon: Circle, color: "hsl(145, 65%, 45%)", label: "Enabled" },
    disabled: { icon: XCircle, color: "hsl(214, 5%, 55%)", label: "Disabled" },
  }
  const modifiedStyle = {
    icon: Pencil,
    color: "hsl(40, 85%, 55%)",
    label: "Modified",
  }
  const { icon: Icon, color, label } = isModified ? modifiedStyle : config[status]

  return (
    <span className="flex items-center gap-1 text-xs" style={{ color }}>
      <Icon className="h-3 w-3" />
      {label}
    </span>
  )
}

function ProvidersSection({
  providers,
  modifiedProviderIds,
  selectedProviderId,
  onSelectProvider,
  onProvidersChange,
  onSave,
  isSaving,
  saveError,
}: {
  providers: ProviderConfig[]
  modifiedProviderIds: Set<string>
  selectedProviderId: string | null
  onSelectProvider: (id: string) => void
  onProvidersChange: (providers: ProviderConfig[]) => void
  onSave?: () => void
  isSaving?: boolean
  saveError?: string | null
}) {
  const [searchQuery, setSearchQuery] = useState("")
  const [showApiKey, setShowApiKey] = useState(false)
  const [addProviderDialogOpen, setAddProviderDialogOpen] = useState(false)

  const selectedProvider = providers.find((p) => p.id === selectedProviderId)

  const filteredProviders = providers.filter((p) =>
    p.name.toLowerCase().includes(searchQuery.toLowerCase())
  )

  const updateProvider = (id: string, updates: Partial<ProviderConfig>) => {
    onProvidersChange(
      providers.map((p) => (p.id === id ? { ...p, ...updates } : p))
    )
  }

  return (
    <div>
      <SectionHeader
        title="Settings"
        description="Connect model backends (cloud/local). Keys are stored locally and never shown again after save."
      />

      <SearchBar
        value={searchQuery}
        onChange={setSearchQuery}
        placeholder="Search settings..."
      />

      <div className="flex gap-6">
        {/* Provider list */}
        <div className="w-56 shrink-0">
          <h3
            className="mb-2 text-xs font-semibold uppercase tracking-wider"
            style={{ color: "hsl(214, 5%, 55%)" }}
          >
            Enabled providers
          </h3>
          <div className="flex flex-col gap-1">
            {filteredProviders.map((provider) => (
              <button
                key={provider.id}
                type="button"
                onClick={() => onSelectProvider(provider.id)}
                className={cn(
                  "flex flex-col rounded-lg px-3 py-2.5 text-left transition-colors"
                )}
                style={{
                  backgroundColor:
                    selectedProviderId === provider.id
                      ? "hsl(228, 6%, 28%)"
                      : "transparent",
                  borderLeft:
                    selectedProviderId === provider.id
                      ? "2px solid hsl(235, 86%, 65%)"
                      : "2px solid transparent",
                }}
              >
                <span
                  className="text-sm font-medium"
                  style={{ color: "hsl(210, 3%, 90%)" }}
                >
                  {provider.name}
                </span>
                <ProviderStatusBadge
                status={provider.status}
                isModified={modifiedProviderIds.has(provider.id)}
              />
              </button>
            ))}
            <button
              type="button"
              onClick={() => setAddProviderDialogOpen(true)}
              className="mb-1 flex w-full items-center justify-center gap-2 rounded-lg border-2 border-dashed px-3 py-2.5 text-left text-sm font-medium transition-all"
              style={{
                color: "hsl(214, 5%, 65%)",
                borderColor: "hsl(228, 6%, 32%)",
                backgroundColor: "transparent",
              }}
              onMouseEnter={(e) => {
                e.currentTarget.style.backgroundColor = "hsl(228, 6%, 18%)"
                e.currentTarget.style.borderColor = "hsl(235, 86%, 65%)"
                e.currentTarget.style.color = "hsl(210, 3%, 90%)"
              }}
              onMouseLeave={(e) => {
                e.currentTarget.style.backgroundColor = "transparent"
                e.currentTarget.style.borderColor = "hsl(228, 6%, 32%)"
                e.currentTarget.style.color = "hsl(214, 5%, 65%)"
              }}
            >
              <Plus className="h-4 w-4 shrink-0" />
              <span>Add Provider</span>
            </button>
          </div>
        </div>

        <Dialog open={addProviderDialogOpen} onOpenChange={setAddProviderDialogOpen}>
          <DialogContent
            className="rounded-lg border-0 p-6 shadow-lg"
            style={{
              backgroundColor: "rgb(49, 51, 56)",
              color: "rgb(255, 255, 255)",
            }}
          >
            <DialogHeader className="space-y-2 text-left">
              <DialogTitle className="text-lg font-semibold text-white">
                Add provider
              </DialogTitle>
              <DialogDescription asChild>
                <p className="text-sm" style={{ color: "rgb(163, 163, 163)" }}>
                  Choose a provider to add to your settings.
                </p>
              </DialogDescription>
            </DialogHeader>
            <div className="flex flex-col gap-0.5 py-2">
              {KNOWN_PROVIDERS.map(({ id, name, Icon, template }) => {
                const existingIds = new Set(providers.map((p) => p.id))
                const newId = existingIds.has(id)
                  ? `${id}-${Date.now()}`
                  : id
                return (
                  <button
                    key={id}
                    type="button"
                    onClick={() => {
                      onProvidersChange([
                        ...providers,
                        { ...template, id: newId, status: "draft", apiKey: "" },
                      ])
                      onSelectProvider(newId)
                      setAddProviderDialogOpen(false)
                    }}
                    className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm font-medium transition-colors"
                    style={{ color: "hsl(210, 3%, 90%)" }}
                    onMouseEnter={(e) => {
                      e.currentTarget.style.backgroundColor = "hsl(228, 6%, 28%)"
                    }}
                    onMouseLeave={(e) => {
                      e.currentTarget.style.backgroundColor = "transparent"
                    }}
                  >
                    <span className="h-5 w-5 shrink-0" style={{ color: "hsl(214, 5%, 65%)" }}>
                      <Icon className="h-5 w-5 shrink-0" />
                    </span>
                    <span>{name}</span>
                  </button>
                )
              })}
            </div>
          </DialogContent>
        </Dialog>

        {/* Provider detail form */}
        {selectedProvider && (
          <div className="min-w-0 flex-1">
            <div className="mb-4">
              <h3
                className="text-lg font-semibold"
                style={{ color: "hsl(210, 3%, 95%)" }}
              >
                {selectedProvider.name}
              </h3>
              <div className="flex items-center gap-2">
                <ProviderStatusBadge
                status={selectedProvider.status}
                isModified={modifiedProviderIds.has(selectedProvider.id)}
              />
                {selectedProvider.isDefault && (
                  <span
                    className="text-xs"
                    style={{ color: "hsl(214, 5%, 55%)" }}
                  >
                    &middot; Default provider for new agents
                  </span>
                )}
              </div>
            </div>

            <FormField label="Type">
              <SelectInput
                value={selectedProvider.providerType ?? selectedProvider.name}
                onChange={() => {}}
                options={(() => {
                  const known: { value: string; label: string }[] = [
                    { value: "OpenAI", label: "OpenAI" },
                    { value: "Azure OpenAI", label: "Azure OpenAI" },
                    { value: "Anthropic", label: "Anthropic" },
                    { value: "Ollama (Local)", label: "Ollama (Local)" },
                  ]
                  const current = selectedProvider.providerType ?? selectedProvider.name
                  if (current && !known.some((o) => o.value === current)) {
                    return [...known, { value: current, label: current }]
                  }
                  return known
                })()}
                disabled
              />
            </FormField>

            <FormField label="Name">
              <TextInput
                value={selectedProvider.name}
                onChange={(v) =>
                  updateProvider(selectedProvider.id, { name: v })
                }
                placeholder="Provider display name"
              />
            </FormField>

            <FormField label="API Key">
              <TextInput
                value={selectedProvider.apiKey}
                onChange={(v) =>
                  updateProvider(selectedProvider.id, { apiKey: v })
                }
                type={showApiKey ? "text" : "password"}
                placeholder="Enter API key..."
                rightElement={
                  <button
                    type="button"
                    onClick={() => setShowApiKey(!showApiKey)}
                    className="rounded p-1 transition-colors hover:bg-[hsl(228,6%,35%)]"
                    style={{ color: "hsl(214, 5%, 65%)" }}
                    aria-label={showApiKey ? "Hide API key" : "Show API key"}
                  >
                    {showApiKey ? (
                      <EyeOff className="h-4 w-4" />
                    ) : (
                      <Eye className="h-4 w-4" />
                    )}
                  </button>
                }
              />
            </FormField>

            <FormField label="Base URL">
              <TextInput
                value={selectedProvider.baseUrl}
                onChange={(v) =>
                  updateProvider(selectedProvider.id, { baseUrl: v })
                }
                placeholder="https://api.example.com/v1"
              />
            </FormField>

            <FormField label="Default model">
              <TextInput
                value={selectedProvider.defaultModel}
                onChange={(v) =>
                  updateProvider(selectedProvider.id, { defaultModel: v })
                }
                placeholder="Model name"
              />
            </FormField>

            <FormField
              label="Enabled"
              description="When off, this provider is disabled and won't be used."
            >
              <ToggleSwitch
                checked={selectedProvider.status !== "disabled"}
                onChange={(v) =>
                  updateProvider(selectedProvider.id, {
                    status: v ? "enabled" : "disabled",
                  })
                }
              />
            </FormField>

            {saveError && (
              <p className="mt-2 text-sm" style={{ color: "hsl(0, 70%, 55%)" }}>
                {saveError}
              </p>
            )}
            <div className="mt-6 flex gap-3">
              <ActionButton
                variant="primary"
                onClick={onSave}
                disabled={isSaving}
              >
                {isSaving ? "Saving…" : "Save"}
              </ActionButton>
              <ActionButton variant="secondary">Test</ActionButton>
              <ActionButton
                variant="danger"
                onClick={() => {
                  onProvidersChange(providers.filter((p) => p.id !== selectedProvider.id))
                  const remaining = providers.filter((p) => p.id !== selectedProvider.id)
                  onSelectProvider(remaining[0]?.id ?? null)
                }}
              >
                Remove
              </ActionButton>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────
 * Agents Section
 * ───────────────────────────────────────────────── */

function AgentsSection({
  onNavigateToAllAgents,
}: {
  onNavigateToAllAgents?: () => void
}) {
  return (
    <div>
      <SectionHeader
        title="Agents"
        description="Configure default agent behaviors, model assignments, and system prompts."
      />
      <div
        className="rounded-lg border p-8 text-center"
        style={{
          borderColor: "hsl(228, 6%, 30%)",
          color: "hsl(214, 5%, 55%)",
        }}
      >
        <p className="text-sm">
          Agent settings are managed per-agent. Use the{" "}
          {onNavigateToAllAgents ? (
            <button
              type="button"
              onClick={onNavigateToAllAgents}
              className="underline focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-offset-transparent rounded"
              style={{
                color: "hsl(235, 86%, 65%)",
                backgroundColor: "transparent",
                border: "none",
                padding: 0,
                cursor: "pointer",
              }}
            >
              All Agents
            </button>
          ) : (
            <span style={{ color: "hsl(235, 86%, 65%)" }}>All Agents</span>
          )}{" "}
          view to configure individual agents.
        </p>
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────
 * Routing Section
 * ───────────────────────────────────────────────── */

function RoutingSection({
  routing,
  onRoutingChange,
}: {
  routing: RoutingConfig
  onRoutingChange: (r: RoutingConfig) => void
}) {
  return (
    <div
      className="pointer-events-none select-none opacity-60"
      aria-disabled="true"
    >
      <SectionHeader
        title="Routing"
        description="Control how requests are routed between providers and models."
      />

      <FormField
        label="Strategy"
        description="How requests are distributed across available providers."
      >
        <SelectInput
          value={routing.strategy}
          onChange={(v) =>
            onRoutingChange({
              ...routing,
              strategy: v as RoutingConfig["strategy"],
            })
          }
          options={[
            { value: "round-robin", label: "Round Robin" },
            { value: "priority", label: "Priority (default provider first)" },
            { value: "cost-optimized", label: "Cost Optimized" },
            { value: "latency-optimized", label: "Latency Optimized" },
          ]}
        />
      </FormField>

      <FormField
        label="Fallback enabled"
        description="Automatically try another provider if the primary fails."
      >
        <ToggleSwitch
          checked={routing.fallbackEnabled}
          onChange={(v) => onRoutingChange({ ...routing, fallbackEnabled: v })}
        />
      </FormField>

      <FormField
        label="Max retries"
        description="Maximum number of retry attempts before failing."
      >
        <NumberInput
          value={routing.maxRetries}
          onChange={(v) => onRoutingChange({ ...routing, maxRetries: v })}
          min={0}
          max={10}
        />
      </FormField>
    </div>
  )
}

/* ─────────────────────────────────────────────────
 * Advanced Section
 * ───────────────────────────────────────────────── */

function AdvancedSection({
  advanced,
  onAdvancedChange,
}: {
  advanced: AdvancedConfig
  onAdvancedChange: (a: AdvancedConfig) => void
}) {
  return (
    <div>
      <SectionHeader
        title="Advanced"
        description="Low-level configuration options. Change these only if you know what you're doing."
      />

      <FormField
        label="Developer mode"
        description="Enable developer overlays and non-released features."
      >
        <ToggleSwitch
          checked={advanced.debugMode}
          onChange={(v) => onAdvancedChange({ ...advanced, debugMode: v })}
        />
      </FormField>
    </div>
  )
}

/* ─────────────────────────────────────────────────
 * Account Section
 * ───────────────────────────────────────────────── */

function AccountSection({
  account,
  onAccountChange,
}: {
  account: AccountConfig
  onAccountChange: (account: AccountConfig) => void
}) {
  const initial = (account.displayName || "U").trim().toUpperCase().slice(0, 1) || "U"
  return (
    <div>
      <SectionHeader
        title="Account"
        description="Manage your account settings and preferences."
      />

      <div
        className="flex items-center gap-4 rounded-lg border p-4"
        style={{ borderColor: "hsl(228, 6%, 30%)" }}
      >
        <div
          className="flex h-16 w-16 items-center justify-center rounded-full text-xl font-bold"
          style={{
            backgroundColor: "hsl(235, 86%, 65%)",
            color: "#fff",
          }}
        >
          {initial}
        </div>
        <div>
          <div
            className="text-base font-semibold"
            style={{ color: "hsl(210, 3%, 95%)" }}
          >
            {account.displayName || "User"}
          </div>
          <div className="text-sm" style={{ color: "hsl(214, 5%, 55%)" }}>
            Local account
          </div>
        </div>
      </div>

      <div className="mt-6">
        <FormField label="Display name">
          <TextInput
            value={account.displayName ?? ""}
            onChange={(v) => onAccountChange({ ...account, displayName: v })}
            placeholder="Your display name"
          />
        </FormField>
      </div>
    </div>
  )
}

/* ─────────────────────────────────────────────────
 * Appearance Section
 * ───────────────────────────────────────────────── */

function AppearanceSection({
  appearance,
  onAppearanceChange,
}: {
  appearance: AppearanceConfig
  onAppearanceChange: (a: AppearanceConfig) => void
}) {
  return (
    <div
      className="pointer-events-none select-none opacity-60"
      aria-disabled="true"
    >
      <SectionHeader
        title="Appearance"
        description="Customize the look and feel of the application."
      />

      <FormField label="Theme">
        <SelectInput
          value={appearance.theme}
          onChange={(v) =>
            onAppearanceChange({
              ...appearance,
              theme: v as AppearanceConfig["theme"],
            })
          }
          options={[
            { value: "dark", label: "Dark" },
            { value: "light", label: "Light" },
            { value: "system", label: "System" },
          ]}
        />
      </FormField>

      <FormField label="Font size">
        <SelectInput
          value={appearance.fontSize}
          onChange={(v) =>
            onAppearanceChange({
              ...appearance,
              fontSize: v as AppearanceConfig["fontSize"],
            })
          }
          options={[
            { value: "small", label: "Small (13px)" },
            { value: "medium", label: "Medium (15px)" },
            { value: "large", label: "Large (18px)" },
          ]}
        />
      </FormField>

      <FormField
        label="Compact mode"
        description="Reduce spacing between messages and UI elements."
      >
        <ToggleSwitch
          checked={appearance.compactMode}
          onChange={(v) =>
            onAppearanceChange({ ...appearance, compactMode: v })
          }
        />
      </FormField>
    </div>
  )
}

/* ─────────────────────────────────────────────────
 * Notifications Section
 * ───────────────────────────────────────────────── */

function NotificationsSection({
  notifications,
  onNotificationsChange,
}: {
  notifications: NotificationConfig
  onNotificationsChange: (n: NotificationConfig) => void
}) {
  return (
    <div
      className="pointer-events-none select-none opacity-60"
      aria-disabled="true"
    >
      <SectionHeader
        title="Notifications"
        description="Configure how and when you receive notifications."
      />

      <FormField
        label="Desktop notifications"
        description="Show system notifications for new messages."
      >
        <ToggleSwitch
          checked={notifications.desktopNotifications}
          onChange={(v) =>
            onNotificationsChange({
              ...notifications,
              desktopNotifications: v,
            })
          }
        />
      </FormField>

      <FormField
        label="Sound"
        description="Play a sound when notifications arrive."
      >
        <ToggleSwitch
          checked={notifications.soundEnabled}
          onChange={(v) =>
            onNotificationsChange({ ...notifications, soundEnabled: v })
          }
        />
      </FormField>

      <FormField
        label="Mention notifications"
        description="Notify when you are mentioned by an agent or in a channel."
      >
        <ToggleSwitch
          checked={notifications.mentionNotifications}
          onChange={(v) =>
            onNotificationsChange({
              ...notifications,
              mentionNotifications: v,
            })
          }
        />
      </FormField>
    </div>
  )
}
