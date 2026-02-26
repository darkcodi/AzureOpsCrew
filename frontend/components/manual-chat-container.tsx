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
  const [streamingContent, setStreamingContent] = useState("")
  const messagesEndRef = useRef<HTMLDivElement>(null)

  const selectedAgent = agents.find((a) => a.id === activeDMId)
  const placeholder = selectedAgent
    ? `Message @${selectedAgent.name}...`
    : "Message @AzureOpsCrew Assistant..."

  // Scroll to bottom on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" })
  }, [messages, streamingContent])

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

              if (event.type === EventType.TEXT_MESSAGE_CONTENT) {
                const chunk = (event as { delta?: string }).delta ?? (event as { content?: string }).content ?? ""
                fullContent += chunk
                setStreamingContent(fullContent)
              }

              if (event.type === EventType.TEXT_MESSAGE_END) {
                // Finalize the message
                setMessages((prev) => [
                  ...prev,
                  { id: assistantMessageId, role: "assistant", content: fullContent },
                ])
                setStreamingContent("")
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
        {messages.length === 0 && !streamingContent ? (
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

              // Assistant message - on the left with avatar and card styling
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
                        {msg.content}
                      </ReactMarkdown>
                    </div>
                  </div>
                </div>
              )
            })}
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
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Input area */}
      <MessageInput placeholder={placeholder} onSend={sendMessage} />
    </div>
  )
}
