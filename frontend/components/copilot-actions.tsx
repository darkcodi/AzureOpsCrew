"use client"

import { useState, useCallback, type CSSProperties, type ReactNode } from "react"
import { useCopilotAction, useCopilotChatInternal } from "@copilotkit/react-core"
import {
  GitBranch,
  Clock,
  User,
  CheckCircle2,
  XCircle,
  Loader2,
  Circle,
  AlertTriangle,
  Server,
  Globe,
  Activity,
  TrendingUp,
  TrendingDown,
  Minus,
  ArrowRight,
  Package,
  Tag,
  ChevronDown,
  ChevronRight,
  RotateCcw,
  FileText,
  Copy,
  Check,
  Play,
  Square,
  ArrowUpCircle,
  Filter,
  Search,
} from "lucide-react"

/* ═══════════════════════════════════════════════
   Shared helpers, hooks & base components
   ═══════════════════════════════════════════════ */

const statusColorMap: Record<string, string> = {
  succeeded: "#43b581", success: "#43b581", completed: "#43b581",
  done: "#43b581", resolved: "#43b581", closed: "#43b581", healthy: "#43b581",
  running: "#0078d4", "in progress": "#0078d4", inprogress: "#0078d4",
  active: "#0078d4", deploying: "#0078d4", building: "#0078d4",
  pending: "#99aab5", queued: "#99aab5", waiting: "#99aab5",
  new: "#99aab5", "to do": "#99aab5", todo: "#99aab5", "not started": "#99aab5",
  failed: "#f04747", error: "#f04747", unhealthy: "#f04747",
  cancelled: "#faa61a", canceled: "#faa61a", warning: "#faa61a", degraded: "#faa61a",
  skipped: "#747f8d", removed: "#747f8d",
}

function getStatusColor(s: string): string {
  return statusColorMap[s.toLowerCase().trim()] ?? "#99aab5"
}

const isSuccess = (s: string) =>
  ["succeeded", "success", "completed", "done", "resolved", "closed", "healthy"].includes(s.toLowerCase().trim())
const isRunning = (s: string) =>
  ["running", "in progress", "inprogress", "active", "deploying", "building"].includes(s.toLowerCase().trim())
const isFailed = (s: string) =>
  ["failed", "error", "unhealthy"].includes(s.toLowerCase().trim())
const isPending = (s: string) =>
  ["pending", "queued", "waiting", "new", "to do", "todo", "not started"].includes(s.toLowerCase().trim())

function StatusIcon({ status, size = 14 }: { status: string; size?: number }) {
  const color = getStatusColor(status)
  if (isSuccess(status)) return <CheckCircle2 size={size} color={color} />
  if (isRunning(status))
    return <Loader2 size={size} color={color} style={{ animation: "spin 1s linear infinite" }} />
  if (isFailed(status)) return <XCircle size={size} color={color} />
  if (["cancelled", "canceled", "warning", "degraded"].includes(status.toLowerCase().trim()))
    return <AlertTriangle size={size} color={color} />
  return <Circle size={size} color={color} />
}

/* ── Hook: send follow-up message to agent ── */

function useFollowUp() {
  const { sendMessage } = useCopilotChatInternal()
  return useCallback(
    (content: string) => {
      sendMessage({ id: crypto.randomUUID(), role: "user", content }).catch(() => {})
    },
    [sendMessage],
  )
}

/* ── Hook: copy to clipboard ── */

function useCopyToClipboard() {
  const [copied, setCopied] = useState<string | null>(null)
  const copy = useCallback((text: string, key?: string) => {
    navigator.clipboard.writeText(text).catch(() => {})
    setCopied(key ?? text)
    setTimeout(() => setCopied(null), 1500)
  }, [])
  return { copied, copy }
}

/* ── Base styles ── */

const cardStyle: CSSProperties = {
  background: "linear-gradient(135deg, rgba(30, 31, 35, 0.95), rgba(40, 41, 46, 0.95))",
  border: "1px solid rgba(255, 255, 255, 0.08)",
  borderRadius: 12, padding: 16, marginTop: 8, marginBottom: 8,
  maxWidth: 560, fontSize: 13, color: "#dcddde", fontFamily: "inherit",
}

const headerStyle: CSSProperties = {
  display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12,
}

const titleStyle: CSSProperties = { fontSize: 15, fontWeight: 600, color: "#ffffff", margin: 0 }

/* ── Small reusable pieces ── */

