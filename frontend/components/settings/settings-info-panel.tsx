"use client"

import { useState } from "react"
import { Circle, ChevronDown, ChevronUp } from "lucide-react"
import { ScrollArea } from "@/components/ui/scroll-area"
import { type SettingsSection } from "./settings-sidebar"
import { type ProviderConfig, type ProviderTestResult } from "./settings-types"

interface SettingsInfoPanelProps {
  activeSection: SettingsSection
  selectedProvider: ProviderConfig | null
  providerTestResult?: ProviderTestResult | null
}

export function SettingsInfoPanel({
  activeSection,
  selectedProvider,
  providerTestResult = null,
}: SettingsInfoPanelProps) {
  return (
    <div
      className="flex h-full w-[260px] shrink-0 flex-col"
      style={{
        backgroundColor: "hsl(228, 7%, 14%)",
        borderLeft: "1px solid hsl(228, 6%, 20%)",
      }}
    >
      <ScrollArea className="flex-1">
        <div className="p-4">
          {activeSection === "providers" && selectedProvider && (
            <ProviderInfo provider={selectedProvider} providerTestResult={providerTestResult} />
          )}
          {activeSection === "providers" && !selectedProvider && (
            <GeneralProviderInfo />
          )}
          {activeSection === "routing" && <RoutingInfo />}
          {activeSection === "advanced" && <AdvancedInfo />}
          {activeSection === "account" && <AccountInfo />}
          {activeSection === "appearance" && <AppearanceInfo />}
          {activeSection === "notifications" && <NotificationsInfo />}
          {activeSection === "agents" && <AgentsInfo />}
        </div>
      </ScrollArea>
    </div>
  )
}

/* ─────────────────────────────────────────────────
 * Info panel components
 * ───────────────────────────────────────────────── */

function InfoSection({
  title,
  children,
}: {
  title: string
  children: React.ReactNode
}) {
  return (
    <div className="mb-5">
      <h4
        className="mb-2 text-[11px] font-bold uppercase tracking-wider"
        style={{ color: "hsl(214, 5%, 55%)" }}
      >
        {title}
      </h4>
      <div>{children}</div>
    </div>
  )
}

function InfoRow({
  label,
  value,
  valueColor,
}: {
  label: string
  value: string
  valueColor?: string
}) {
  return (
    <div className="flex items-center justify-between py-1">
      <span className="text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
        {label}
      </span>
      <span
        className="text-xs font-medium"
        style={{ color: valueColor ?? "hsl(210, 3%, 80%)" }}
      >
        {value}
      </span>
    </div>
  )
}

function NoteBullet({ children }: { children: React.ReactNode }) {
  return (
    <li
      className="flex items-start gap-1.5 text-xs leading-relaxed"
      style={{ color: "hsl(214, 5%, 65%)" }}
    >
      <span className="mt-1 shrink-0">•</span>
      <span>{children}</span>
    </li>
  )
}

/* ─────────────────────────────────────────────────
 * Provider-specific info
 * ───────────────────────────────────────────────── */

function formatCheckedAt(checkedAt?: string): string {
  if (!checkedAt) return "—"
  try {
    const d = new Date(checkedAt)
    if (Number.isNaN(d.getTime())) return checkedAt
    const now = new Date()
    const diffMs = now.getTime() - d.getTime()
    if (diffMs < 60_000) return "just now"
    if (diffMs < 3600_000) return `${Math.floor(diffMs / 60_000)} min ago`
    if (diffMs < 86400_000) return `${Math.floor(diffMs / 3600_000)} h ago`
    return d.toLocaleString()
  } catch {
    return checkedAt
  }
}

