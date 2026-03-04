"use client"

import { useState, useEffect, useRef } from "react"
import {
    ChevronDown,
    ChevronUp,
    Activity,
    CheckCircle2,
    AlertTriangle,
    Clock,
    Loader2,
    ArrowRight,
    Zap,
    MessageSquare
} from "lucide-react"

export type ActivityPhase =
    | "idle"
    | "triaging"
    | "investigating"
    | "executing-tools"
    | "waiting-approval"
    | "implementing"
    | "resolved"
    | "error"

export interface ActivityLogEntry {
    id: string
    timestamp: Date
    type: "phase-change" | "agent-switch" | "tool-call" | "message-start" | "message-chunk" | "message-end" | "error"
    phase?: ActivityPhase
    agentName?: string | null
    toolName?: string
    toolStatus?: "running" | "completed" | "error"
    message?: string
    error?: string
}

interface AgentActivityLogProps {
    phase: ActivityPhase
    activeAgentName: string | null
    entries: ActivityLogEntry[]
    elapsedSeconds: number
    defaultExpanded?: boolean
}

const phaseConfig: Record<ActivityPhase, { label: string; color: string; icon: React.ReactNode }> = {
    idle: { label: "Ready", color: "hsl(214, 5%, 55%)", icon: <Clock className="h-3.5 w-3.5" /> },
    triaging: { label: "Thinking", color: "hsl(45, 93%, 47%)", icon: <Activity className="h-3.5 w-3.5 animate-pulse" /> },
    investigating: { label: "Investigating", color: "hsl(200, 100%, 50%)", icon: <Activity className="h-3.5 w-3.5 animate-pulse" /> },
    "executing-tools": { label: "Executing tools", color: "hsl(280, 70%, 60%)", icon: <Zap className="h-3.5 w-3.5 animate-pulse" /> },
    "waiting-approval": { label: "Waiting for approval", color: "hsl(25, 95%, 53%)", icon: <Clock className="h-3.5 w-3.5" /> },
    implementing: { label: "Implementing", color: "hsl(170, 70%, 45%)", icon: <Activity className="h-3.5 w-3.5 animate-pulse" /> },
    resolved: { label: "Completed", color: "hsl(142, 70%, 45%)", icon: <CheckCircle2 className="h-3.5 w-3.5" /> },
    error: { label: "Error", color: "hsl(0, 72%, 51%)", icon: <AlertTriangle className="h-3.5 w-3.5" /> },
}

const entryTypeConfig = {
    "phase-change": { icon: <Activity className="h-3 w-3" />, color: "hsl(200, 100%, 50%)" },
    "agent-switch": { icon: <ArrowRight className="h-3 w-3" />, color: "hsl(280, 70%, 60%)" },
    "tool-call": { icon: <Zap className="h-3 w-3" />, color: "hsl(45, 93%, 47%)" },
    "message-start": { icon: <MessageSquare className="h-3 w-3" />, color: "hsl(210, 3%, 70%)" },
    "message-chunk": { icon: <Loader2 className="h-2.5 w-2.5" />, color: "hsl(214, 5%, 55%)" },
    "message-end": { icon: <CheckCircle2 className="h-3 w-3" />, color: "hsl(142, 70%, 45%)" },
    "error": { icon: <AlertTriangle className="h-3 w-3" />, color: "hsl(0, 72%, 51%)" },
}

