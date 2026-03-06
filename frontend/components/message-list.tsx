"use client"

import { useEffect, useRef, useState, useMemo } from "react"
import type { Agent, ChatMessage } from "@/lib/agents"
import ReactMarkdown from "react-markdown"
import { StartConversationEmpty } from "@/components/start-conversation-empty"
import { ChevronDown, ChevronRight, Shield, FileText, Lightbulb, AlertTriangle, CheckCircle2, Bot } from "lucide-react"

function formatTime(date: Date) {
    return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
}

// ─── Structured Blocks ──────────────────────────────────────────────────────

type BlockType = "triage" | "plan" | "evidence" | "interpretation" | "hypothesis" | "recommended-action" | "approval-required" | "resolved"

interface StructuredBlock {
    type: BlockType
    content: string
}

function parseStructuredBlocks(content: string): { blocks: StructuredBlock[]; plainContent: string } {
    const blocks: StructuredBlock[] = []
    const markers: [string, BlockType][] = [
        ["[TRIAGE]", "triage"],
        ["[PLAN]", "plan"],
        ["[EVIDENCE]", "evidence"],
        ["[INTERPRETATION]", "interpretation"],
        ["[HYPOTHESIS]", "hypothesis"],
        ["[RECOMMENDED ACTION]", "recommended-action"],
        ["[APPROVAL REQUIRED]", "approval-required"],
        ["[RESOLVED]", "resolved"],
    ]

    let remaining = content
    for (const [marker, type] of markers) {
        const idx = remaining.indexOf(marker)
        if (idx !== -1) {
            const afterMarker = idx + marker.length
            let endIdx = remaining.length
            for (const [nextMarker] of markers) {
                if (nextMarker === marker) continue
                const nextIdx = remaining.indexOf(nextMarker, afterMarker)
                if (nextIdx !== -1 && nextIdx < endIdx) endIdx = nextIdx
            }
            blocks.push({ type, content: remaining.slice(afterMarker, endIdx).trim() })
        }
    }

    const firstMarkerIdx = markers.reduce((minIdx, [marker]) => {
        const idx = content.indexOf(marker)
        return idx !== -1 && idx < minIdx ? idx : minIdx
    }, content.length)
    const plainContent = content.slice(0, firstMarkerIdx).trim()

    return { blocks, plainContent }
}

const blockConfig: Record<BlockType, { label: string; color: string; icon: React.ReactNode; collapsible: boolean }> = {
    triage: { label: "Triage", color: "hsl(45, 93%, 47%)", icon: <AlertTriangle className="h-3.5 w-3.5" />, collapsible: false },
    plan: { label: "Plan", color: "hsl(200, 100%, 50%)", icon: <FileText className="h-3.5 w-3.5" />, collapsible: false },
    evidence: { label: "Evidence", color: "hsl(142, 70%, 45%)", icon: <FileText className="h-3.5 w-3.5" />, collapsible: true },
    interpretation: { label: "Interpretation", color: "hsl(210, 80%, 60%)", icon: <Lightbulb className="h-3.5 w-3.5" />, collapsible: true },
    hypothesis: { label: "Hypothesis", color: "hsl(280, 70%, 60%)", icon: <Lightbulb className="h-3.5 w-3.5" />, collapsible: false },
    "recommended-action": { label: "Recommended Action", color: "hsl(170, 70%, 45%)", icon: <CheckCircle2 className="h-3.5 w-3.5" />, collapsible: false },
    "approval-required": { label: "Approval Required", color: "hsl(25, 95%, 53%)", icon: <Shield className="h-3.5 w-3.5" />, collapsible: false },
    resolved: { label: "Resolved", color: "hsl(142, 70%, 45%)", icon: <CheckCircle2 className="h-3.5 w-3.5" />, collapsible: false },
}

