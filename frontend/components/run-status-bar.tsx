"use client"

import { useState } from "react"
import { ChevronDown, ChevronUp, Activity, CheckCircle2, AlertTriangle, Clock, Shield } from "lucide-react"

export type RunPhase =
    | "idle"
    | "triaging"
    | "investigating"
    | "executing-tools"
    | "waiting-approval"
    | "implementing"
    | "resolved"
    | "error"

export interface ToolCallInfo {
    id: string
    name: string
    agentName: string
    status: "running" | "completed" | "error"
    timestamp: Date
}

interface RunStatusBarProps {
    phase: RunPhase
    activeAgentName: string | null
    toolCalls: ToolCallInfo[]
    elapsedSeconds: number
}

const phaseConfig: Record<RunPhase, { label: string; color: string; icon: React.ReactNode }> = {
    idle: { label: "Ready", color: "hsl(214, 5%, 55%)", icon: <Clock className="h-3.5 w-3.5" /> },
    triaging: { label: "Triaging", color: "hsl(45, 93%, 47%)", icon: <Activity className="h-3.5 w-3.5" /> },
    investigating: { label: "Investigating", color: "hsl(200, 100%, 50%)", icon: <Activity className="h-3.5 w-3.5" /> },
    "executing-tools": { label: "Executing tools", color: "hsl(280, 70%, 60%)", icon: <Activity className="h-3.5 w-3.5 animate-pulse" /> },
    "waiting-approval": { label: "Waiting for approval", color: "hsl(25, 95%, 53%)", icon: <Shield className="h-3.5 w-3.5" /> },
    implementing: { label: "Implementing fix", color: "hsl(170, 70%, 45%)", icon: <Activity className="h-3.5 w-3.5" /> },
    resolved: { label: "Resolved", color: "hsl(142, 70%, 45%)", icon: <CheckCircle2 className="h-3.5 w-3.5" /> },
    error: { label: "Error", color: "hsl(0, 72%, 51%)", icon: <AlertTriangle className="h-3.5 w-3.5" /> },
}

export function RunStatusBar({ phase, activeAgentName, toolCalls, elapsedSeconds }: RunStatusBarProps) {
    const [expanded, setExpanded] = useState(false)

    if (phase === "idle") return null

    const config = phaseConfig[phase]
    const completedTools = toolCalls.filter((t) => t.status === "completed").length
    const runningTools = toolCalls.filter((t) => t.status === "running").length

    const formatElapsed = (s: number) => {
        if (s < 60) return `${s}s`
        return `${Math.floor(s / 60)}m ${s % 60}s`
    }

    return (
        <div
            className="mx-4 mt-2 rounded-lg border text-sm"
            style={{
                backgroundColor: "hsl(228, 7%, 16%)",
                borderColor: config.color + "40",
            }}
        >
            {/* Main status row */}
            <div
                className="flex items-center justify-between px-3 py-2 cursor-pointer"
                onClick={() => toolCalls.length > 0 && setExpanded((e) => !e)}
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
                    {toolCalls.length > 0 && (
                        <span className="text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
                            {runningTools > 0 && `${runningTools} running · `}
                            {completedTools} tool call{completedTools !== 1 ? "s" : ""}
                        </span>
                    )}
                    <span className="text-xs tabular-nums" style={{ color: "hsl(214, 5%, 45%)" }}>
                        {formatElapsed(elapsedSeconds)}
                    </span>
                    {toolCalls.length > 0 && (
                        <span style={{ color: "hsl(214, 5%, 55%)" }}>
                            {expanded ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
                        </span>
                    )}
                </div>
            </div>

            {/* Expandable tool trace */}
            {expanded && toolCalls.length > 0 && (
                <div
                    className="border-t px-3 py-2 space-y-1"
                    style={{ borderColor: "hsl(228, 6%, 20%)" }}
                >
                    <div className="text-xs font-medium mb-1" style={{ color: "hsl(214, 5%, 55%)" }}>
                        Tool Execution Trace
                    </div>
                    {toolCalls.map((tc, idx) => (
                        <div key={`${tc.id}-${idx}`} className="flex items-center gap-2 text-xs">
                            <span
                                className="h-1.5 w-1.5 rounded-full"
                                style={{
                                    backgroundColor:
                                        tc.status === "running"
                                            ? "hsl(45, 93%, 47%)"
                                            : tc.status === "completed"
                                                ? "hsl(142, 70%, 45%)"
                                                : "hsl(0, 72%, 51%)",
                                }}
                            />
                            <span style={{ color: "hsl(210, 3%, 70%)" }} className="font-mono">
                                {tc.name}
                            </span>
                            <span style={{ color: "hsl(214, 5%, 45%)" }}>by {tc.agentName}</span>
                        </div>
                    ))}
                </div>
            )}
        </div>
    )
}