export function AgentActivityLog({
    phase,
    activeAgentName,
    entries,
    elapsedSeconds,
    defaultExpanded = false,
}: AgentActivityLogProps) {
    const [expanded, setExpanded] = useState(defaultExpanded)
    const logEndRef = useRef<HTMLDivElement>(null)

    // Auto-scroll to bottom when new entries arrive (only if already expanded)
    useEffect(() => {
        if (expanded && logEndRef.current) {
            logEndRef.current.scrollIntoView({ behavior: "smooth" })
        }
    }, [entries.length, expanded])

    if (phase === "idle") return null

    const config = phaseConfig[phase]

    const formatElapsed = (s: number) => {
        if (s < 60) return `${s}s`
        const m = Math.floor(s / 60)
        const sec = s % 60
        return `${m}m ${sec}s`
    }

    const getEntryLabel = (entry: ActivityLogEntry): string => {
        switch (entry.type) {
            case "phase-change":
                return `Phase: ${phaseConfig[entry.phase ?? "idle"].label}`
            case "agent-switch":
                return `Agent: ${entry.agentName}`
            case "tool-call":
                const status = entry.toolStatus === "completed" ? "✓" : entry.toolStatus === "error" ? "✗" : "⏳"
                return `${status} Tool: ${entry.toolName}`
            case "message-start":
                return `${entry.agentName} started responding`
            case "message-chunk":
                return `${entry.agentName} typing...`
            case "message-end":
                return `${entry.agentName} finished`
            case "error":
                return `Error: ${entry.error ?? entry.message ?? "Unknown error"}`
            default:
                return entry.message ?? "Unknown event"
        }
    }

    const runningToolsCount = entries.filter(e => e.type === "tool-call" && e.toolStatus === "running").length
    const completedToolsCount = entries.filter(e => e.type === "tool-call" && e.toolStatus === "completed").length
    const errorCount = entries.filter(e => e.type === "error").length

    return (
        <div
            className="mx-4 mt-2 rounded-lg border text-sm"
            style={{
                backgroundColor: "hsl(228, 7%, 16%)",
                borderColor: config.color + "40",
            }}
        >
            {/* Collapsed header */}
            <div
                className="flex items-center justify-between px-3 py-2 cursor-pointer select-none"
                onClick={() => setExpanded(e => !e)}
            >
                <div className="flex items-center gap-2">
                    <span style={{ color: config.color }}>{config.icon}</span>
                    <span className="font-medium" style={{ color: config.color }}>
                        {config.label}
                    </span>
                    {activeAgentName && (
                        <>
                            <span style={{ color: "hsl(214, 5%, 45%)" }}>—</span>
                            <span style={{ color: "hsl(210, 3%, 83%)" }}>{activeAgentName}</span>
                        </>
                    )}
                </div>
                <div className="flex items-center gap-3">
                    {entries.length > 0 && (
                        <span className="text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
                            {runningToolsCount > 0 && `${runningToolsCount} running · `}
                            {completedToolsCount > 0 && `${completedToolsCount} tools · `}
                            {errorCount > 0 && `${errorCount} errors · `}
                            {entries.length} events
                        </span>
                    )}
                    <span className="text-xs tabular-nums" style={{ color: "hsl(214, 5%, 45%)" }}>
                        {formatElapsed(elapsedSeconds)}
                    </span>
                    <span style={{ color: "hsl(214, 5%, 55%)" }}>
                        {expanded ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
                    </span>
                </div>
            </div>

            {/* Expanded activity log */}
            {expanded && (
                <div
                    className="border-t max-h-80 overflow-y-auto"
                    style={{ borderColor: "hsl(228, 6%, 20%)" }}
                >
                    {entries.length === 0 ? (
                        <div className="px-3 py-4 text-center text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
                            No activity yet
                        </div>
                    ) : (
                        <div className="px-3 py-2 space-y-0.5">
                            {entries.map((entry, idx) => {
                                const typeConfig = entryTypeConfig[entry.type]
                                const timestamp = entry.timestamp.toLocaleTimeString([], {
                                    hour: "2-digit",
                                    minute: "2-digit",
                                    second: "2-digit"
                                })

                                return (
                                    <div
                                        key={entry.id}
                                        className="flex items-start gap-2 text-xs py-1 hover:bg-zinc-800/30 rounded px-2"
                                    >
                                        <span
                                            className="mt-0.5 flex-shrink-0"
                                            style={{ color: typeConfig.color }}
                                        >
                                            {typeConfig.icon}
                                        </span>
                                        <span
                                            className="text-[10px] tabular-nums flex-shrink-0"
                                            style={{ color: "hsl(214, 5%, 45%)" }}
                                        >
                                            {timestamp}
                                        </span>
                                        <span
                                            className="flex-1 break-words"
                                            style={{
                                                color: entry.type === "error" ? "hsl(0, 72%, 51%)" : "hsl(210, 3%, 83%)"
                                            }}
                                        >
                                            {getEntryLabel(entry)}
                                        </span>
                                    </div>
                                )
                            })}
                            <div ref={logEndRef} />
                        </div>
                    )}
                </div>
            )}
        </div>
    )
}
