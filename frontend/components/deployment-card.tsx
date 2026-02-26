"use client"

import { useState, useCallback, type CSSProperties } from "react"
import {
  CheckCircle2,
  XCircle,
  Loader2,
  Circle,
  AlertTriangle,
  ChevronDown,
  ChevronRight,
  RotateCcw,
  Check,
} from "lucide-react"

export interface DeploymentEnv {
  name: string
  status: string
  version?: string
  lastDeployed?: string
}

interface DeploymentCardProps {
  applicationName: string
  environments: DeploymentEnv[]
  onFollowUp?: (message: string) => void
}

const statusColorMap: Record<string, string> = {
  succeeded: "#43b581",
  success: "#43b581",
  completed: "#43b581",
  done: "#43b581",
  resolved: "#43b581",
  closed: "#43b581",
  healthy: "#43b581",
  running: "#0078d4",
  "in progress": "#0078d4",
  inprogress: "#0078d4",
  active: "#0078d4",
  deploying: "#0078d4",
  building: "#0078d4",
  pending: "#99aab5",
  queued: "#99aab5",
  waiting: "#99aab5",
  new: "#99aab5",
  "to do": "#99aab5",
  todo: "#99aab5",
  "not started": "#99aab5",
  failed: "#f04747",
  error: "#f04747",
  unhealthy: "#f04747",
  cancelled: "#faa61a",
  canceled: "#faa61a",
  warning: "#faa61a",
  degraded: "#faa61a",
  skipped: "#747f8d",
  removed: "#747f8d",
}

function getStatusColor(s: string): string {
  return statusColorMap[s.toLowerCase().trim()] ?? "#99aab5"
}

const isSuccess = (s: string) =>
  ["succeeded", "success", "completed", "done", "resolved", "closed", "healthy"].includes(
    s.toLowerCase().trim()
  )
const isFailed = (s: string) =>
  ["failed", "error", "unhealthy"].includes(s.toLowerCase().trim())
const isPending = (s: string) =>
  ["pending", "queued", "waiting", "new", "to do", "todo", "not started"].includes(
    s.toLowerCase().trim()
  )

function StatusIcon({ status, size = 14 }: { status: string; size?: number }) {
  const color = getStatusColor(status)
  if (isSuccess(status)) return <CheckCircle2 size={size} color={color} />
  if (status.toLowerCase().trim() === "running" ||
      ["in progress", "inprogress", "active", "deploying", "building"].includes(status.toLowerCase().trim()))
    return <Loader2 size={size} color={color} style={{ animation: "spin 1s linear infinite" }} />
  if (isFailed(status)) return <XCircle size={size} color={color} />
  if (["cancelled", "canceled", "warning", "degraded"].includes(status.toLowerCase().trim()))
    return <AlertTriangle size={size} color={color} />
  return <Circle size={size} color={color} />
}

const cardStyle: CSSProperties = {
  background: "linear-gradient(135deg, rgba(30, 31, 35, 0.95), rgba(40, 41, 46, 0.95))",
  border: "1px solid rgba(255, 255, 255, 0.08)",
  borderRadius: 12,
  padding: 16,
  marginTop: 8,
  marginBottom: 8,
  maxWidth: 560,
  fontSize: 13,
  color: "#dcddde",
  fontFamily: "inherit",
}

const headerStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  marginBottom: 12,
}

const titleStyle: CSSProperties = {
  fontSize: 15,
  fontWeight: 600,
  color: "#ffffff",
  margin: 0,
}

function StatusBadge({ status }: { status: string }) {
  const c = getStatusColor(status)
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        padding: "2px 8px",
        borderRadius: 12,
        fontSize: 11,
        fontWeight: 600,
        backgroundColor: `${c}22`,
        color: c,
        border: `1px solid ${c}44`,
        textTransform: "uppercase",
        letterSpacing: 0.5,
        flexShrink: 0,
      }}
    >
      <StatusIcon status={status} size={11} /> {status}
    </span>
  )
}

function ActionBtn({
  label,
  icon,
  onClick,
  color = "#0078d4",
  small = false,
  disabled = false,
}: {
  label: string
  icon?: React.ReactNode
  onClick: () => void
  color?: string
  small?: boolean
  disabled?: boolean
}) {
  const [hov, setHov] = useState(false)
  return (
    <button
      disabled={disabled}
      onClick={(e) => {
        e.stopPropagation()
        onClick()
      }}
      onMouseEnter={() => setHov(true)}
      onMouseLeave={() => setHov(false)}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        padding: small ? "2px 8px" : "4px 10px",
        borderRadius: 6,
        fontSize: small ? 10 : 11,
        fontWeight: 600,
        border: `1px solid ${color}55`,
        color,
        backgroundColor: hov && !disabled ? `${color}22` : "transparent",
        cursor: disabled ? "default" : "pointer",
        opacity: disabled ? 0.5 : 1,
        transition: "background-color 0.15s, opacity 0.15s",
        whiteSpace: "nowrap",
      }}
    >
      {icon} {label}
    </button>
  )
}

