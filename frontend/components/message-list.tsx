"use client"

import { useEffect, useRef, useState } from "react"
import type { Agent, ChatMessage } from "@/lib/agents"
import ReactMarkdown from "react-markdown"
import { StartConversationEmpty } from "@/components/start-conversation-empty"
import { ChevronDown, ChevronUp, Shield, FileText, Lightbulb, AlertTriangle, CheckCircle2 } from "lucide-react"

function formatTime(date: Date) {
  return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
}

/** Structured block markers we detect in agent output */
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
      // Find end of this block (next marker or end of string)
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

  // Plain content is everything before the first marker
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
            {collapsed ? <ChevronDown className="h-3 w-3" /> : <ChevronUp className="h-3 w-3" />}
          </span>
        )}
      </div>
      {!collapsed && (
        <div className="px-3 pb-2 text-sm" style={{ color: "hsl(210, 3%, 83%)" }}>
          <ReactMarkdown
            components={{
              p: ({ children }) => <p className="mb-1 last:mb-0">{children}</p>,
              strong: ({ children }) => <strong style={{ color: "hsl(0, 0%, 100%)" }}>{children}</strong>,
              code: ({ children }) => (
                <code className="rounded px-1 py-0.5 text-sm" style={{ backgroundColor: "hsl(228, 7%, 14%)", color: "hsl(210, 3%, 90%)" }}>
                  {children}
                </code>
              ),
            }}
          >
            {block.content}
          </ReactMarkdown>
          {block.type === "approval-required" && onApprove && (
            <div className="mt-2 flex gap-2">
              <button
                onClick={() => onApprove(block.content)}
                className="rounded-md px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-90"
                style={{
                  backgroundColor: "hsl(142, 70%, 45%)",
                  color: "#fff",
                }}
              >
                Approve
              </button>
              <button
                onClick={() => onApprove("[REJECTED] " + block.content)}
                className="rounded-md px-3 py-1.5 text-xs font-medium transition-colors hover:opacity-90"
                style={{
                  backgroundColor: "hsl(0, 72%, 51%)",
                  color: "#fff",
                }}
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

  const agentMap = new Map(agents.map((a) => [a.id, a]))

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
      {messages.map((message) => {
        if (message.role === "user") {
          return (
            <div
              key={message.id}
              className="mb-4 flex items-start justify-end gap-3"
            >
              <div className="flex flex-col items-end">
                <div className="mb-1 flex items-center gap-2">
                  <span
                    className="text-xs"
                    style={{ color: "hsl(214, 5%, 55%)" }}
                  >
                    {formatTime(message.timestamp)}
                  </span>
                  <span
                    className="text-sm font-medium"
                    style={{ color: "hsl(0, 0%, 100%)" }}
                  >
                    You
                  </span>
                </div>
                <div
                  className="max-w-lg rounded-xl rounded-tr-sm px-4 py-2.5 text-sm leading-relaxed"
                  style={{
                    backgroundColor: "hsl(235, 86%, 65%)",
                    color: "#fff",
                  }}
                >
                  {message.content}
                </div>
              </div>
              <div
                className="mt-6 flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                style={{
                  backgroundColor: "hsl(168, 76%, 42%)",
                  color: "#fff",
                }}
              >
                U
              </div>
            </div>
          )
        }

        // Assistant message
        const agent = message.agentId
          ? agentMap.get(message.agentId)
          : undefined

        // Check if it's an error message
        const isErrorMessage = message.content.startsWith("⚠️")

        // Parse structured blocks from agent output
        const { blocks, plainContent } = parseStructuredBlocks(message.content)
        const hasStructuredContent = blocks.length > 0

        return (
          <div key={message.id} className="mb-4 flex items-start gap-3">
            <div
              className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
              style={{
                backgroundColor: isErrorMessage
                  ? "hsl(0, 72%, 51%)"
                  : (agent?.color ?? "hsl(214, 5%, 55%)"),
                color: "#fff",
              }}
            >
              {isErrorMessage ? "!" : (agent?.avatar ?? "?")}
            </div>
            <div className="min-w-0 flex-1">
              <div className="mb-1 flex items-center gap-2">
                <span
                  className="text-sm font-medium"
                  style={{ color: isErrorMessage ? "hsl(0, 72%, 51%)" : "hsl(0, 0%, 100%)" }}
                >
                  {isErrorMessage ? "Error" : (agent?.name ?? "Agent")}
                </span>
                <span
                  className="text-xs"
                  style={{ color: "hsl(214, 5%, 55%)" }}
                >
                  {formatTime(message.timestamp)}
                </span>
              </div>
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
                {/* Plain text content (before any markers) */}
                {plainContent && (
                  <ReactMarkdown
                    components={{
                      p: ({ children }) => (
                        <p className="mb-2 last:mb-0">{children}</p>
                      ),
                      code: ({ children, className }) => {
                        const isBlock = className?.includes("language-")
                        if (isBlock) {
                          return (
                            <pre
                              className="my-2 overflow-x-auto rounded-md p-3 text-sm"
                              style={{
                                backgroundColor: "hsl(228, 7%, 12%)",
                                border: "1px solid hsl(228, 6%, 20%)",
                              }}
                            >
                              <code>{children}</code>
                            </pre>
                          )
                        }
                        return (
                          <code
                            className="rounded px-1 py-0.5 text-sm"
                            style={{
                              backgroundColor: "hsl(228, 7%, 14%)",
                              color: "hsl(210, 3%, 90%)",
                            }}
                          >
                            {children}
                          </code>
                        )
                      },
                      pre: ({ children }) => <>{children}</>,
                      ul: ({ children }) => (
                        <ul className="mb-2 ml-4 list-disc">{children}</ul>
                      ),
                      ol: ({ children }) => (
                        <ol className="mb-2 ml-4 list-decimal">{children}</ol>
                      ),
                      strong: ({ children }) => (
                        <strong style={{ color: "hsl(0, 0%, 100%)" }}>
                          {children}
                        </strong>
                      ),
                      a: ({ children, href }) => (
                        <a
                          href={href}
                          target="_blank"
                          rel="noopener noreferrer"
                          style={{ color: "hsl(200, 100%, 60%)" }}
                          className="hover:underline"
                        >
                          {children}
                        </a>
                      ),
                    }}
                  >
                    {plainContent}
                  </ReactMarkdown>
                )}
                {/* Structured blocks rendered as cards */}
                {hasStructuredContent && blocks.map((block, i) => (
                  <StructuredBlockCard key={`${message.id}-block-${i}`} block={block} onApprove={onApprove} />
                ))}
                {/* Fallback: if no structured blocks, render full content */}
                {!hasStructuredContent && !plainContent && (
                  <ReactMarkdown
                    components={{
                      p: ({ children }) => (
                        <p className="mb-2 last:mb-0">{children}</p>
                      ),
                      code: ({ children, className }) => {
                        const isBlock = className?.includes("language-")
                        if (isBlock) {
                          return (
                            <pre
                              className="my-2 overflow-x-auto rounded-md p-3 text-sm"
                              style={{
                                backgroundColor: "hsl(228, 7%, 12%)",
                                border: "1px solid hsl(228, 6%, 20%)",
                              }}
                            >
                              <code>{children}</code>
                            </pre>
                          )
                        }
                        return (
                          <code
                            className="rounded px-1 py-0.5 text-sm"
                            style={{
                              backgroundColor: "hsl(228, 7%, 14%)",
                              color: "hsl(210, 3%, 90%)",
                            }}
                          >
                            {children}
                          </code>
                        )
                      },
                      pre: ({ children }) => <>{children}</>,
                      ul: ({ children }) => (
                        <ul className="mb-2 ml-4 list-disc">{children}</ul>
                      ),
                      ol: ({ children }) => (
                        <ol className="mb-2 ml-4 list-decimal">{children}</ol>
                      ),
                      strong: ({ children }) => (
                        <strong style={{ color: "hsl(0, 0%, 100%)" }}>
                          {children}
                        </strong>
                      ),
                      a: ({ children, href }) => (
                        <a
                          href={href}
                          target="_blank"
                          rel="noopener noreferrer"
                          style={{ color: "hsl(200, 100%, 60%)" }}
                          className="hover:underline"
                        >
                          {children}
                        </a>
                      ),
                    }}
                  >
                    {message.content}
                  </ReactMarkdown>
                )}
              </div>
            </div>
          </div>
        )
      })}

      {/* Streaming message */}
      {streamingAgentId && streamingContent && (
        <div className="mb-4 flex items-start gap-3">
          <div
            className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
            style={{
              backgroundColor:
                agentMap.get(streamingAgentId)?.color ?? "hsl(214, 5%, 55%)",
              color: "#fff",
            }}
          >
            {agentMap.get(streamingAgentId)?.avatar ?? "?"}
          </div>
          <div className="min-w-0 flex-1">
            <div className="mb-1 flex items-center gap-2">
              <span
                className="text-sm font-medium"
                style={{ color: "hsl(0, 0%, 100%)" }}
              >
                {agentMap.get(streamingAgentId)?.name ?? "Agent"}
              </span>
              <span
                className="text-xs"
                style={{ color: "hsl(214, 5%, 55%)" }}
              >
                {formatTime(new Date())}
              </span>
            </div>
            <div
              className="max-w-2xl text-sm leading-relaxed"
              style={{ color: "hsl(210, 3%, 83%)" }}
            >
              <ReactMarkdown
                components={{
                  p: ({ children }) => (
                    <p className="mb-2 last:mb-0">{children}</p>
                  ),
                  code: ({ children, className }) => {
                    const isBlock = className?.includes("language-")
                    if (isBlock) {
                      return (
                        <pre
                          className="my-2 overflow-x-auto rounded-md p-3 text-sm"
                          style={{
                            backgroundColor: "hsl(228, 7%, 12%)",
                            border: "1px solid hsl(228, 6%, 20%)",
                          }}
                        >
                          <code>{children}</code>
                        </pre>
                      )
                    }
                    return (
                      <code
                        className="rounded px-1 py-0.5 text-sm"
                        style={{
                          backgroundColor: "hsl(228, 7%, 14%)",
                          color: "hsl(210, 3%, 90%)",
                        }}
                      >
                        {children}
                      </code>
                    )
                  },
                  pre: ({ children }) => <>{children}</>,
                }}
              >
                {streamingContent}
              </ReactMarkdown>
            </div>
          </div>
        </div>
      )}

      {/* Typing indicator for queued agents */}
      {streamingAgentId && !streamingContent && (
        <div className="mb-4 flex items-center gap-3">
          <div
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
            style={{
              backgroundColor:
                agentMap.get(streamingAgentId)?.color ?? "hsl(214, 5%, 55%)",
              color: "#fff",
            }}
          >
            {agentMap.get(streamingAgentId)?.avatar ?? "?"}
          </div>
          <div className="flex items-center gap-1">
            <div
              className="typing-dot h-2 w-2 rounded-full"
              style={{ backgroundColor: "hsl(210, 3%, 80%)" }}
            />
            <div
              className="typing-dot h-2 w-2 rounded-full"
              style={{ backgroundColor: "hsl(210, 3%, 80%)" }}
            />
            <div
              className="typing-dot h-2 w-2 rounded-full"
              style={{ backgroundColor: "hsl(210, 3%, 80%)" }}
            />
          </div>
          <span className="text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
            {(agentMap.get(streamingAgentId)?.name ?? "Agent") +
              " is typing..."}
          </span>
        </div>
      )}

      <div ref={bottomRef} />
    </div>
  )
}
