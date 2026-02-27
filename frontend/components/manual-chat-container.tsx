"use client"

import { useEffect, useRef, useState } from "react"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"
import { MessageInput } from "@/components/message-input"
import { fetchWithErrorHandling } from "@/lib/fetch"
import type { Agent } from "@/lib/agents"
import type { AGUIEvent } from "@ag-ui/core"
import { EventType } from "@ag-ui/core"
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
  role: "user" | "assistant"
  content: string
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
  const [isStreaming, setIsStreaming] = useState(false)
  const [isRunActive, setIsRunActive] = useState(false)
  const [streamingContent, setStreamingContent] = useState("")
  const [streamingWidget, setStreamingWidget] = useState<ChatMessage["widget"] | null>(null)
  const [runError, setRunError] = useState<string | null>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const pendingBackendToolsRef = useRef<Map<string, { name: string; args: string }>>(new Map())

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
  }, [messages, streamingContent, streamingWidget, isRunActive, runError])

  // Load chat history when agent changes
  useEffect(() => {
    if (!activeDMId) {
      setMessages([])
      setRunError(null)
      return
    }

    setIsLoading(true)
    fetchWithErrorHandling(`/api/chat-history/agents/${activeDMId}`)
      .then(async (res) => {
        if (res.ok) {
          const data = (await res.json()) as {
            messages: Array<{
              id: string
              role: "user" | "assistant"
              content: string
              widget?: {
                toolName: string
                callId: string
                args?: Record<string, unknown>
                result?: Record<string, unknown>
              }
            }>
          }
          const chatMessages: ChatMessage[] = data.messages.map((m) => {
            const base: ChatMessage = { id: m.id, role: m.role, content: m.content }
            if (!m.widget) return base
            const w = m.widget
            return {
              ...base,
              widget: {
                toolName: w.toolName,
                callId: w.callId,
                args: w.args ?? {},
                result: w.result ?? {},
              },
            }
          })
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
    if (!activeDMId || isStreaming) return

    // Add user message immediately
    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: "user",
      content,
    }
    setMessages((prev) => [...prev, userMessage])

    // Prepare request with all messages
    const requestMessages = [...messages, userMessage]

    try {
      setIsStreaming(true)
      setStreamingContent("")
      setStreamingWidget(null)
      setRunError(null)
      setIsRunActive(false)
      pendingBackendToolsRef.current.clear()

      const response = await fetch(`/api/agent-agui/${activeDMId}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ messages: requestMessages }),
      })

      if (!response.ok) {
        throw new Error("Failed to send message")
      }

      // Handle SSE stream
      const reader = response.body?.getReader()
      const decoder = new TextDecoder()

      let assistantMessageId = crypto.randomUUID()
      let fullContent = ""
      let currentToolCall: { id: string; name: string; args: string } | null = null

      while (true) {
        const { done, value } = await reader!.read()
        if (done) break

        const chunk = decoder.decode(value)
        const lines = chunk.split("\n")

        for (const line of lines) {
          const trimmed = line.trim()
          if (trimmed.startsWith("data:")) {
            const data = trimmed.slice(5).trim()
            if (!data || data === "[DONE]") continue

            try {
              const event: AGUIEvent = JSON.parse(data)

              // Run started - show typing indicator
              if (event.type === EventType.RUN_STARTED) {
                setIsRunActive(true)
              }

              // Handle text message content
              if (event.type === EventType.TEXT_MESSAGE_CONTENT) {
                const chunkText = (event as { delta?: string }).delta ?? (event as { content?: string }).content ?? ""
                fullContent += chunkText
                setStreamingContent(fullContent)
              }

              // Handle tool call start - track all tools the same way
              if (event.type === EventType.TOOL_CALL_START) {
                const toolEvent = event as { toolCallId: string; toolCallName: string }
                currentToolCall = {
                  id: toolEvent.toolCallId,
                  name: toolEvent.toolCallName,
                  args: "",
                }
                pendingBackendToolsRef.current.set(toolEvent.toolCallId, {
                  name: toolEvent.toolCallName,
                  args: "",
                })
              }

              // Handle tool call args
              if (event.type === EventType.TOOL_CALL_ARGS) {
                if (currentToolCall) {
                  const argsEvent = event as { toolCallId: string; delta: string }
                  currentToolCall.args += argsEvent.delta
                  const pending = pendingBackendToolsRef.current.get(argsEvent.toolCallId)
                  if (pending) pending.args += argsEvent.delta
                }
              }

              // Handle tool call end - no widget push; all tools completed on TOOL_CALL_RESULT
              if (event.type === EventType.TOOL_CALL_END) {
                currentToolCall = null
              }

              // Handle tool call result - push one message for any tool; display resolved by toolName
              if (event.type === EventType.TOOL_CALL_RESULT) {
                const resultEvent = event as { toolCallId: string; content: string }
                const pending = pendingBackendToolsRef.current.get(resultEvent.toolCallId)
                if (pending) {
                  let argsObj: Record<string, unknown> = {}
                  try {
                    if (pending.args.trim()) argsObj = JSON.parse(pending.args) as Record<string, unknown>
                  } catch {
                    // leave empty
                  }
                  let resultObj: Record<string, unknown> = {}
                  try {
                    const raw = resultEvent.content ?? ""
                    if (raw.trim()) {
                      const parsed = JSON.parse(raw) as unknown
                      if (parsed != null && typeof parsed === "object" && !Array.isArray(parsed))
                        resultObj = parsed as Record<string, unknown>
                      else resultObj = { value: raw }
                    }
                  } catch {
                    resultObj = { raw: resultEvent.content ?? "" }
                  }
                  const widgetMessage: ChatMessage = {
                    id: resultEvent.toolCallId,
                    role: "assistant",
                    content: "",
                    widget: {
                      toolName: pending.name,
                      callId: resultEvent.toolCallId,
                      args: argsObj,
                      result: resultObj,
                    },
                  }
                  setMessages((prev) => [...prev, widgetMessage])
                  pendingBackendToolsRef.current.delete(resultEvent.toolCallId)
                }
              }

              // Handle text message end - finalize and append the text-only message (only once)
              if (event.type === EventType.TEXT_MESSAGE_END) {
                const newMessage: ChatMessage = {
                  id: assistantMessageId,
                  role: "assistant",
                  content: fullContent,
                }

                setMessages((prev) => [...prev, newMessage])
                setStreamingContent("")
                setStreamingWidget(null)
              }

              // Handle run finished - only clear streaming state (do not append again)
              if (event.type === EventType.RUN_FINISHED) {
                setStreamingContent("")
                setStreamingWidget(null)
                setIsRunActive(false)
              }

              // Run error - clear typing and show failure in chat
              if (event.type === EventType.RUN_ERROR) {
                setIsRunActive(false)
                setStreamingContent("")
                setStreamingWidget(null)
                const errEvent = event as { message?: string; error?: string }
                const errMessage =
                  errEvent.message ?? errEvent.error ?? "The run failed. Please try again."
                setRunError(errMessage)
                console.error("AGUI run error:", errMessage)
              }
            } catch (e) {
              // Skip unparseable events
            }
          }
        }
      }
    } catch (error) {
      console.error("Error sending message:", error)
      // Could add error state here if needed
    } finally {
      setIsStreaming(false)
      setIsRunActive(false)
    }
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
        {messages.length === 0 && !streamingContent && !streamingWidget && !isRunActive ? (
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
                    className="mb-4 flex items-start justify-end gap-3"
                  >
                    <div className="flex max-w-lg flex-col items-end">
                      <div
                        className="rounded-xl rounded-tr-sm px-4 py-2.5 text-sm leading-relaxed"
                        style={{
                          backgroundColor: "hsl(235, 86%, 65%)",
                          color: "#fff",
                        }}
                      >
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
              const isWidgetOnly = !msg.content && msg.widget
              return (
                <div key={msg.id} className="mb-4 flex items-start gap-3">
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
                      {renderWidget(msg.widget)}
                    </div>
                  ) : (
                    <div
                      className="assistantMessage"
                      style={{
                        color: "hsl(210, 3%, 92%)",
                        background: "hsl(228, 12%, 18%)",
                        border: "1px solid hsl(228, 6%, 28%)",
                        borderRadius: "var(--radius)",
                        padding: "0.75rem 1rem",
                        maxWidth: "100%",
                      }}
                    >
                      {msg.content && (
                        <div className="messageContent prose prose-invert max-w-none">
                          <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownComponents}>
                            {normalizeMarkdownBlockNewlines(msg.content)}
                          </ReactMarkdown>
                        </div>
                      )}
                      {msg.widget && renderWidget(msg.widget)}
                    </div>
                  )}
                </div>
              )
            })}
            {/* Run error banner - shown when RUN_ERROR is received */}
            {runError && (
              <div className="mb-4 flex items-start gap-3">
                <div
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                  style={{
                    backgroundColor: "hsl(0, 60%, 45%)",
                    color: "#fff",
                  }}
                  aria-hidden
                >
                  !
                </div>
                <div
                  className="flex flex-1 flex-col gap-2 rounded-lg border px-4 py-3"
                  style={{
                    backgroundColor: "hsl(0, 40%, 14%)",
                    borderColor: "hsl(0, 50%, 35%)",
                    color: "hsl(0, 0%, 92%)",
                  }}
                >
                  <span className="text-sm font-medium" style={{ color: "hsl(0, 70%, 75%)" }}>
                    Run failed
                  </span>
                  <p className="text-sm leading-relaxed">{runError}</p>
                  <button
                    type="button"
                    onClick={() => setRunError(null)}
                    className="self-start rounded px-2 py-1 text-xs font-medium transition-colors hover:opacity-90"
                    style={{
                      backgroundColor: "hsl(0, 40%, 28%)",
                      color: "hsl(0, 0%, 92%)",
                    }}
                  >
                    Dismiss
                  </button>
                </div>
              </div>
            )}
            {/* Typing indicator when run is active and no streaming content yet */}
            {isRunActive && !streamingContent && (
              <div className="mb-4 flex items-center gap-3">
                <div
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                  style={{
                    backgroundColor:
                      selectedAgent?.color ?? "hsl(214, 5%, 55%)",
                    color: "#fff",
                  }}
                >
                  {selectedAgent?.avatar ?? selectedAgent?.name?.charAt(0).toUpperCase() ?? "?"}
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
                  {(selectedAgent?.name ?? "Agent") + " is typing..."}
                </span>
              </div>
            )}
            {/* Streaming content - text only; tool results are committed as separate messages */}
            {streamingContent && (
              <div className="mb-4 flex items-start gap-3">
                <div
                  className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                  style={{
                    backgroundColor: "hsl(235, 86%, 65%)",
                    color: "#fff",
                  }}
                >
                  {selectedAgent ? selectedAgent.name.charAt(0).toUpperCase() : "A"}
                </div>
                <div
                  className="assistantMessage"
                  style={{
                    color: "hsl(210, 3%, 92%)",
                    background: "hsl(228, 12%, 18%)",
                    border: "1px solid hsl(228, 6%, 28%)",
                    borderRadius: "var(--radius)",
                    padding: "0.75rem 1rem",
                    maxWidth: "100%",
                  }}
                >
                  <div className="messageContent prose prose-invert max-w-none">
                    <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownComponents}>
                      {normalizeMarkdownBlockNewlines(streamingContent)}
                    </ReactMarkdown>
                  </div>
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>
        )}
      </div>

      {/* Input area */}
      <MessageInput placeholder={placeholder} onSend={sendMessage} />
    </div>
  )
}
