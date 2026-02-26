"use client"

import { useEffect, useRef, useState } from "react"
import ReactMarkdown from "react-markdown"
import { useAgentRuntime } from "@/contexts/agent-runtime-context"
import { MessageInput } from "@/components/message-input"
import { fetchWithErrorHandling } from "@/lib/fetch"
import type { Agent } from "@/lib/agents"
import type { AGUIEvent } from "@ag-ui/core"
import { EventType } from "@ag-ui/core"
import { StartConversationEmpty } from "@/components/start-conversation-empty"
import { DeploymentCard } from "@/components/deployment-card"
import { MyIpCard, type IpInfo } from "@/components/my-ip-card"

const DM_EMPTY_SUBTITLE = "Send a message to get started."

// Markdown components for consistent styling
const markdownComponents = {
  p: ({ children, ...props }: any) => (
    <p className="mb-2 last:mb-0" {...props}>{children}</p>
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
}

export interface ChatMessage {
  id: string
  role: "user" | "assistant"
  content: string
  widget?:
    | { toolName: "showMyIp"; data: IpInfo }
    | { toolName: "showDeployment"; data?: never }
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
  }, [messages, streamingContent, streamingWidget, isRunActive])

  // Load chat history when agent changes
  useEffect(() => {
    if (!activeDMId) {
      setMessages([])
      return
    }

    setIsLoading(true)
    fetchWithErrorHandling(`/api/chat-history/agents/${activeDMId}`)
      .then(async (res) => {
        if (res.ok) {
          const data = await res.json() as { messages: ChatMessage[] }
          setMessages(data.messages)
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
      setIsRunActive(false)

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

              // Handle tool call start
              if (event.type === EventType.TOOL_CALL_START) {
                const toolEvent = event as { toolCallId: string; toolCallName: string }
                currentToolCall = {
                  id: toolEvent.toolCallId,
                  name: toolEvent.toolCallName,
                  args: "",
                }
              }

              // Handle tool call args
              if (event.type === EventType.TOOL_CALL_ARGS) {
                if (currentToolCall) {
                  const argsEvent = event as { toolCallId: string; delta: string }
                  currentToolCall.args += argsEvent.delta
                }
              }

              // Handle tool call end - push widget as its own message (do not embed in text message)
              if (event.type === EventType.TOOL_CALL_END) {
                if (currentToolCall?.name === "showMyIp") {
                  try {
                    const args = JSON.parse(currentToolCall.args)
                    const widgetMessage: ChatMessage = {
                      id: currentToolCall.id,
                      role: "assistant",
                      content: "",
                    widget: { toolName: "showMyIp", data: args as IpInfo },
                    }
                    setMessages((prev) => [...prev, widgetMessage])
                  } catch (e) {
                    console.error("Failed to parse showMyIp args:", e)
                  }
                } else if (currentToolCall?.name === "showDeployment") {
                  const widgetMessage: ChatMessage = {
                    id: currentToolCall.id,
                    role: "assistant",
                    content: "",
                    widget: { toolName: "showDeployment" },
                  }
                  setMessages((prev) => [...prev, widgetMessage])
                }
                currentToolCall = null
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

              // Run error - clear typing
              if (event.type === EventType.RUN_ERROR) {
                setIsRunActive(false)
                console.error("AGUI run error:", (event as { message?: string }).message)
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

  // Render a widget based on its type
  const renderWidget = (widget: ChatMessage["widget"]) => {
    if (!widget) return null

    switch (widget.toolName) {
      case "showMyIp":
        return <MyIpCard ipInfo={widget.data} onFollowUp={sendMessage} />
      case "showDeployment":
        return <DeploymentCard onFollowUp={sendMessage} />
      default:
        return null
    }
  }

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
      <div className="copilotKitMessages min-h-0 flex-1 flex flex-col">
        {messages.length === 0 && !streamingContent && !streamingWidget && !isRunActive ? (
          <div className="copilotKitMessagesContainer min-h-0 flex-1 flex flex-col justify-center">
            <StartConversationEmpty subtitle={DM_EMPTY_SUBTITLE} />
          </div>
        ) : (
          <div className="copilotKitMessagesContainer overflow-y-auto px-4 py-4">
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
                      className="copilotKitAssistantMessage"
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
                          <ReactMarkdown components={markdownComponents}>
                            {msg.content}
                          </ReactMarkdown>
                        </div>
                      )}
                      {msg.widget && renderWidget(msg.widget)}
                    </div>
                  )}
                </div>
              )
            })}
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
                  className="copilotKitAssistantMessage"
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
                    <ReactMarkdown components={markdownComponents}>
                      {streamingContent}
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