function StatusBadge({ status }: { status: string }) {
  const c = getStatusColor(status)
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 4, padding: "2px 8px",
      borderRadius: 12, fontSize: 11, fontWeight: 600, backgroundColor: `${c}22`,
      color: c, border: `1px solid ${c}44`, textTransform: "uppercase", letterSpacing: 0.5, flexShrink: 0,
    }}>
      <StatusIcon status={status} size={11} /> {status}
    </span>
  )
}

function Meta({ children }: { children: ReactNode }) {
  return <span style={{ display: "inline-flex", alignItems: "center", gap: 4, fontSize: 12, color: "#99aab5" }}>{children}</span>
}

function ActionBtn({
  label, icon, onClick, color = "#0078d4", small = false, disabled = false,
}: {
  label: string; icon?: ReactNode; onClick: () => void; color?: string; small?: boolean; disabled?: boolean
}) {
  const [hov, setHov] = useState(false)
  return (
    <button
      disabled={disabled}
      onClick={(e) => { e.stopPropagation(); onClick() }}
      onMouseEnter={() => setHov(true)}
      onMouseLeave={() => setHov(false)}
      style={{
        display: "inline-flex", alignItems: "center", gap: 4,
        padding: small ? "2px 8px" : "4px 10px", borderRadius: 6, fontSize: small ? 10 : 11,
        fontWeight: 600, border: `1px solid ${color}55`, color,
        backgroundColor: hov && !disabled ? `${color}22` : "transparent",
        cursor: disabled ? "default" : "pointer", opacity: disabled ? 0.5 : 1,
        transition: "background-color 0.15s, opacity 0.15s", whiteSpace: "nowrap",
      }}
    >
      {icon} {label}
    </button>
  )
}

function CopyBtn({ text, label, id }: { text: string; label?: string; id: string }) {
  const { copied, copy } = useCopyToClipboard()
  return (
    <ActionBtn
      label={copied === id ? "Copied!" : label ?? "Copy"}
      icon={copied === id ? <Check size={10} /> : <Copy size={10} />}
      onClick={() => copy(text, id)}
      color={copied === id ? "#43b581" : "#99aab5"}
      small
    />
  )
}

function ExpandToggle({ expanded }: { expanded: boolean }) {
  return expanded
    ? <ChevronDown size={14} color="#99aab5" style={{ transition: "transform 0.15s", flexShrink: 0 }} />
    : <ChevronRight size={14} color="#99aab5" style={{ transition: "transform 0.15s", flexShrink: 0 }} />
}

function LoadingCard({ label }: { label: string }) {
  return (
    <div style={{ ...cardStyle, display: "flex", alignItems: "center", gap: 10 }}>
      <Loader2 size={16} color="#0078d4" style={{ animation: "spin 1s linear infinite" }} />
      <span style={{ color: "#99aab5", fontSize: 13 }}>{label}</span>
    </div>
  )
}

function DetailPanel({ children }: { children: ReactNode }) {
  return (
    <div style={{
      marginTop: 8, padding: "8px 10px", borderRadius: 6,
      backgroundColor: "rgba(0,0,0,0.25)", border: "1px solid rgba(255,255,255,0.06)",
      fontSize: 12, color: "#b9bbbe",
    }}>
      {children}
    </div>
  )
}

function TabBar({ tabs, active, onChange }: { tabs: string[]; active: string; onChange: (t: string) => void }) {
  return (
    <div style={{ display: "flex", gap: 2, marginBottom: 10, padding: 2, borderRadius: 8, backgroundColor: "rgba(0,0,0,0.2)" }}>
      {tabs.map((t) => (
        <button
          key={t}
          onClick={() => onChange(t)}
          style={{
            flex: 1, padding: "4px 8px", borderRadius: 6, border: "none", fontSize: 11,
            fontWeight: 600, cursor: "pointer", transition: "all 0.15s",
            backgroundColor: active === t ? "rgba(255,255,255,0.1)" : "transparent",
            color: active === t ? "#ffffff" : "#99aab5",
          }}
        >
          {t}
        </button>
      ))}
    </div>
  )
}

/* ═══════════════════════════════════════════════
   1. Pipeline Status  ─  Interactive
   ═══════════════════════════════════════════════ */

interface PipelineStage { name: string; status: string }