function StructuredBlockCard({ block, onApprove }: { block: StructuredBlock; onApprove?: (action: string) => void }) {
    const [collapsed, setCollapsed] = useState(false)
    const config = blockConfig[block.type]

    return (
        <div
            className="my-2 rounded-lg border"
            style={{
                borderColor: config.color + "40",
                backgroundColor: config.color + "08",
            }}
        >
            <div
                className={`flex items-center gap-2 px-3 py-1.5 ${config.collapsible ? "cursor-pointer" : ""}`}
                onClick={() => config.collapsible && setCollapsed((c) => !c)}
            >
                <span style={{ color: config.color }}>{config.icon}</span>
                <span className="text-xs font-semibold uppercase tracking-wide" style={{ color: config.color }}>
                    {config.label}
                </span>
                {config.collapsible && (
                    <span style={{ color: config.color }} className="ml-auto">
                        {collapsed ? <ChevronDown className="h-3 w-3" /> : <ChevronRight className="h-3 w-3" />}
                    </span>
                )}
            </div>
            {!collapsed && (
                <div className="px-3 pb-2 text-sm" style={{ color: "hsl(210, 3%, 83%)" }}>
                    <MarkdownContent content={block.content} />
                    {block.type === "approval-required" && onApprove && (
                        <div className="mt-2 flex gap-2">
                            <button
                                onClick={() => onApprove(block.content)}
                                className="rounded-md px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-90"
                                style={{ backgroundColor: "hsl(142, 70%, 45%)", color: "#fff" }}
                            >
                                Approve
                            </button>
                            <button
                                onClick={() => onApprove("[REJECTED] " + block.content)}
                                className="rounded-md px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-90"
                                style={{ backgroundColor: "hsl(0, 72%, 51%)", color: "#fff" }}
                            >
                                Reject
                            </button>
                        </div>
                    )}
                </div>
            )}
        </div>
    )
}

// ─── Markdown Renderer ──────────────────────────────────────────────────────

const markdownComponents = {
    p: ({ children }: { children?: React.ReactNode }) => <p className="mb-2 last:mb-0">{children}</p>,
    code: ({ children, className }: { children?: React.ReactNode; className?: string }) => {
        const isBlock = className?.includes("language-")
        if (isBlock) {
            return (
                <pre
                    className="my-2 overflow-x-auto rounded-md p-3 text-sm"
                    style={{ backgroundColor: "hsl(228, 7%, 12%)", border: "1px solid hsl(228, 6%, 20%)" }}
                >
                    <code>{children}</code>
                </pre>
            )
        }
        return (
            <code className="rounded px-1 py-0.5 text-sm" style={{ backgroundColor: "hsl(228, 7%, 14%)", color: "hsl(210, 3%, 90%)" }}>
                {children}
            </code>
        )
    },
    pre: ({ children }: { children?: React.ReactNode }) => <>{children}</>,
    ul: ({ children }: { children?: React.ReactNode }) => <ul className="mb-2 ml-4 list-disc">{children}</ul>,
    ol: ({ children }: { children?: React.ReactNode }) => <ol className="mb-2 ml-4 list-decimal">{children}</ol>,
    strong: ({ children }: { children?: React.ReactNode }) => <strong style={{ color: "hsl(0, 0%, 100%)" }}>{children}</strong>,
    a: ({ children, href }: { children?: React.ReactNode; href?: string }) => (
        <a href={href} target="_blank" rel="noopener noreferrer" style={{ color: "hsl(200, 100%, 60%)" }} className="hover:underline">
            {children}
        </a>
    ),
    table: ({ children }: { children?: React.ReactNode }) => (
        <div className="my-2 overflow-x-auto">
            <table className="min-w-full text-sm" style={{ borderCollapse: "collapse" }}>{children}</table>
        </div>
    ),
    th: ({ children }: { children?: React.ReactNode }) => (
        <th className="border-b px-2 py-1 text-left text-xs font-semibold" style={{ borderColor: "hsl(228, 6%, 30%)", color: "hsl(0, 0%, 90%)" }}>
            {children}
        </th>
    ),
    td: ({ children }: { children?: React.ReactNode }) => (
        <td className="border-b px-2 py-1 text-xs" style={{ borderColor: "hsl(228, 6%, 25%)" }}>
            {children}
        </td>
    ),
}

