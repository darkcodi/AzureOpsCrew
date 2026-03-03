"use client"

import { useState } from "react"
import { Shield, AlertTriangle, CheckCircle2, XCircle } from "lucide-react"

export interface ApprovalRequest {
    id: string
    actionType: string
    proposedAction: string
    target: string | null
    riskLevel: string
    rollbackPlan: string | null
    verificationPlan: string | null
    affectedResources: string | null
    status: string
    decisionReason: string | null
    requestedAt: string
    respondedAt: string | null
}

interface ApprovalDialogProps {
    runId: string
    approval: ApprovalRequest
    onDecision: (approved: boolean, reason?: string) => void
    isProcessing?: boolean
}

const riskColor: Record<string, string> = {
    Low: "hsl(142, 70%, 45%)",
    Medium: "hsl(45, 93%, 47%)",
    High: "hsl(25, 95%, 53%)",
    Critical: "hsl(0, 72%, 51%)",
}

export function ApprovalDialog({ runId, approval, onDecision, isProcessing }: ApprovalDialogProps) {
    const [reason, setReason] = useState("")
    const [decided, setDecided] = useState<"approved" | "denied" | null>(null)

    const color = riskColor[approval.riskLevel] ?? riskColor.Medium

    const handleApprove = async () => {
        setDecided("approved")
        onDecision(true, reason || undefined)
    }

    const handleDeny = async () => {
        setDecided("denied")
        onDecision(false, reason || undefined)
    }

    if (decided) {
        return (
            <div
                className="mx-4 my-2 rounded-lg border p-3 text-sm"
                style={{
                    backgroundColor: "hsl(228, 7%, 16%)",
                    borderColor: decided === "approved" ? "hsl(142, 70%, 45%)40" : "hsl(0, 72%, 51%)40",
                }}
            >
                <div className="flex items-center gap-2">
                    {decided === "approved" ? (
                        <CheckCircle2 className="h-4 w-4 text-green-500" />
                    ) : (
                        <XCircle className="h-4 w-4 text-red-500" />
                    )}
                    <span className="text-zinc-300">
                        {decided === "approved" ? "Approved" : "Denied"}: {approval.proposedAction}
                    </span>
                </div>
            </div>
        )
    }

    return (
        <div
            className="mx-4 my-2 rounded-lg border p-4 text-sm"
            style={{
                backgroundColor: "hsl(228, 7%, 16%)",
                borderColor: color + "60",
            }}
        >
            {/* Header */}
            <div className="flex items-center gap-2 mb-3">
                <Shield className="h-4 w-4" style={{ color }} />
                <span className="font-medium text-zinc-200">Approval Required</span>
                <span
                    className="text-[10px] font-medium px-1.5 py-0.5 rounded"
                    style={{ color, backgroundColor: color + "20" }}
                >
                    {approval.riskLevel} Risk
                </span>
            </div>

            {/* Action details */}
            <div className="space-y-2 mb-3">
                <div>
                    <span className="text-[10px] uppercase tracking-wider text-zinc-500">Action</span>
                    <p className="text-zinc-300 text-xs mt-0.5">{approval.proposedAction}</p>
                </div>

                {approval.target && (
                    <div>
                        <span className="text-[10px] uppercase tracking-wider text-zinc-500">Target</span>
                        <p className="text-zinc-300 text-xs mt-0.5 font-mono">{approval.target}</p>
                    </div>
                )}

                {approval.affectedResources && (
                    <div>
                        <span className="text-[10px] uppercase tracking-wider text-zinc-500">Affected Resources</span>
                        <p className="text-zinc-300 text-xs mt-0.5">{approval.affectedResources}</p>
                    </div>
                )}

                {approval.rollbackPlan && (
                    <div>
                        <span className="text-[10px] uppercase tracking-wider text-zinc-500">Rollback Plan</span>
                        <p className="text-zinc-400 text-xs mt-0.5">{approval.rollbackPlan}</p>
                    </div>
                )}

                {approval.verificationPlan && (
                    <div>
                        <span className="text-[10px] uppercase tracking-wider text-zinc-500">Verification Plan</span>
                        <p className="text-zinc-400 text-xs mt-0.5">{approval.verificationPlan}</p>
                    </div>
                )}
            </div>

            {/* Reason input */}
            <div className="mb-3">
                <textarea
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="Optional: add a note..."
                    className="w-full rounded border px-2 py-1.5 text-xs text-zinc-300 placeholder-zinc-600 resize-none"
                    style={{
                        backgroundColor: "hsl(228, 7%, 12%)",
                        borderColor: "hsl(228, 6%, 22%)",
                    }}
                    rows={2}
                />
            </div>

            {/* Action buttons */}
            <div className="flex gap-2">
                <button
                    onClick={handleApprove}
                    disabled={isProcessing}
                    className="flex-1 flex items-center justify-center gap-1.5 rounded px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-90 disabled:opacity-50"
                    style={{
                        backgroundColor: "hsl(142, 70%, 45%)",
                        color: "white",
                    }}
                >
                    <CheckCircle2 className="h-3.5 w-3.5" />
                    Approve
                </button>
                <button
                    onClick={handleDeny}
                    disabled={isProcessing}
                    className="flex-1 flex items-center justify-center gap-1.5 rounded border px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-90 disabled:opacity-50"
                    style={{
                        borderColor: "hsl(0, 72%, 51%)60",
                        color: "hsl(0, 72%, 65%)",
                    }}
                >
                    <XCircle className="h-3.5 w-3.5" />
                    Deny
                </button>
            </div>

            {/* Warning for high/critical */}
            {(approval.riskLevel === "High" || approval.riskLevel === "Critical") && (
                <div className="flex items-start gap-2 mt-3 text-[10px] text-amber-500/80">
                    <AlertTriangle className="h-3 w-3 mt-0.5 flex-shrink-0" />
                    <span>This is a {approval.riskLevel.toLowerCase()}-risk operation. Please review carefully before approving.</span>
                </div>
            )}
        </div>
    )
}
