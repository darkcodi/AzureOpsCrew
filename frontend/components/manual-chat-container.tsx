"use client"

import { useEffect, useRef, useState } from "react"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"
import { MessageInput } from "@/components/message-input"
import { fetchWithErrorHandling } from "@/lib/fetch"
import type { Agent } from "@/lib/agents"
import { StartConversationEmpty } from "@/components/start-conversation-empty"
import { DeploymentCard } from "@/components/deployment-card"
import { MyIpCard, type IpInfo } from "@/components/my-ip-card"
import { BackendToolCard } from "@/components/backend-tool-card"

const KNOWN_FE_TOOL_NAMES = new Set(["showMyIp", "showDeployment"])
const DM_EMPTY_SUBTITLE = "Send a message to get started."

/** Flat tool run widget: ToolName, CallId, Args, Result (no nested Data). */
export interface ToolWidget {
  toolName: string
  callId: string
  args: Record<string, unknown>
  result: Record<string, unknown>
}

function deriveIpInfo(widget: ToolWidget): IpInfo | undefined {
  const from = (o: Record<string, unknown>): IpInfo | undefined => {
    if (!o || typeof o !== "object" || Array.isArray(o)) return undefined
    if ("ipAddress" in o || "ipVersion" in o) return o as IpInfo
    return undefined
  }
  return from(widget.result) ?? from(widget.args)
}