function MarkdownContent({ content }: { content: string }) {
    return <ReactMarkdown components={markdownComponents as any}>{content}</ReactMarkdown>
}

// ─── Deduplication ──────────────────────────────────────────────────────────

/**
 * Compute a rough content fingerprint for deduplication.
 */
function contentFingerprint(content: string): string {
    const normalized = content.replace(/\s+/g, " ").trim()
    return `${normalized.slice(0, 300)}::${normalized.length}`
}

/** Check if two messages have substantially similar content (>70% overlap) */
function isSimilarContent(a: string, b: string): boolean {
    const normA = a.replace(/\s+/g, " ").trim()
    const normB = b.replace(/\s+/g, " ").trim()

    // If one is a substring of the other
    if (normA.length > 100 && normB.length > 100) {
        if (normA.includes(normB.slice(0, 200)) || normB.includes(normA.slice(0, 200))) return true
    }

    // Compare first 300 chars
    const prefixA = normA.slice(0, 300)
    const prefixB = normB.slice(0, 300)
    if (prefixA === prefixB && normA.length > 100) return true

    // Check overlap ratio
    const minLen = Math.min(normA.length, normB.length)
    if (minLen < 100) return false
    let shared = 0
    const checkLen = Math.min(minLen, 500)
    for (let i = 0; i < checkLen; i++) {
        if (normA[i] === normB[i]) shared++
    }
    return shared / checkLen > 0.7
}

/**
 * Deduplicate messages within a group: keep the LATEST version of similar content.
 */
function deduplicateMessages(messages: ChatMessage[]): ChatMessage[] {
    if (messages.length <= 1) return messages

    const keep: ChatMessage[] = []
    const seen = new Set<string>()

    // Process from last to first — keep the LATEST version of similar content
    for (let i = messages.length - 1; i >= 0; i--) {
        const fp = contentFingerprint(messages[i].content)
        if (seen.has(fp)) continue

        // Check if similar to any already-kept message
        let isDuplicate = false
        for (const kept of keep) {
            if (isSimilarContent(messages[i].content, kept.content)) {
                isDuplicate = true
                break
            }
        }
        if (!isDuplicate) {
            keep.unshift(messages[i])
            seen.add(fp)
        }
    }

    return keep
}

// ─── Message Groups ─────────────────────────────────────────────────────────

interface MessageGroup {
    userMessage: ChatMessage
    intermediateMessages: ChatMessage[]
    finalMessage: ChatMessage | null
}

/**
 * Group messages into "runs": each user message starts a new group,
 * all subsequent agent messages belong to that group until the next user message.
 * The last agent message in each group is the "final answer".
 */
function groupMessages(messages: ChatMessage[]): MessageGroup[] {
    const groups: MessageGroup[] = []
    let currentGroup: MessageGroup | null = null

    for (const msg of messages) {
        if (msg.role === "user") {
            // Finalize previous group
            if (currentGroup) {
                finalizeGroup(currentGroup)
                groups.push(currentGroup)
            }
            currentGroup = {
                userMessage: msg,
                intermediateMessages: [],
                finalMessage: null,
            }
        } else if (msg.role === "assistant") {
            if (!currentGroup) {
                // Agent message without a preceding user message — create phantom group
                currentGroup = {
                    userMessage: { id: "phantom-" + msg.id, role: "user", content: "", timestamp: msg.timestamp },
                    intermediateMessages: [],
                    finalMessage: null,
                }
            }
            currentGroup.intermediateMessages.push(msg)
        }
    }

    if (currentGroup) {
        finalizeGroup(currentGroup)
        groups.push(currentGroup)
    }

    return groups
}

