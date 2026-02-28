"use client"

import { useEffect, useRef, useState } from "react"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import {
  type ChatMessage,
  markdownComponents,
  normalizeMarkdownBlockNewlines,
  renderMessageWidget,
} from "@/components/manual-chat-container"
import { fetchWithErrorHandling } from "@/lib/fetch"
import type { Agent } from "@/lib/agents"

interface AgentMindModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  agentId: string
  agent: Agent
}

export function AgentMindModal({
  open,
  onOpenChange,
  agentId,
  agent,
}: AgentMindModalProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const scrollRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open || !agentId) {
      setMessages([])
      return
    }
    setIsLoading(true)
    fetchWithErrorHandling(`/api/chat-history/agents/${agentId}`)
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
      .catch(() => setMessages([]))
      .finally(() => setIsLoading(false))
  }, [open, agentId])

  useEffect(() => {
    if (!scrollRef.current || messages.length === 0) return
    const el = scrollRef.current
    const id = requestAnimationFrame(() => {
      el.scrollTop = el.scrollHeight
    })
    return () => cancelAnimationFrame(id)
  }, [messages])

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="flex h-[90vh] max-h-[90vh] w-[95vw] !max-w-[95vw] flex-col gap-0 border p-0"
        style={{
          backgroundColor: "hsl(228, 6%, 22%)",
          borderColor: "hsl(228, 6%, 28%)",
        }}
      >
        <DialogHeader className="shrink-0 border-b px-6 py-4" style={{ borderColor: "hsl(228, 6%, 28%)" }}>
          <DialogTitle className="text-left text-lg font-semibold" style={{ color: "hsl(0, 0%, 100%)" }}>
            Agent Mind — {agent.name}
          </DialogTitle>
        </DialogHeader>

        <div
          ref={scrollRef}
          className="min-h-0 flex-1 overflow-y-auto px-6 py-4"
        >
          {isLoading ? (
            <div className="flex items-center justify-center py-12" style={{ color: "hsl(214, 5%, 55%)" }}>
              Loading conversation...
            </div>
          ) : messages.length === 0 ? (
            <div className="flex items-center justify-center py-12" style={{ color: "hsl(214, 5%, 55%)" }}>
              No messages yet.
            </div>
          ) : (
            <div className="flex flex-col">
              {messages.map((msg) => {
                if (msg.role === "user") {
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
                          <div
                            className="mb-1.5 text-xs font-semibold"
                            style={{ color: "hsl(195, 80%, 85%)" }}
                          >
                            You
                          </div>
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

                const isWidgetOnly = !msg.content && msg.widget
                return (
                  <div key={msg.id} className="mb-4 flex items-start gap-3">
                    <div
                      className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
                      style={{
                        backgroundColor: agent.color ?? "hsl(235, 86%, 65%)",
                        color: "#fff",
                      }}
                    >
                      {agent.name.charAt(0).toUpperCase()}
                    </div>
                    {isWidgetOnly ? (
                      <div className="min-w-0 max-w-3xl">
                        <div
                          className="mb-1.5 text-xs font-semibold"
                          style={{
                            color: agent.color ?? "hsl(270, 55%, 78%)",
                          }}
                        >
                          {agent.name}
                        </div>
                        {renderMessageWidget(msg.widget)}
                      </div>
                    ) : (
                      <div
                        className="assistantMessage min-w-0 max-w-3xl"
                        style={{
                          color: "hsl(210, 3%, 92%)",
                          background: "hsl(228, 12%, 18%)",
                          border: "1px solid hsl(228, 6%, 28%)",
                          borderRadius: "var(--radius)",
                          padding: "0.75rem 1rem",
                          maxWidth: "100%",
                        }}
                      >
                        <div
                          className="mb-1.5 text-xs font-semibold"
                          style={{
                            color: agent.color ?? "hsl(270, 55%, 78%)",
                          }}
                        >
                          {agent.name}
                        </div>
                        {msg.content && (
                          <div className="messageContent prose prose-invert max-w-none">
                            <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownComponents}>
                              {normalizeMarkdownBlockNewlines(msg.content)}
                            </ReactMarkdown>
                          </div>
                        )}
                        {msg.widget && renderMessageWidget(msg.widget)}
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}