function ProviderInfo({
  provider,
  providerTestResult,
}: {
  provider: ProviderConfig
  providerTestResult: ProviderTestResult | null
}) {
  const [modelsExpanded, setModelsExpanded] = useState(false)
  const hasTestResult = providerTestResult != null
  const statusColor = hasTestResult && providerTestResult.success
    ? "hsl(145, 65%, 45%)"
    : hasTestResult && !providerTestResult.success
      ? "hsl(0, 70%, 55%)"
      : "hsl(214, 5%, 55%)"

  return (
    <>
      <InfoSection title="Test result">
        <div
          className="mb-3 rounded-lg border p-3"
          style={{ borderColor: "hsl(228, 6%, 25%)" }}
        >
          {!hasTestResult ? (
            <p
              className="text-xs leading-relaxed"
              style={{ color: "hsl(214, 5%, 55%)" }}
            >
              Press &ldquo;Test&rdquo; to check connectivity and see latency, last check, and quota.
            </p>
          ) : (
            <>
              <div className="mb-2 flex items-center gap-2">
                <Circle
                  className="h-2.5 w-2.5"
                  style={{ color: statusColor, fill: statusColor }}
                />
                <span
                  className="text-sm font-semibold"
                  style={{ color: "hsl(210, 3%, 95%)" }}
                >
                  {provider.name}
                </span>
              </div>
              <InfoRow
                label="Latency"
                value={providerTestResult.latencyMs != null ? `${providerTestResult.latencyMs} ms` : "—"}
              />
              <InfoRow
                label="Last check"
                value={formatCheckedAt(providerTestResult.checkedAt)}
              />
              <InfoRow
                label="Quota"
                value={providerTestResult.quota ?? "—"}
                valueColor={
                  providerTestResult.success ? "hsl(145, 65%, 45%)" : "hsl(0, 70%, 55%)"
                }
              />
            </>
          )}
        </div>
      </InfoSection>

      <InfoSection title="Available models">
        <div className="flex flex-col gap-1">
          {!providerTestResult?.availableModels?.length ? (
            <p
              className="text-xs leading-relaxed"
              style={{ color: "hsl(214, 5%, 55%)" }}
            >
              Press &ldquo;Test&rdquo; to see available models.
            </p>
          ) : (() => {
            const isDefault = (m: { id: string; name: string }) =>
              provider.defaultModel && (m.id === provider.defaultModel || m.name === provider.defaultModel)
            const models = [...providerTestResult.availableModels].sort((a, b) =>
              isDefault(a) ? -1 : isDefault(b) ? 1 : 0
            )
            const initialCount = 5
            const showAll = modelsExpanded || models.length <= initialCount
            const visible = showAll ? models : models.slice(0, initialCount)
            const hiddenCount = models.length - initialCount
            return (
              <>
                {visible.map((m) => {
                  const defaultModel = isDefault(m)
                  return (
                    <div
                      key={m.id}
                      className="rounded-md px-2.5 py-1.5 text-xs"
                      style={{
                        backgroundColor: "hsl(228, 6%, 20%)",
                        color: "hsl(210, 3%, 80%)",
                        fontWeight: defaultModel ? 600 : undefined,
                      }}
                    >
                      {m.name}
                      {defaultModel ? " (default)" : ""}
                    </div>
                  )
                })}
                {!showAll && hiddenCount > 0 && (
                  <button
                    type="button"
                    onClick={() => setModelsExpanded(true)}
                    className="flex items-center gap-1 rounded-md px-2.5 py-1.5 text-xs transition-opacity hover:opacity-90"
                    style={{
                      backgroundColor: "hsl(228, 6%, 25%)",
                      color: "hsl(214, 5%, 65%)",
                    }}
                  >
                    <ChevronDown className="h-3.5 w-3.5" />
                    Show {hiddenCount} more
                  </button>
                )}
                {showAll && models.length > initialCount && (
                  <button
                    type="button"
                    onClick={() => setModelsExpanded(false)}
                    className="flex items-center gap-1 rounded-md px-2.5 py-1.5 text-xs transition-opacity hover:opacity-90"
                    style={{
                      backgroundColor: "hsl(228, 6%, 25%)",
                      color: "hsl(214, 5%, 65%)",
                    }}
                  >
                    <ChevronUp className="h-3.5 w-3.5" />
                    Show less
                  </button>
                )}
              </>
            )
          })()}
        </div>
      </InfoSection>

      <InfoSection title="Notes">
        <ul className="flex flex-col gap-1.5">
          <NoteBullet>Keys are encrypted at rest.</NoteBullet>
          <NoteBullet>Use profiles (dev/prod) to switch endpoints.</NoteBullet>
          <NoteBullet>Agents can override the default provider/model.</NoteBullet>
        </ul>
      </InfoSection>
    </>
  )
}