function ExpandToggle({ expanded }: { expanded: boolean }) {
  return expanded ? (
    <ChevronDown
      size={14}
      color="#99aab5"
      style={{ transition: "transform 0.15s", flexShrink: 0 }}
    />
  ) : (
    <ChevronRight
      size={14}
      color="#99aab5"
      style={{ transition: "transform 0.15s", flexShrink: 0 }}
    />
  )
}

function DetailPanel({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        marginTop: 8,
        padding: "8px 10px",
        borderRadius: 6,
        backgroundColor: "rgba(0,0,0,0.25)",
        border: "1px solid rgba(255,255,255,0.06)",
        fontSize: 12,
        color: "#b9bbbe",
      }}
    >
      {children}
    </div>
  )
}

export function DeploymentCard({ applicationName, environments, onFollowUp }: DeploymentCardProps) {
  const [expanded, setExpanded] = useState<number | null>(null)
  const [hovEnv, setHovEnv] = useState<number | null>(null)
  const [confirmed, setConfirmed] = useState<Record<number, boolean>>({})

  const handleFollowUp = useCallback((message: string) => {
    if (onFollowUp) {
      onFollowUp(message)
    }
  }, [onFollowUp])

  const handleApprove = (env: DeploymentEnv, idx: number) => {
    setConfirmed((prev) => ({ ...prev, [idx]: true }))
    handleFollowUp(
      `Approve the deployment of "${applicationName}" to ${env.name}${env.version ? ` (version ${env.version})` : ""}.`
    )
  }

  return (
    <div style={cardStyle}>
      <div style={headerStyle}>
        <h3 style={titleStyle}>{applicationName}</h3>
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
        {environments.map((env, i) => {
          const isOpen = expanded === i
          const hovered = hovEnv === i
          const sc = getStatusColor(env.status)
          return (
            <div key={i}>
              <div
                onClick={() => setExpanded(isOpen ? null : i)}
                onMouseEnter={() => setHovEnv(i)}
                onMouseLeave={() => setHovEnv(null)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 10,
                  padding: "8px 12px",
                  borderRadius: 8,
                  cursor: "pointer",
                  transition: "all 0.15s",
                  backgroundColor: isOpen ? `${sc}18` : hovered ? `${sc}10` : `${sc}08`,
                  borderTopWidth: 1,
                  borderTopStyle: "solid",
                  borderTopColor: isOpen ? `${sc}44` : "transparent",
                  borderRightWidth: 1,
                  borderRightStyle: "solid",
                  borderRightColor: isOpen ? `${sc}44` : "transparent",
                  borderBottomWidth: 1,
                  borderBottomStyle: "solid",
                  borderBottomColor: isOpen ? `${sc}44` : "transparent",
                  borderLeftWidth: 3,
                  borderLeftStyle: "solid",
                  borderLeftColor: sc,
                }}
              >
                <ExpandToggle expanded={isOpen} />
                <StatusIcon status={env.status} size={14} />
                <div style={{ flex: 1 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <span style={{ fontSize: 13, fontWeight: 600, color: "#ffffff" }}>
                      {env.name}
                    </span>
                    {env.version && (
                      <span style={{ fontSize: 11, color: "#99aab5", fontFamily: "monospace" }}>
                        v{env.version}
                      </span>
                    )}
                  </div>
                  {env.lastDeployed && (
                    <span style={{ fontSize: 11, color: "#747f8d" }}>{env.lastDeployed}</span>
                  )}
                </div>
                <StatusBadge status={env.status} />
              </div>

              {/* Expanded detail panel */}
              {isOpen && (
                <DetailPanel>
                  <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                    {isPending(env.status) && !confirmed[i] && (
                      <ActionBtn
                        label="Approve Deployment"
                        icon={<CheckCircle2 size={11} />}
                        onClick={() => handleApprove(env, i)}
                        color="#43b581"
                      />
                    )}
                    {confirmed[i] && (
                      <span
                        style={{
                          fontSize: 11,
                          color: "#43b581",
                          display: "flex",
                          alignItems: "center",
                          gap: 4,
                        }}
                      >
                        <CheckCircle2 size={12} /> Approval requested
                      </span>
                    )}
                    {isSuccess(env.status) && (
                      <ActionBtn
                        label="Rollback"
                        icon={<RotateCcw size={11} />}
                        onClick={() =>
                          handleFollowUp(
                            `Rollback "${applicationName}" in ${env.name} from version ${env.version ?? "current"}.`
                          )
                        }
                        color="#f04747"
                      />
                    )}
                    {isFailed(env.status) && (
                      <ActionBtn
                        label="Retry Deploy"
                        icon={<RotateCcw size={11} />}
                        onClick={() =>
                          handleFollowUp(`Retry the failed deployment of "${applicationName}" to ${env.name}.`)
                        }
                        color="#faa61a"
                      />
                    )}
                  </div>
                </DetailPanel>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}