function PipelineStatusCard({
  pipelineName, status, stages, branch, triggeredBy, duration,
}: {
  pipelineName: string; status: string; stages?: PipelineStage[]
  branch?: string; triggeredBy?: string; duration?: string
}) {
  const followUp = useFollowUp()
  const [selectedStage, setSelectedStage] = useState<number | null>(null)
  const [hovStage, setHovStage] = useState<number | null>(null)

  return (
    <div style={cardStyle}>
      <div style={headerStyle}>
        <h3 style={titleStyle}>{pipelineName}</h3>
        <StatusBadge status={status} />
      </div>

      {/* Meta */}
      <div style={{ display: "flex", gap: 16, marginBottom: 12, flexWrap: "wrap" }}>
        {branch && <Meta><GitBranch size={12} /> {branch}</Meta>}
        {triggeredBy && <Meta><User size={12} /> {triggeredBy}</Meta>}
        {duration && <Meta><Clock size={12} /> {duration}</Meta>}
      </div>

      {/* Stages – clickable */}
      {stages && stages.length > 0 && (
        <div style={{ display: "flex", alignItems: "center", gap: 4, flexWrap: "wrap" }}>
          {stages.map((stage, i) => {
            const sc = getStatusColor(stage.status)
            const selected = selectedStage === i
            const hovered = hovStage === i
            return (
              <div key={i} style={{ display: "flex", alignItems: "center", gap: 4 }}>
                <button
                  onClick={() => setSelectedStage(selected ? null : i)}
                  onMouseEnter={() => setHovStage(i)}
                  onMouseLeave={() => setHovStage(null)}
                  style={{
                    display: "flex", alignItems: "center", gap: 6,
                    padding: "6px 10px", borderRadius: 8, border: `1px solid ${sc}${selected ? "88" : "33"}`,
                    backgroundColor: `${sc}${selected ? "30" : hovered ? "22" : "15"}`,
                    cursor: "pointer", transition: "all 0.15s",
                    transform: hovered ? "translateY(-1px)" : "none",
                    boxShadow: selected ? `0 0 8px ${sc}33` : "none",
                  }}
                >
                  <StatusIcon status={stage.status} size={12} />
                  <span style={{ fontSize: 12, color: "#dcddde" }}>{stage.name}</span>
                </button>
                {i < stages.length - 1 && <ArrowRight size={12} color="#747f8d" />}
              </div>
            )
          })}
        </div>
      )}

      {/* Selected stage detail panel */}
      {selectedStage !== null && stages && stages[selectedStage] && (
        <DetailPanel>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 6 }}>
            <span style={{ fontWeight: 600, color: "#ffffff" }}>
              {stages[selectedStage].name}
            </span>
            <StatusBadge status={stages[selectedStage].status} />
          </div>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            <ActionBtn
              label="View Logs"
              icon={<FileText size={11} />}
              onClick={() => followUp(`Show me the logs for the "${stages[selectedStage].name}" stage of pipeline "${pipelineName}".`)}
            />
            {isFailed(stages[selectedStage].status) && (
              <ActionBtn
                label="Retry Stage"
                icon={<RotateCcw size={11} />}
                onClick={() => followUp(`Retry the "${stages[selectedStage].name}" stage of pipeline "${pipelineName}".`)}
                color="#faa61a"
              />
            )}
            <CopyBtn text={stages[selectedStage].name} label="Copy Name" id={`stage-${selectedStage}`} />
          </div>
        </DetailPanel>
      )}

      {/* Global pipeline actions */}
      <div style={{ display: "flex", gap: 6, marginTop: 10, flexWrap: "wrap" }}>
        {isFailed(status) && (
          <ActionBtn
            label="Retry Pipeline"
            icon={<RotateCcw size={11} />}
            onClick={() => followUp(`Retry the failed pipeline "${pipelineName}"${branch ? ` on branch ${branch}` : ""}.`)}
            color="#faa61a"
          />
        )}
        <ActionBtn
          label="Full Details"
          icon={<Search size={11} />}
          onClick={() => followUp(`Give me full details about pipeline "${pipelineName}" including all stage durations and any errors.`)}
        />
      </div>
    </div>
  )
}

/* ═══════════════════════════════════════════════
   2. Work Items  ─  Interactive
   ═══════════════════════════════════════════════ */

const priorityColors: Record<string, string> = {
  critical: "#f04747", high: "#f04747", "1": "#f04747",
  medium: "#faa61a", "2": "#faa61a",
  low: "#43b581", "3": "#43b581", "4": "#99aab5",
}

const typeEmojis: Record<string, string> = {
  bug: "\u{1F41B}", task: "\u{1F4CB}", story: "\u{1F4D6}",
  "user story": "\u{1F4D6}", feature: "\u{2728}", epic: "\u{1F3D4}\u{FE0F}", issue: "\u{1F527}",
}