function GeneralProviderInfo() {
  return (
    <>
      <InfoSection title="Providers">
        <p className="text-xs leading-relaxed" style={{ color: "hsl(214, 5%, 65%)" }}>
          Select a provider from the list to view its test result,
          available models, and configuration details.
        </p>
      </InfoSection>
      <InfoSection title="Quick tips">
        <ul className="flex flex-col gap-1.5">
          <NoteBullet>Click a provider to see its details.</NoteBullet>
          <NoteBullet>Use &ldquo;Test&rdquo; to verify connectivity.</NoteBullet>
          <NoteBullet>The default provider is used for new agents.</NoteBullet>
        </ul>
      </InfoSection>
    </>
  )
}

/* ─────────────────────────────────────────────────
 * Section-specific info panels
 * ───────────────────────────────────────────────── */

function RoutingInfo() {
  return (
    <>
      <InfoSection title="Routing strategies">
        <ul className="flex flex-col gap-1.5">
          <NoteBullet>
            <strong>Priority:</strong> Always tries the default provider first.
          </NoteBullet>
          <NoteBullet>
            <strong>Round Robin:</strong> Distributes load evenly across providers.
          </NoteBullet>
          <NoteBullet>
            <strong>Cost Optimized:</strong> Prefers cheaper models when possible.
          </NoteBullet>
          <NoteBullet>
            <strong>Latency Optimized:</strong> Routes to the fastest responding provider.
          </NoteBullet>
        </ul>
      </InfoSection>
      <InfoSection title="Notes">
        <ul className="flex flex-col gap-1.5">
          <NoteBullet>Fallback kicks in when the primary provider fails or times out.</NoteBullet>
          <NoteBullet>Per-agent routing overrides take precedence over global settings.</NoteBullet>
        </ul>
      </InfoSection>
    </>
  )
}

function AdvancedInfo() {
  return (
    <>
      <InfoSection title="Caution">
        <p className="text-xs leading-relaxed" style={{ color: "hsl(40, 85%, 55%)" }}>
          Changing advanced settings may affect application stability. Only modify
          these if you understand the implications.
        </p>
      </InfoSection>
      <InfoSection title="Notes">
        <ul className="flex flex-col gap-1.5">
          <NoteBullet>Developer mode enables overlays and non-released features.</NoteBullet>
        </ul>
      </InfoSection>
    </>
  )
}

function AccountInfo() {
  return (
    <InfoSection title="Account">
      <p className="text-xs leading-relaxed" style={{ color: "hsl(214, 5%, 65%)" }}>
        Your account is stored locally. All settings and data persist in
        the local database.
      </p>
    </InfoSection>
  )
}

function AppearanceInfo() {
  return (
    <>
      <InfoSection title="Theme">
        <p className="text-xs leading-relaxed" style={{ color: "hsl(214, 5%, 65%)" }}>
          The dark theme is optimized for extended use. Light and system themes
          are available for accessibility.
        </p>
      </InfoSection>
      <InfoSection title="Notes">
        <ul className="flex flex-col gap-1.5">
          <NoteBullet>Theme changes apply instantly.</NoteBullet>
          <NoteBullet>Compact mode reduces vertical spacing for denser layouts.</NoteBullet>
        </ul>
      </InfoSection>
    </>
  )
}

function NotificationsInfo() {
  return (
    <InfoSection title="Notifications">
      <ul className="flex flex-col gap-1.5">
        <NoteBullet>Desktop notifications require browser permission.</NoteBullet>
        <NoteBullet>Sounds play only when the tab is in the background.</NoteBullet>
        <NoteBullet>Mention notifications are triggered by @-mentions in channels.</NoteBullet>
      </ul>
    </InfoSection>
  )
}

function AgentsInfo() {
  return (
    <InfoSection title="Agents">
      <ul className="flex flex-col gap-1.5">
        <NoteBullet>Each agent can have its own provider and model.</NoteBullet>
        <NoteBullet>System prompts define the agent&apos;s personality and capabilities.</NoteBullet>
        <NoteBullet>MCP connections enable external tool integration.</NoteBullet>
      </ul>
    </InfoSection>
  )
}