export function normalizeMarkdownBlockNewlines(content: string): string {
  if (!content || typeof content !== "string") return content
  // Ensure newline after ATX headings when content continues on same line (e.g. "# Title Next paragraph")
  let out = content.replace(/(#{1,6}\s[^\n]*?)(\s+)(?=[A-Z*_`-])/g, "$1\n\n$2")
  // Ensure "---" is on its own line for horizontal rule
  out = out.replace(/\s+---\s+/g, "\n\n---\n\n")
  return out
}

// Markdown components for consistent styling (exported for read-only chat views)
export const markdownComponents = {
  p: ({ children, ...props }: any) => (
    <p className="mb-2 last:mb-0" {...props}>{children}</p>
  ),
  h1: ({ children, ...props }: any) => (
    <h1 className="mb-2 mt-4 text-xl font-bold first:mt-0" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h1>
  ),
  h2: ({ children, ...props }: any) => (
    <h2 className="mb-2 mt-3 text-lg font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h2>
  ),
  h3: ({ children, ...props }: any) => (
    <h3 className="mb-2 mt-2 text-base font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h3>
  ),
  h4: ({ children, ...props }: any) => (
    <h4 className="mb-1 mt-2 text-sm font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h4>
  ),
  h5: ({ children, ...props }: any) => (
    <h5 className="mb-1 mt-1 text-sm font-medium" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h5>
  ),
  h6: ({ children, ...props }: any) => (
    <h6 className="mb-1 mt-1 text-xs font-medium" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h6>
  ),
  hr: ({ ...props }: any) => (
    <hr className="my-3 border-0" style={{ borderTop: "1px solid hsl(228, 6%, 28%)" }} {...props} />
  ),
  code: ({ children, className, ...props }: any) => {
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
        {...props}
      >
        {children}
      </code>
    )
  },
  pre: ({ children, ...props }: any) => <>{children}</>,
  ul: ({ children, ...props }: any) => (
    <ul className="mb-2 ml-4 list-disc" {...props}>{children}</ul>
  ),
  ol: ({ children, ...props }: any) => (
    <ol className="mb-2 ml-4 list-decimal" {...props}>{children}</ol>
  ),
  strong: ({ children, ...props }: any) => (
    <strong style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</strong>
  ),
  a: ({ children, href, ...props }: any) => (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      style={{ color: "hsl(200, 100%, 60%)" }}
      className="hover:underline"
      {...props}
    >
      {children}
    </a>
  ),
  table: ({ children, ...props }: any) => (
    <div className="my-2 overflow-x-auto rounded-md" style={{ border: "1px solid hsl(228, 6%, 20%)" }}>
      <table className="w-full border-collapse text-sm" {...props}>{children}</table>
    </div>
  ),
  thead: ({ children, ...props }: any) => (
    <thead style={{ backgroundColor: "hsl(228, 7%, 12%)" }} {...props}>{children}</thead>
  ),
  tbody: ({ children, ...props }: any) => <tbody {...props}>{children}</tbody>,
  tr: ({ children, ...props }: any) => (
    <tr style={{ borderBottom: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</tr>
  ),
  th: ({ children, ...props }: any) => (
    <th className="px-3 py-2 text-left font-semibold" style={{ color: "hsl(0, 0%, 100%)", borderRight: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</th>
  ),
  td: ({ children, ...props }: any) => (
    <td className="px-3 py-2" style={{ borderRight: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</td>
  ),
}

/** Renders a single message widget (tool result). Optional onFollowUp for read-only views. */
export function renderMessageWidget(
  widget: ChatMessage["widget"],
  onFollowUp?: (content: string) => void
) {
  if (!widget) return null
  const followUp = onFollowUp ?? (() => {})
  if (KNOWN_FE_TOOL_NAMES.has(widget.toolName)) {
    if (widget.toolName === "showMyIp")
      return <MyIpCard ipInfo={deriveIpInfo(widget)} onFollowUp={followUp} />
    if (widget.toolName === "showDeployment")
      return <DeploymentCard onFollowUp={followUp} />
  }
  return (
    <BackendToolCard
      toolName={widget.toolName}
      args={widget.args}
      result={widget.result}
    />
  )
}

export interface ChatMessage {
  id: string
  role: "user" | "assistant" | "system"
  content: string
  reasoning?: string | null
  widget?: ToolWidget
}

interface ManualChatContainerProps {
  activeDMId: string | null
  agents: Agent[]
}

export function ManualChatContainer({ activeDMId, agents }: ManualChatContainerProps) {
  const { agentId } = useAgentRuntime()
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [expandedReasoningIds, setExpandedReasoningIds] = useState<Set<string>>(new Set())
  const messagesEndRef = useRef<HTMLDivElement>(null)

  const selectedAgent = agents.find((a) => a.id === activeDMId)
  const placeholder = selectedAgent
    ? `Message @${selectedAgent.name}...`
    : "Message @AzureOpsCrew Assistant..."

  // Scroll to bottom on new messages and when history loads
  useEffect(() => {
    const el = messagesEndRef.current
    if (!el) return
    const id = requestAnimationFrame(() => {
      el.scrollIntoView({ behavior: "smooth" })
    })
    return () => cancelAnimationFrame(id)
  }, [messages])

  // Load chat history when agent changes
  useEffect(() => {
    if (!activeDMId) {
      setMessages([])
      setExpandedReasoningIds(new Set())
      return
    }

    setExpandedReasoningIds(new Set())
    setIsLoading(true)

    // Fetch both user info and messages in parallel
    Promise.all([
      fetchWithErrorHandling('/api/auth/me'),
      fetchWithErrorHandling(`/api/dms/agents/${activeDMId}/messages`)
    ])
      .then(async ([userRes, messagesRes]) => {
        if (userRes.ok && messagesRes.ok) {
          const user = await userRes.json()
          const messages = await messagesRes.json()

          const chatMessages: ChatMessage[] = messages.map((m: {
            id: string
            chatId: string
            content: string
            senderId: string
            postedAt: string
          }) => ({
            id: m.id,
            role: m.senderId === user.id ? 'user' : 'assistant',
            content: m.content,
          }))
          setMessages(chatMessages)
        } else {
          setMessages([])
        }
      })
      .catch(() => {
        setMessages([])
      })
      .finally(() => setIsLoading(false))
  }, [activeDMId])

  const sendMessage = async (content: string) => {
    if (!activeDMId) return

    const response = await fetchWithErrorHandling(`/api/dms/agents/${activeDMId}/messages`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ Content: content }),
    })

    if (response.ok) {
      const message = await response.json()
      // Add user message to state
      const userMessage: ChatMessage = {
        id: message.id,
        role: "user",
        content: message.content,
      }
      setMessages((prev) => [...prev, userMessage])
    }
  }

  const toggleReasoning = (messageId: string) => {
    setExpandedReasoningIds((prev) => {
      const next = new Set(prev)
      if (next.has(messageId)) next.delete(messageId)
      else next.add(messageId)
      return next
    })
  }

  // Render a widget; resolve by toolName (known FE → specific widget, else generic box)
  const renderWidget = (w: ChatMessage["widget"]) => renderMessageWidget(w, sendMessage)

  // Show loading state
  if (isLoading) {
    return (
      <div className="flex min-h-0 flex-1 flex-col items-center justify-center">
        <div style={{ color: "hsl(0, 0%, 100%)" }}>Loading conversation...</div>
      </div>
    )
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {/* Messages area */}
      <div className="messagesArea min-h-0 flex-1 flex flex-col">
        {messages.length === 0 ? (
          <div className="messagesContainer min-h-0 flex-1 flex flex-col justify-center">
            <StartConversationEmpty title="Start a conversation" subtitle={DM_EMPTY_SUBTITLE} />
          </div>
        ) : (
          <div className="messagesContainer overflow-y-auto px-4 py-4">
            {messages.map((msg) => {
              if (msg.role === "user") {
                // User message - bubble on the right
                return (
                  <div
                    key={msg.id}
                    className="mb-4 flex items-end justify-end gap-3"
                  >
                    <div className="flex max-w-lg flex-col items-end">
                      <div
                        className="relative rounded-xl rounded-tr-sm px-4 py-2.5 text-sm leading-relaxed"
                        style={{
                          backgroundColor: "hsl(235, 86%, 65%)",
                          color: "#fff",
                        }}
                      >
                        <span
                          className="absolute bottom-2 right-0 block h-0 w-0 border-y-[6px] border-y-transparent border-l-[8px]"
                          style={{
                            borderLeftColor: "hsl(235, 86%, 65%)",
                            right: "-6px",
                          }}
                          aria-hidden
                        />
                        {msg.content}
                      </div>
                    </div>
                    <div
                      className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
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

              // Assistant message - on the left with avatar; widget-only as standalone, text in bubble
              const isWidgetOnly = !msg.content && !msg.reasoning && msg.widget
              return (
                <div key={msg.id} className="mb-4 flex items-end gap-3">
                  <div
                    className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                    style={{
                      backgroundColor: "hsl(235, 86%, 65%)",
                      color: "#fff",
                    }}
                  >
                    {selectedAgent ? selectedAgent.name.charAt(0).toUpperCase() : "A"}
                  </div>
                  {isWidgetOnly ? (
                    <div className="min-w-0 flex-1">
                      <div
                        className="mb-1.5 text-xs font-semibold"
                        style={{
                          color: selectedAgent?.color ?? "hsl(270, 55%, 78%)",
                        }}
                      >
                        {selectedAgent?.name ?? "Assistant"}
                      </div>
                      {renderWidget(msg.widget)}
                    </div>
                  ) : (
                    <div
                      className="assistantMessage relative"
                      style={{
                        color: "hsl(210, 3%, 92%)",
                        background: "hsl(228, 12%, 18%)",
                        border: "1px solid hsl(228, 6%, 28%)",
                        borderRadius: "var(--radius)",
                        padding: "0.75rem 1rem",
                        maxWidth: "100%",
                      }}
                    >
                      <span
                        className="absolute bottom-2 left-0 block h-0 w-0 border-y-[6px] border-y-transparent border-r-[8px]"
                        style={{
                          borderRightColor: "hsl(228, 12%, 18%)",
                          left: "-6px",
                        }}
                        aria-hidden
                      />
                      <div
                        className="mb-1.5 text-xs font-semibold"
                        style={{
                          color: selectedAgent?.color ?? "hsl(270, 55%, 78%)",
                        }}
                      >
                        {selectedAgent?.name ?? "Assistant"}
                      </div>
                      {msg.reasoning && !msg.content && (
                        <div
                          className="messageContent max-w-none rounded"
                          style={{ background: "hsl(228, 10%, 14%)" }}
                        >
                          <button
                            type="button"
                            onClick={() => toggleReasoning(msg.id)}
                            className="flex w-full items-center justify-between gap-2 rounded py-2 px-3 pr-1 text-left text-xs font-medium transition-colors hover:opacity-90"
                            style={{ color: "hsl(214, 5%, 65%)" }}
                          >
                            <span>Thought a bit...</span>
                            <span
                              className="shrink-0 transition-transform"
                              style={{ transform: expandedReasoningIds.has(msg.id) ? "rotate(90deg)" : "none" }}
                              aria-hidden
                            >
                              →
                            </span>
                          </button>
                          {expandedReasoningIds.has(msg.id) && (
                            <div
                              className="whitespace-pre-wrap px-3 pb-3 pt-0 text-sm opacity-90"
                              style={{ borderTop: "1px solid hsl(228, 6%, 22%)" }}
                            >
                              {msg.reasoning}
                            </div>
                          )}
                        </div>
                      )}
                      {msg.content && (
                        <div className="messageContent prose prose-invert max-w-none">
                          <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownComponents}>
                            {normalizeMarkdownBlockNewlines(msg.content)}
                          </ReactMarkdown>
                        </div>
                      )}
                      {msg.reasoning && msg.content && (
                        <div
                          className="messageContent max-w-none mt-2 rounded border-t border-white/10 pt-2"
                          style={{ background: "hsl(228, 10%, 14%)" }}
                        >
                          <button
                            type="button"
                            onClick={() => toggleReasoning(msg.id)}
                            className="flex w-full items-center justify-between gap-2 rounded py-2 px-3 pr-1 text-left text-xs font-medium transition-colors hover:opacity-90"
                            style={{ color: "hsl(214, 5%, 65%)" }}
                          >
                            <span>Thought a bit...</span>
                            <span
                              className="shrink-0 transition-transform"
                              style={{ transform: expandedReasoningIds.has(msg.id) ? "rotate(90deg)" : "none" }}
                              aria-hidden
                            >
                              →
                            </span>
                          </button>
                          {expandedReasoningIds.has(msg.id) && (
                            <div
                              className="whitespace-pre-wrap px-3 pb-3 pt-0 text-xs opacity-80"
                              style={{ borderTop: "1px solid hsl(228, 6%, 22%)" }}
                            >
                              {msg.reasoning}
                            </div>
                          )}
                        </div>
                      )}
                      {msg.widget && renderWidget(msg.widget)}
                    </div>
                  )}
                </div>
              )
            })}
            <div ref={messagesEndRef} />
          </div>
        )}
      </div>

      {/* Input area */}
      <MessageInput placeholder={placeholder} onSend={sendMessage} />
    </div>
  )
}
