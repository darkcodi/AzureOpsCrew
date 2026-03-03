"use client"

import { useState, useEffect } from "react"
import {
    ChevronRight,
    ChevronDown,
    CheckCircle2,
    XCircle,
    Clock,
    Loader2,
    AlertTriangle,
    Shield,
    SkipForward,
    Ban,
    Circle,
} from "lucide-react"

// ─── Types ───

export interface TaskNode {
    id: string
    parentTaskId: string | null
    title: string
    description: string | null
    taskType: string
    assignedAgent: string | null
    priority: number
    status: string
    goal: string | null
    resultSummary: string | null
    stepCount: number
    retryCount: number
    createdAt: string
    completedAt: string | null
}

export interface RunData {
    id: string
    channelId: string
    userRequest: string
    status: string
    goal: string | null
    initialPlan: string | null
    currentPlan: string | null
    planRevision: number
    totalSteps: number
    totalToolCalls: number
    totalReplans: number
    resultSummary: string | null
    errorMessage: string | null
    createdAt: string
    completedAt: string | null
    tasks: TaskNode[]
    pendingApprovals: string[]
}

// ─── Status helpers ───

const statusIcon: Record<string, React.ReactNode> = {
    Created: <Circle className="h-3.5 w-3.5 text-zinc-500" />,
    Ready: <Clock className="h-3.5 w-3.5 text-blue-400" />,
    Blocked: <Ban className="h-3.5 w-3.5 text-zinc-600" />,
    Running: <Loader2 className="h-3.5 w-3.5 text-yellow-400 animate-spin" />,
    WaitingForTool: <Loader2 className="h-3.5 w-3.5 text-purple-400 animate-pulse" />,
    WaitingForDependency: <Clock className="h-3.5 w-3.5 text-zinc-500" />,
    WaitingForApproval: <Shield className="h-3.5 w-3.5 text-orange-400" />,
    WaitingForUserInput: <AlertTriangle className="h-3.5 w-3.5 text-amber-400" />,
    Succeeded: <CheckCircle2 className="h-3.5 w-3.5 text-green-500" />,
    Failed: <XCircle className="h-3.5 w-3.5 text-red-500" />,
    Cancelled: <Ban className="h-3.5 w-3.5 text-zinc-600" />,
    Skipped: <SkipForward className="h-3.5 w-3.5 text-zinc-500" />,
}

const agentColor: Record<string, string> = {
    manager: "hsl(45, 93%, 47%)",
    devops: "hsl(280, 70%, 65%)",
    developer: "hsl(140, 70%, 55%)",
}

function getAgentColor(agent: string | null): string {
    if (!agent) return "hsl(214, 5%, 55%)"
    return agentColor[agent] ?? "hsl(210, 3%, 70%)"
}

// ─── Tree builder ───

interface TreeNode extends TaskNode {
    children: TreeNode[]
}

function buildTree(tasks: TaskNode[]): TreeNode[] {
    const map = new Map<string, TreeNode>()
    const roots: TreeNode[] = []

    for (const task of tasks) {
        map.set(task.id, { ...task, children: [] })
    }

    for (const task of tasks) {
        const node = map.get(task.id)!
        if (task.parentTaskId && map.has(task.parentTaskId)) {
            map.get(task.parentTaskId)!.children.push(node)
        } else {
            roots.push(node)
        }
    }

    return roots
}

// ─── TaskTreeNode ───