function finalizeGroup(group: MessageGroup) {
    if (group.intermediateMessages.length === 0) return

    // Deduplicate intermediate messages
    const deduped = deduplicateMessages(group.intermediateMessages)

    // Last message = final answer
    group.finalMessage = deduped[deduped.length - 1]
    group.intermediateMessages = deduped.slice(0, -1)
}

// ─── Agent Avatar ───────────────────────────────────────────────────────────

function AgentAvatar({ agent, agentName, size = "normal", isError }: {
    agent?: Agent
    agentName?: string
    size?: "normal" | "small"
    isError?: boolean
}) {
    const sizeClasses = size === "small" ? "h-6 w-6 text-[10px]" : "h-9 w-9 text-xs"
    return (
        <div
            className={`flex shrink-0 items-center justify-center rounded-full font-bold ${sizeClasses}`}
            style={{
                backgroundColor: isError
                    ? "hsl(0, 72%, 51%)"
                    : (agent?.color ?? "hsl(214, 5%, 55%)"),
                color: "#fff",
            }}
            title={isError ? "Error" : (agent?.name ?? agentName ?? "Agent")}
        >
            {isError ? "!" : (agent?.avatar ?? (agentName?.[0]?.toUpperCase() ?? "?"))}
        </div>
    )
}

// ─── Collapsible Reasoning Chain ────────────────────────────────────────────

function ReasoningChain({ messages, agents, onApprove }: {
    messages: ChatMessage[]
    agents: Map<string, Agent>
    onApprove?: (action: string) => void
}) {
    const [expanded, setExpanded] = useState(false)

    if (messages.length === 0) return null

    // Collect unique agent names involved
    const agentNames = [...new Set(messages.map((m) => {
        const agent = m.agentId ? agents.get(m.agentId) : undefined
        return agent?.name ?? m.agentName ?? "Agent"
    }))]

    return (
        <div className="mb-3 ml-12">
            <button
                onClick={() => setExpanded(!expanded)}
                className="flex items-center gap-2 rounded-lg px-3 py-2 text-xs font-medium transition-all hover:brightness-125"
                style={{
                    color: "hsl(210, 3%, 65%)",
                    backgroundColor: expanded ? "hsl(228, 7%, 18%)" : "hsl(228, 7%, 15%)",
                    border: "1px solid hsl(228, 6%, 25%)",
                }}
            >
                {expanded ? (
                    <ChevronDown className="h-3.5 w-3.5" />
                ) : (
                    <ChevronRight className="h-3.5 w-3.5" />
                )}
                <Bot className="h-3.5 w-3.5" style={{ color: "hsl(200, 100%, 50%)" }} />
                <span>
                    Reasoning chain ({messages.length} {messages.length === 1 ? "step" : "steps"})
                </span>
                <span style={{ color: "hsl(214, 5%, 45%)" }}>
                    — {agentNames.join(", ")}
                </span>
            </button>

            {expanded && (
                <div
                    className="ml-2 mt-2 space-y-2 border-l-2 pl-3"
                    style={{ borderColor: "hsl(228, 6%, 25%)" }}
                >
                    {messages.map((message) => {
                        const agent = message.agentId ? agents.get(message.agentId) : undefined
                        const { blocks, plainContent } = parseStructuredBlocks(message.content)
                        const hasStructuredContent = blocks.length > 0
                        const displayName = agent?.name ?? message.agentName ?? "Agent"

                        return (
                            <div
                                key={message.id}
                                className="rounded-lg px-3 py-2"
                                style={{ backgroundColor: "hsl(228, 7%, 16%)" }}
                            >
                                <div className="mb-1.5 flex items-center gap-2">
                                    <AgentAvatar agent={agent} agentName={message.agentName} size="small" />
                                    <span className="text-xs font-semibold" style={{ color: agent?.color ?? "hsl(200, 100%, 60%)" }}>
                                        {displayName}
                                    </span>
                                    <span className="text-xs" style={{ color: "hsl(214, 5%, 45%)" }}>
                                        {formatTime(message.timestamp)}
                                    </span>
                                </div>
                                <div className="text-sm leading-relaxed" style={{ color: "hsl(210, 3%, 75%)" }}>
                                    {plainContent && <MarkdownContent content={plainContent} />}
                                    {hasStructuredContent && blocks.map((block, i) => (
                                        <StructuredBlockCard key={`${message.id}-block-${i}`} block={block} onApprove={onApprove} />
                                    ))}
                                    {!hasStructuredContent && !plainContent && (
                                        <MarkdownContent content={message.content} />
                                    )}
                                </div>
                            </div>
                        )
                    })}
                </div>
            )}
        </div>
    )
}