interface WorkItem {
  id: string; title: string; type?: string; status: string; priority?: string; assignedTo?: string
}

function WorkItemsCard({ title, items }: { title: string; items: WorkItem[] }) {
  const followUp = useFollowUp()
  const [expanded, setExpanded] = useState<string | null>(null)
  const [filter, setFilter] = useState("All")
  const [hovItem, setHovItem] = useState<string | null>(null)

  const statusGroups = ["All", "Active", "Done", "New"]
  const filtered = filter === "All"
    ? items
    : items.filter((it) => {
        const s = it.status.toLowerCase()
        if (filter === "Active") return isRunning(s) || s === "active" || s === "in progress"
        if (filter === "Done") return isSuccess(s)
        if (filter === "New") return isPending(s)
        return true
      })

  return (
    <div style={cardStyle}>
      <div style={headerStyle}>
        <h3 style={titleStyle}>{title}</h3>
        <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <Filter size={12} color="#99aab5" />
          <span style={{ fontSize: 12, color: "#99aab5" }}>
            {filtered.length}/{items.length}
          </span>
        </div>
      </div>

      <TabBar tabs={statusGroups} active={filter} onChange={setFilter} />

      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
        {filtered.length === 0 && (
          <div style={{ padding: 16, textAlign: "center", color: "#747f8d", fontSize: 12 }}>
            No items match this filter.
          </div>
        )}
        {filtered.map((item) => {
          const isOpen = expanded === item.id
          const hovered = hovItem === item.id
          return (
            <div key={item.id}>
              <div
                onClick={() => setExpanded(isOpen ? null : item.id)}
                onMouseEnter={() => setHovItem(item.id)}
                onMouseLeave={() => setHovItem(null)}
                style={{
                  display: "flex", alignItems: "center", gap: 10,
                  padding: "8px 10px", borderRadius: 8, cursor: "pointer",
                  backgroundColor: isOpen ? "rgba(255,255,255,0.06)" : hovered ? "rgba(255,255,255,0.04)" : "rgba(255,255,255,0.02)",
                  border: `1px solid ${isOpen ? "rgba(255,255,255,0.1)" : "rgba(255,255,255,0.04)"}`,
                  transition: "all 0.15s",
                }}
              >
                <ExpandToggle expanded={isOpen} />
                {/* Priority bar */}
                {item.priority && (
                  <div style={{
                    width: 3, height: 24, borderRadius: 2, flexShrink: 0,
                    backgroundColor: priorityColors[item.priority.toLowerCase()] ?? "#99aab5",
                  }} />
                )}
                {/* Type emoji */}
                {item.type && (
                  <span style={{ fontSize: 14, flexShrink: 0 }}>
                    {typeEmojis[item.type.toLowerCase()] ?? "\u{1F4CB}"}
                  </span>
                )}
                {/* Content */}
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <span style={{ fontSize: 11, color: "#99aab5", flexShrink: 0 }}>{item.id}</span>
                    <span style={{ fontSize: 13, color: "#ffffff", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {item.title}
                    </span>
                  </div>
                </div>
                <StatusBadge status={item.status} />
              </div>

              {/* Expanded detail panel */}
              {isOpen && (
                <DetailPanel>
                  <div style={{ display: "flex", gap: 16, flexWrap: "wrap", marginBottom: 8 }}>
                    {item.type && <Meta>{typeEmojis[item.type.toLowerCase()] ?? ""} {item.type}</Meta>}
                    {item.priority && (
                      <Meta>
                        <span style={{
                          display: "inline-block", width: 8, height: 8, borderRadius: "50%",
                          backgroundColor: priorityColors[item.priority.toLowerCase()] ?? "#99aab5",
                        }} />
                        {item.priority} priority
                      </Meta>
                    )}
                    {item.assignedTo && <Meta><User size={11} /> {item.assignedTo}</Meta>}
                  </div>
                  <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                    <ActionBtn
                      label="Ask About This"
                      icon={<Search size={11} />}
                      onClick={() => followUp(`Tell me more about work item ${item.id}: "${item.title}". What's the current status and what needs to happen next?`)}
                    />
                    {!isSuccess(item.status) && (
                      <ActionBtn
                        label="Mark Done"
                        icon={<CheckCircle2 size={11} />}
                        onClick={() => followUp(`Mark work item ${item.id} ("${item.title}") as done.`)}
                        color="#43b581"
                      />
                    )}
                    <CopyBtn text={`${item.id} - ${item.title}`} label="Copy" id={`wi-${item.id}`} />
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

/* ═══════════════════════════════════════════════
   3. Azure Resource Info  ─  Interactive
   ═══════════════════════════════════════════════ */

interface AzureResource {
  name: string; type: string; status: string; region?: string; resourceGroup?: string; details?: string
}

function ResourceInfoCard({ resources }: { resources: AzureResource[] }) {
  const followUp = useFollowUp()
  const [expanded, setExpanded] = useState<number | null>(null)
  const [hovRes, setHovRes] = useState<number | null>(null)

  return (
    <div style={cardStyle}>
      <div style={headerStyle}>
        <h3 style={titleStyle}>Azure Resources</h3>
        <span style={{ fontSize: 12, color: "#99aab5" }}>
          {resources.length} resource{resources.length !== 1 ? "s" : ""}
        </span>
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {resources.map((res, i) => {
          const isOpen = expanded === i
          const hovered = hovRes === i
          return (
            <div key={i}>
              <div
                onClick={() => setExpanded(isOpen ? null : i)}
                onMouseEnter={() => setHovRes(i)}
                onMouseLeave={() => setHovRes(null)}
                style={{
                  padding: "10px 12px", borderRadius: 8, cursor: "pointer",
                  backgroundColor: isOpen ? "rgba(255,255,255,0.06)" : hovered ? "rgba(255,255,255,0.04)" : "rgba(255,255,255,0.02)",
                  border: `1px solid ${isOpen ? "rgba(255,255,255,0.1)" : "rgba(255,255,255,0.04)"}`,
                  transition: "all 0.15s",
                }}
              >
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 4 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <ExpandToggle expanded={isOpen} />
                    <Server size={14} color="#0078d4" />
                    <span style={{ fontSize: 14, fontWeight: 600, color: "#ffffff" }}>{res.name}</span>
                  </div>
                  <StatusBadge status={res.status} />
                </div>
                <div style={{ display: "flex", gap: 16, flexWrap: "wrap", marginTop: 4, paddingLeft: 28 }}>
                  <Meta><Package size={11} /> {res.type}</Meta>
                  {res.region && <Meta><Globe size={11} /> {res.region}</Meta>}
                  {res.resourceGroup && <Meta><Tag size={11} /> {res.resourceGroup}</Meta>}
                </div>
              </div>

              {/* Expanded */}
              {isOpen && (
                <DetailPanel>
                  {res.details && (
                    <p style={{ fontSize: 12, color: "#b9bbbe", marginTop: 0, marginBottom: 8 }}>{res.details}</p>
                  )}
                  <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                    {isRunning(res.status) && (
                      <ActionBtn
                        label="Stop"
                        icon={<Square size={11} />}
                        onClick={() => followUp(`Stop the Azure resource "${res.name}" (${res.type}) in ${res.resourceGroup ?? "its resource group"}.`)}
                        color="#f04747"
                      />
                    )}
                    {(isFailed(res.status) || isPending(res.status)) && (
                      <ActionBtn
                        label="Start"
                        icon={<Play size={11} />}
                        onClick={() => followUp(`Start the Azure resource "${res.name}" (${res.type}).`)}
                        color="#43b581"
                      />
                    )}
                    <ActionBtn
                      label="Restart"
                      icon={<RotateCcw size={11} />}
                      onClick={() => followUp(`Restart the Azure resource "${res.name}" (${res.type}).`)}
                      color="#faa61a"
                    />
                    <ActionBtn
                      label="View Metrics"
                      icon={<Activity size={11} />}
                      onClick={() => followUp(`Show me the performance metrics for Azure resource "${res.name}" (${res.type}).`)}
                    />
                    <ActionBtn
                      label="Scale"
                      icon={<ArrowUpCircle size={11} />}
                      onClick={() => followUp(`What are the scaling options for "${res.name}" (${res.type}) and what do you recommend?`)}
                    />
                    <CopyBtn text={res.name} label="Copy Name" id={`res-${i}`} />
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

/* ═══════════════════════════════════════════════
   4. Deployment Status  ─  Interactive
   ═══════════════════════════════════════════════ */

interface DeploymentEnv {
  name: string; status: string; version?: string; lastDeployed?: string
}

function DeploymentCard({
  applicationName, environments,
}: {
  applicationName: string; environments: DeploymentEnv[]
}) {
  const followUp = useFollowUp()
  const [expanded, setExpanded] = useState<number | null>(null)
  const [hovEnv, setHovEnv] = useState<number | null>(null)
  const [confirmed, setConfirmed] = useState<Record<number, boolean>>({})

  const handleApprove = (env: DeploymentEnv, idx: number) => {
    setConfirmed((prev) => ({ ...prev, [idx]: true }))
    followUp(`Approve the deployment of "${applicationName}" to ${env.name}${env.version ? ` (version ${env.version})` : ""}.`)
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
                  display: "flex", alignItems: "center", gap: 10, padding: "8px 12px",
                  borderRadius: 8, cursor: "pointer", transition: "all 0.15s",
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
                    <span style={{ fontSize: 13, fontWeight: 600, color: "#ffffff" }}>{env.name}</span>
                    {env.version && (
                      <span style={{ fontSize: 11, color: "#99aab5", fontFamily: "monospace" }}>v{env.version}</span>
                    )}
                  </div>
                  {env.lastDeployed && <span style={{ fontSize: 11, color: "#747f8d" }}>{env.lastDeployed}</span>}
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
                      <span style={{ fontSize: 11, color: "#43b581", display: "flex", alignItems: "center", gap: 4 }}>
                        <CheckCircle2 size={12} /> Approval requested
                      </span>
                    )}
                    {isSuccess(env.status) && (
                      <ActionBtn
                        label="Rollback"
                        icon={<RotateCcw size={11} />}
                        onClick={() => followUp(`Rollback "${applicationName}" in ${env.name} from version ${env.version ?? "current"}.`)}
                        color="#f04747"
                      />
                    )}
                    {isFailed(env.status) && (
                      <ActionBtn
                        label="Retry Deploy"
                        icon={<RotateCcw size={11} />}
                        onClick={() => followUp(`Retry the failed deployment of "${applicationName}" to ${env.name}.`)}
                        color="#faa61a"
                      />
                    )}
                    <ActionBtn
                      label="View Logs"
                      icon={<FileText size={11} />}
                      onClick={() => followUp(`Show me the deployment logs for "${applicationName}" in ${env.name}.`)}
                    />
                    {env.version && (
                      <CopyBtn text={env.version} label="Copy Version" id={`dep-${i}`} />
                    )}
                  </div>
                </DetailPanel>
              )}

              {/* Connector line */}
              {i < environments.length - 1 && (
                <div style={{ display: "flex", justifyContent: "center", padding: "2px 0" }}>
                  <div style={{ width: 1, height: 8, backgroundColor: "#747f8d" }} />
                </div>
              )}
            </div>
          )
        })}
      </div>

      {/* Global deployment actions */}
      <div style={{ display: "flex", gap: 6, marginTop: 10, flexWrap: "wrap" }}>
        <ActionBtn
          label="Deployment History"
          icon={<Clock size={11} />}
          onClick={() => followUp(`Show me the full deployment history for "${applicationName}" across all environments.`)}
        />
      </div>
    </div>
  )
}

/* ═══════════════════════════════════════════════
   5. Metrics Card  ─  Interactive
   ═══════════════════════════════════════════════ */

interface Metric { label: string; value: string; unit?: string; trend?: string }

function TrendIcon({ trend }: { trend?: string }) {
  if (!trend) return null
  const t = trend.toLowerCase()
  if (t === "up") return <TrendingUp size={12} color="#43b581" />
  if (t === "down") return <TrendingDown size={12} color="#f04747" />
  return <Minus size={12} color="#99aab5" />
}

function MetricsCard({ title, metrics }: { title: string; metrics: Metric[] }) {
  const followUp = useFollowUp()
  const [selected, setSelected] = useState<number | null>(null)
  const [hovMetric, setHovMetric] = useState<number | null>(null)

  return (
    <div style={cardStyle}>
      <div style={headerStyle}>
        <h3 style={{ ...titleStyle, display: "flex", alignItems: "center", gap: 6 }}>
          <Activity size={16} color="#0078d4" />
          {title}
        </h3>
      </div>
      <div style={{
        display: "grid",
        gridTemplateColumns: `repeat(${Math.min(metrics.length, 3)}, 1fr)`,
        gap: 8,
      }}>
        {metrics.map((m, i) => {
          const isSel = selected === i
          const isHov = hovMetric === i
          return (
            <div
              key={i}
              onClick={() => setSelected(isSel ? null : i)}
              onMouseEnter={() => setHovMetric(i)}
              onMouseLeave={() => setHovMetric(null)}
              style={{
                padding: "10px 12px", borderRadius: 8, textAlign: "center", cursor: "pointer",
                backgroundColor: isSel ? "rgba(0,120,212,0.15)" : isHov ? "rgba(255,255,255,0.06)" : "rgba(255,255,255,0.03)",
                border: `1px solid ${isSel ? "rgba(0,120,212,0.4)" : "rgba(255,255,255,0.05)"}`,
                transition: "all 0.15s",
                transform: isHov ? "translateY(-1px)" : "none",
                boxShadow: isSel ? "0 2px 8px rgba(0,120,212,0.2)" : "none",
              }}
            >
              <div style={{
                fontSize: 22, fontWeight: 700, color: "#ffffff",
                display: "flex", alignItems: "center", justifyContent: "center", gap: 4,
              }}>
                {m.value}
                {m.unit && <span style={{ fontSize: 12, fontWeight: 400, color: "#99aab5" }}>{m.unit}</span>}
                <TrendIcon trend={m.trend} />
              </div>
              <div style={{ fontSize: 11, color: "#99aab5", marginTop: 2 }}>{m.label}</div>
            </div>
          )
        })}
      </div>

      {/* Selected metric detail actions */}
      {selected !== null && metrics[selected] && (
        <DetailPanel>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 6 }}>
            <span style={{ fontWeight: 600, color: "#ffffff" }}>
              {metrics[selected].label}: {metrics[selected].value}{metrics[selected].unit ?? ""}
            </span>
            {metrics[selected].trend && (
              <Meta>
                <TrendIcon trend={metrics[selected].trend} />
                Trending {metrics[selected].trend}
              </Meta>
            )}
          </div>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            <ActionBtn
              label="Analyze"
              icon={<Search size={11} />}
              onClick={() => followUp(`Analyze the "${metrics[selected].label}" metric (currently ${metrics[selected].value}${metrics[selected].unit ?? ""}${metrics[selected].trend ? `, trending ${metrics[selected].trend}` : ""}). What's causing this trend and what should we do?`)}
            />
            <ActionBtn
              label="Compare"
              icon={<Activity size={11} />}
              onClick={() => followUp(`Compare "${metrics[selected].label}" against historical data and benchmarks. How does ${metrics[selected].value}${metrics[selected].unit ?? ""} compare?`)}
            />
            <CopyBtn
              text={`${metrics[selected].label}: ${metrics[selected].value}${metrics[selected].unit ?? ""}`}
              label="Copy"
              id={`metric-${selected}`}
            />
          </div>
        </DetailPanel>
      )}
    </div>
  )
}

/* ═══════════════════════════════════════════════
   Action Registrations
   ═══════════════════════════════════════════════ */

export function CopilotActions() {
  /* 1 ─ Pipeline Status */
  useCopilotAction({
    name: "showPipelineStatus",
    description:
      "Display an interactive CI/CD pipeline visualization with clickable stages. " +
      "Use this when discussing pipeline runs, build status, or CI/CD workflows.",
    parameters: [
      { name: "pipelineName", type: "string", description: "Name of the pipeline", required: true },
      { name: "status", type: "string", description: "Overall status: succeeded, running, failed, cancelled, pending", required: true },
      { name: "stages", type: "object[]", description: "Pipeline stages in order", required: false, attributes: [
        { name: "name", type: "string", description: "Stage name" },
        { name: "status", type: "string", description: "Stage status: succeeded, running, failed, pending, skipped" },
      ]},
      { name: "branch", type: "string", description: "Git branch name", required: false },
      { name: "triggeredBy", type: "string", description: "Who or what triggered the pipeline", required: false },
      { name: "duration", type: "string", description: "Pipeline duration (e.g. '3m 42s')", required: false },
    ],
    render: ({ args, status }) => {
      if (status !== "complete") return <LoadingCard label="Loading pipeline status..." />
      return <PipelineStatusCard
        pipelineName={args.pipelineName as string} status={args.status as string}
        stages={args.stages as PipelineStage[] | undefined}
        branch={args.branch as string | undefined}
        triggeredBy={args.triggeredBy as string | undefined}
        duration={args.duration as string | undefined}
      />
    },
    handler: async (args) => `Pipeline "${args.pipelineName}" displayed. User can click stages for details, retry failed stages, or request logs.`,
  })

  /* 2 ─ Work Items */
  useCopilotAction({
    name: "showWorkItems",
    description:
      "Display an interactive list of work items with filters and expandable details. " +
      "Use this when discussing sprint items, backlogs, task assignments, or boards.",
    parameters: [
      { name: "title", type: "string", description: "Title for the work items list", required: true },
      { name: "items", type: "object[]", description: "Work items to display", required: true, attributes: [
        { name: "id", type: "string", description: "Work item ID (e.g. '#1234')" },
        { name: "title", type: "string", description: "Work item title" },
        { name: "type", type: "string", description: "Item type: bug, task, story, feature, epic" },
        { name: "status", type: "string", description: "Status: new, active, in progress, done, closed" },
        { name: "priority", type: "string", description: "Priority: critical, high, medium, low" },
        { name: "assignedTo", type: "string", description: "Person assigned to the item" },
      ]},
    ],
    render: ({ args, status }) => {
      if (status !== "complete") return <LoadingCard label="Loading work items..." />
      return <WorkItemsCard title={args.title as string} items={args.items as WorkItem[]} />
    },
    handler: async (args) => `${(args.items as WorkItem[])?.length ?? 0} work items displayed. User can filter by status, expand items for details, and take actions.`,
  })

  /* 3 ─ Azure Resources */
  useCopilotAction({
    name: "showResourceInfo",
    description:
      "Display interactive Azure resource cards with action buttons. " +
      "Use this when discussing Azure resources, their status, or troubleshooting.",
    parameters: [
      { name: "resources", type: "object[]", description: "Azure resources to display", required: true, attributes: [
        { name: "name", type: "string", description: "Resource name" },
        { name: "type", type: "string", description: "Resource type (e.g. App Service, Function App, AKS Cluster)" },
        { name: "status", type: "string", description: "Resource status: running, stopped, error, healthy" },
        { name: "region", type: "string", description: "Azure region" },
        { name: "resourceGroup", type: "string", description: "Resource group name" },
        { name: "details", type: "string", description: "Additional details or notes" },
      ]},
    ],
    render: ({ args, status }) => {
      if (status !== "complete") return <LoadingCard label="Loading resources..." />
      return <ResourceInfoCard resources={args.resources as AzureResource[]} />
    },
    handler: async (args) => `${(args.resources as AzureResource[])?.length ?? 0} resources displayed. User can expand for details and take actions like restart, stop, scale.`,
  })

  /* 4 ─ Deployment */
  useCopilotAction({
    name: "showDeployment",
    description:
      "Display interactive deployment status with approve/rollback actions. " +
      "Use this when discussing deployments, releases, or environment promotion.",
    parameters: [
      { name: "applicationName", type: "string", description: "Application being deployed", required: true },
      { name: "environments", type: "object[]", description: "Deployment environments in promotion order", required: true, attributes: [
        { name: "name", type: "string", description: "Environment name (e.g. Development, Staging, Production)" },
        { name: "status", type: "string", description: "Deployment status: succeeded, running, failed, pending" },
        { name: "version", type: "string", description: "Deployed version (e.g. '1.2.3')" },
        { name: "lastDeployed", type: "string", description: "Last deployment time" },
      ]},
    ],
    render: ({ args, status }) => {
      if (status !== "complete") return <LoadingCard label="Loading deployment status..." />
      return <DeploymentCard applicationName={args.applicationName as string} environments={args.environments as DeploymentEnv[]} />
    },
    handler: async (args) => `Deployment status for "${args.applicationName}" displayed. User can approve pending deployments, rollback, or retry failed ones.`,
  })

  /* 5 ─ Metrics */
  useCopilotAction({
    name: "showMetrics",
    description:
      "Display interactive metrics/KPI cards that users can click to analyze. " +
      "Use this when presenting numbers, statistics, performance data, or summaries.",
    parameters: [
      { name: "title", type: "string", description: "Title for the metrics card", required: true },
      { name: "metrics", type: "object[]", description: "Metrics to display (up to 6)", required: true, attributes: [
        { name: "label", type: "string", description: "Metric label" },
        { name: "value", type: "string", description: "Metric value (formatted as string)" },
        { name: "unit", type: "string", description: "Unit (e.g. '%', 'ms', 'GB')" },
        { name: "trend", type: "string", description: "Trend direction: up, down, stable" },
      ]},
    ],
    render: ({ args, status }) => {
      if (status !== "complete") return <LoadingCard label="Loading metrics..." />
      return <MetricsCard title={args.title as string} metrics={args.metrics as Metric[]} />
    },
    handler: async (args) => `Metrics "${args.title}" displayed. User can click individual metrics to analyze or compare them.`,
  })

  return null
}