function TaskTreeNodeRow({ node, depth = 0 }: { node: TreeNode; depth?: number }) {
    const [expanded, setExpanded] = useState(true)
    const hasChildren = node.children.length > 0

    return (
        <div>
            <div
                className="flex items-center gap-1.5 py-1 px-2 hover:bg-zinc-800/50 rounded cursor-pointer text-xs"
                style={{ paddingLeft: `${depth * 16 + 8}px` }}
                onClick={() => hasChildren && setExpanded((e) => !e)}
            >
                {hasChildren ? (
                    <span className="text-zinc-500">
                        {expanded ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
                    </span>
                ) : (
                    <span className="w-3.5" />
                )}

                {statusIcon[node.status] ?? <Circle className="h-3.5 w-3.5 text-zinc-500" />}

                <span className="font-medium text-zinc-200 truncate flex-1">{node.title}</span>

                {node.assignedAgent && (
                    <span
                        className="text-[10px] font-mono px-1.5 py-0.5 rounded"
                        style={{
                            color: getAgentColor(node.assignedAgent),
                            backgroundColor: getAgentColor(node.assignedAgent) + "15",
                        }}
                    >
                        {node.assignedAgent}
                    </span>
                )}

                <span className="text-[10px] text-zinc-600 font-mono">{node.taskType}</span>
            </div>

            {/* Result summary */}
            {node.resultSummary && (node.status === "Succeeded" || node.status === "Failed") && (
                <div
                    className="text-[10px] text-zinc-500 truncate"
                    style={{ paddingLeft: `${depth * 16 + 36}px` }}
                >
                    {node.resultSummary}
                </div>
            )}

            {/* Children */}
            {expanded && hasChildren && (
                <div>
                    {node.children.map((child) => (
                        <TaskTreeNodeRow key={child.id} node={child} depth={depth + 1} />
                    ))}
                </div>
            )}
        </div>
    )
}

// ─── Main component ───

interface TaskTreePanelProps {
    runId: string | null
    run?: RunData | null
    onRefresh?: () => void
}

export function TaskTreePanel({ runId, run: externalRun, onRefresh }: TaskTreePanelProps) {
    const [run, setRun] = useState<RunData | null>(externalRun ?? null)
    const [loading, setLoading] = useState(false)

    useEffect(() => {
        if (externalRun) {
            setRun(externalRun)
            return
        }
        if (!runId) return

        const fetchRun = async () => {
            setLoading(true)
            try {
                const res = await fetch(`/api/runs/${runId}`)
                if (res.ok) {
                    const data = await res.json()
                    setRun(data)
                }
            } catch (err) {
                console.error("Failed to fetch run:", err)
            } finally {
                setLoading(false)
            }
        }

        fetchRun()
        // Poll while active
        const interval = setInterval(fetchRun, 3000)
        return () => clearInterval(interval)
    }, [runId, externalRun])

    if (!run && !loading) return null

    if (loading && !run) {
        return (
            <div className="flex items-center justify-center py-4 text-xs text-zinc-500">
                <Loader2 className="h-4 w-4 animate-spin mr-2" />
                Loading execution plan...
            </div>
        )
    }

    if (!run) return null

    const tree = buildTree(run.tasks)
    const isTerminal = ["Succeeded", "Failed", "BudgetExhausted", "Cancelled"].includes(run.status)

    const statusColor = run.status === "Succeeded"
        ? "hsl(142, 70%, 45%)"
        : run.status === "Failed"
            ? "hsl(0, 72%, 51%)"
            : run.status === "WaitingForApproval"
                ? "hsl(25, 95%, 53%)"
                : "hsl(200, 100%, 50%)"

    return (
        <div className="rounded-lg border text-sm" style={{ backgroundColor: "hsl(228, 7%, 14%)", borderColor: "hsl(228, 6%, 22%)" }}>
            {/* Header */}
            <div className="flex items-center justify-between px-3 py-2 border-b" style={{ borderColor: "hsl(228, 6%, 20%)" }}>
                <div className="flex items-center gap-2">
                    <span className="font-medium text-xs" style={{ color: statusColor }}>
                        {run.status}
                    </span>
                    <span className="text-[10px] text-zinc-500">
                        {run.totalSteps} steps · {run.totalToolCalls} tools · {run.totalReplans} replans
                    </span>
                </div>
                {!isTerminal && onRefresh && (
                    <button
                        onClick={onRefresh}
                        className="text-[10px] text-zinc-500 hover:text-zinc-300 transition-colors"
                    >
                        Refresh
                    </button>
                )}
            </div>

            {/* Task tree */}
            <div className="py-1 max-h-64 overflow-y-auto">
                {tree.length > 0 ? (
                    tree.map((node) => <TaskTreeNodeRow key={node.id} node={node} />)
                ) : (
                    <div className="px-3 py-2 text-xs text-zinc-500">
                        No tasks yet — plan will appear here
                    </div>
                )}
            </div>

            {/* Summary */}
            {run.resultSummary && isTerminal && (
                <div className="px-3 py-2 border-t text-xs text-zinc-400" style={{ borderColor: "hsl(228, 6%, 20%)" }}>
                    {run.resultSummary}
                </div>
            )}

            {/* Error */}
            {run.errorMessage && (
                <div className="px-3 py-2 border-t text-xs text-red-400" style={{ borderColor: "hsl(228, 6%, 20%)" }}>
                    {run.errorMessage}
                </div>
            )}
        </div>
    )
}