// ─── Main Message Content ───────────────────────────────────────────────────

function MessageContent({ message, onApprove }: {
    message: ChatMessage
    onApprove?: (action: string) => void
}) {
    const isErrorMessage = message.content.startsWith("⚠️")
    const { blocks, plainContent } = parseStructuredBlocks(message.content)
    const hasStructuredContent = blocks.length > 0

    return (
        <div
            className={`max-w-2xl text-sm leading-relaxed ${isErrorMessage ? "rounded-lg border px-3 py-2" : ""}`}
            style={{
                color: "hsl(210, 3%, 83%)",
                ...(isErrorMessage ? {
                    backgroundColor: "hsl(0, 72%, 51%, 0.08)",
                    borderColor: "hsl(0, 72%, 51%, 0.3)",
                } : {})
            }}
        >
            {plainContent && <MarkdownContent content={plainContent} />}
            {hasStructuredContent && blocks.map((block, i) => (
                <StructuredBlockCard key={`${message.id}-block-${i}`} block={block} onApprove={onApprove} />
            ))}
            {!hasStructuredContent && !plainContent && (
                <MarkdownContent content={message.content} />
            )}
        </div>
    )
}

// ─── MessageList Component ──────────────────────────────────────────────────

interface MessageListProps {
    messages: ChatMessage[]
    agents: Agent[]
    streamingAgentId: string | null
    streamingContent: string
    onApprove?: (action: string) => void
}

export function MessageList({
    messages,
    agents,
    streamingAgentId,
    streamingContent,
    onApprove,
}: MessageListProps) {
    const bottomRef = useRef<HTMLDivElement>(null)

    useEffect(() => {
        bottomRef.current?.scrollIntoView({ behavior: "smooth" })
    }, [messages, streamingContent])

    const agentMap = useMemo(() => new Map(agents.map((a) => [a.id, a])), [agents])

    const messageGroups = useMemo(() => groupMessages(messages), [messages])

    if (messages.length === 0 && !streamingAgentId) {
        return (
            <>
                <StartConversationEmpty />
                <div ref={bottomRef} />
            </>
        )
    }

    return (
        <div className="flex flex-1 flex-col overflow-y-auto px-4 py-4">
            {messageGroups.map((group, groupIndex) => (
                <div key={`group-${groupIndex}`}>
                    {/* User message */}
                    {group.userMessage.content && (
                        <div className="mb-4 flex items-start justify-end gap-3">
                            <div className="flex flex-col items-end">
                                <div className="mb-1 flex items-center gap-2">
                                    <span className="text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
                                        {formatTime(group.userMessage.timestamp)}
                                    </span>
                                    <span className="text-sm font-medium" style={{ color: "hsl(0, 0%, 100%)" }}>
                                        You
                                    </span>
                                </div>
                                <div
                                    className="max-w-lg rounded-xl rounded-tr-sm px-4 py-2.5 text-sm leading-relaxed"
                                    style={{ backgroundColor: "hsl(235, 86%, 65%)", color: "#fff" }}
                                >
                                    {group.userMessage.content}
                                </div>
                            </div>
                            <div
                                className="mt-6 flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                                style={{ backgroundColor: "hsl(168, 76%, 42%)", color: "#fff" }}
                            >
                                U
                            </div>
                        </div>
                    )}

                    {/* Collapsible reasoning chain (intermediate messages) */}
                    <ReasoningChain
                        messages={group.intermediateMessages}
                        agents={agentMap}
                        onApprove={onApprove}
                    />

                    {/* Final answer — the primary response shown to the user */}
                    {group.finalMessage && (() => {
                        const msg = group.finalMessage!
                        const agent = msg.agentId ? agentMap.get(msg.agentId) : undefined
                        const isError = msg.content.startsWith("⚠️")
                        const displayName = agent?.name ?? msg.agentName ?? "Agent"

                        return (
                            <div className="mb-4 flex items-start gap-3">
                                <div className="mt-0.5">
                                    <AgentAvatar
                                        agent={agent}
                                        agentName={msg.agentName}
                                        isError={isError}
                                    />
                                </div>
                                <div className="min-w-0 flex-1">
                                    <div className="mb-1 flex items-center gap-2">
                                        <span
                                            className="text-sm font-medium"
                                            style={{ color: isError ? "hsl(0, 72%, 51%)" : (agent?.color ?? "hsl(0, 0%, 100%)") }}
                                        >
                                            {isError ? "Error" : displayName}
                                        </span>
                                        <span className="text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
                                            {formatTime(msg.timestamp)}
                                        </span>
                                        {/* Agent role badge for non-Manager agents */}
                                        {msg.agentName && msg.agentName !== displayName && (
                                            <span
                                                className="rounded-full px-2 py-0.5 text-[10px] font-medium"
                                                style={{
                                                    backgroundColor: (agent?.color ?? "hsl(200, 100%, 50%)") + "20",
                                                    color: agent?.color ?? "hsl(200, 100%, 60%)",
                                                }}
                                            >
                                                {msg.agentName}
                                            </span>
                                        )}
                                    </div>
                                    <MessageContent message={msg} onApprove={onApprove} />
                                </div>
                            </div>
                        )
                    })()}
                </div>
            ))}

            {/* Streaming message */}
            {streamingAgentId && streamingContent && (
                <div className="mb-4 flex items-start gap-3">
                    <div className="mt-0.5">
                        <AgentAvatar agent={agentMap.get(streamingAgentId)} />
                    </div>
                    <div className="min-w-0 flex-1">
                        <div className="mb-1 flex items-center gap-2">
                            <span className="text-sm font-medium" style={{ color: "hsl(0, 0%, 100%)" }}>
                                {agentMap.get(streamingAgentId)?.name ?? "Agent"}
                            </span>
                            <span className="text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
                                {formatTime(new Date())}
                            </span>
                        </div>
                        <div className="max-w-2xl text-sm leading-relaxed" style={{ color: "hsl(210, 3%, 83%)" }}>
                            <MarkdownContent content={streamingContent} />
                        </div>
                    </div>
                </div>
            )}

            {/* Typing indicator */}
            {streamingAgentId && !streamingContent && (
                <div className="mb-4 flex items-center gap-3">
                    <AgentAvatar agent={agentMap.get(streamingAgentId)} />
                    <div className="flex items-center gap-1">
                        <div className="typing-dot h-2 w-2 rounded-full" style={{ backgroundColor: "hsl(210, 3%, 80%)" }} />
                        <div className="typing-dot h-2 w-2 rounded-full" style={{ backgroundColor: "hsl(210, 3%, 80%)" }} />
                        <div className="typing-dot h-2 w-2 rounded-full" style={{ backgroundColor: "hsl(210, 3%, 80%)" }} />
                    </div>
                    <span className="text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
                        {(agentMap.get(streamingAgentId)?.name ?? "Agent") + " is typing..."}
                    </span>
                </div>
            )}

            <div ref={bottomRef} />
        </div>
    )
}
