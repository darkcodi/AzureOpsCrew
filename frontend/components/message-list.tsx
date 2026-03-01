"use client"

import { useEffect, useRef } from "react"
import type { Agent, ChatMessage } from "@/lib/agents"
import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import { StartConversationEmpty } from "@/components/start-conversation-empty"

function formatTime(date: Date) {
  return date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
}

function normalizeMarkdownBlockNewlines(content: string): string {
  if (!content || typeof content !== "string") return content
  let out = content.replace(/(#{1,6}\s[^\n]*?)(\s+)(?=[A-Z*_`-])/g, "$1\n\n$2")
  out = out.replace(/\s+---\s+/g, "\n\n---\n\n")
  return out
}

interface MessageListProps {
  messages: ChatMessage[]
  agents: Agent[]
  streamingAgentId: string | null
  streamingContent: string
}

export function MessageList({
  messages,
  agents,
  streamingAgentId,
  streamingContent,
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
        return (
          <div key={message.id} className="mb-4 flex items-start gap-3">
            <div
              className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xs font-bold"
              style={{
                backgroundColor: agent?.color ?? "hsl(214, 5%, 55%)",
                color: "#fff",
              }}
            >
              {agent?.avatar ?? "?"}
            </div>
            <div className="min-w-0 flex-1">
              <div className="mb-1 flex items-center gap-2">
                <span
                  className="text-sm font-medium"
                  style={{ color: "hsl(0, 0%, 100%)" }}
                >
                  {agent?.name ?? "Agent"}
                </span>
                <span
                  className="text-xs"
                  style={{ color: "hsl(214, 5%, 55%)" }}
                >
                  {formatTime(message.timestamp)}
                </span>
              </div>
              <div
                className="max-w-2xl text-sm leading-relaxed"
                style={{ color: "hsl(210, 3%, 83%)" }}
              >
                <ReactMarkdown
                  remarkPlugins={[remarkGfm]}
                  components={{
                    p: ({ children }) => (
                      <p className="mb-2 last:mb-0">{children}</p>
                    ),
                    h1: ({ children, ...props }) => (
                      <h1 className="mb-2 mt-4 text-xl font-bold first:mt-0" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h1>
                    ),
                    h2: ({ children, ...props }) => (
                      <h2 className="mb-2 mt-3 text-lg font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h2>
                    ),
                    h3: ({ children, ...props }) => (
                      <h3 className="mb-2 mt-2 text-base font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h3>
                    ),
                    h4: ({ children, ...props }) => (
                      <h4 className="mb-1 mt-2 text-sm font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h4>
                    ),
                    h5: ({ children, ...props }) => (
                      <h5 className="mb-1 mt-1 text-sm font-medium" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h5>
                    ),
                    h6: ({ children, ...props }) => (
                      <h6 className="mb-1 mt-1 text-xs font-medium" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h6>
                    ),
                    hr: (props) => (
                      <hr className="my-3 border-0" style={{ borderTop: "1px solid hsl(228, 6%, 28%)" }} {...props} />
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
                    table: ({ children, ...props }) => (
                      <div className="my-2 overflow-x-auto rounded-md" style={{ border: "1px solid hsl(228, 6%, 20%)" }}>
                        <table className="w-full border-collapse text-sm" {...props}>{children}</table>
                      </div>
                    ),
                    thead: ({ children, ...props }) => (
                      <thead style={{ backgroundColor: "hsl(228, 7%, 12%)" }} {...props}>{children}</thead>
                    ),
                    tbody: ({ children, ...props }) => <tbody {...props}>{children}</tbody>,
                    tr: ({ children, ...props }) => (
                      <tr style={{ borderBottom: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</tr>
                    ),
                    th: ({ children, ...props }) => (
                      <th className="px-3 py-2 text-left font-semibold" style={{ color: "hsl(0, 0%, 100%)", borderRight: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</th>
                    ),
                    td: ({ children, ...props }) => (
                      <td className="px-3 py-2" style={{ borderRight: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</td>
                    ),
                  }}
                >
                  {normalizeMarkdownBlockNewlines(message.content)}
                </ReactMarkdown>
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
                remarkPlugins={[remarkGfm]}
                components={{
                  p: ({ children }) => (
                    <p className="mb-2 last:mb-0">{children}</p>
                  ),
                  h1: ({ children, ...props }) => (
                    <h1 className="mb-2 mt-4 text-xl font-bold first:mt-0" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h1>
                  ),
                  h2: ({ children, ...props }) => (
                    <h2 className="mb-2 mt-3 text-lg font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h2>
                  ),
                  h3: ({ children, ...props }) => (
                    <h3 className="mb-2 mt-2 text-base font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h3>
                  ),
                  h4: ({ children, ...props }) => (
                    <h4 className="mb-1 mt-2 text-sm font-semibold" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h4>
                  ),
                  h5: ({ children, ...props }) => (
                    <h5 className="mb-1 mt-1 text-sm font-medium" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h5>
                  ),
                  h6: ({ children, ...props }) => (
                    <h6 className="mb-1 mt-1 text-xs font-medium" style={{ color: "hsl(0, 0%, 100%)" }} {...props}>{children}</h6>
                  ),
                  hr: (props) => (
                    <hr className="my-3 border-0" style={{ borderTop: "1px solid hsl(228, 6%, 28%)" }} {...props} />
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
                  table: ({ children, ...props }) => (
                    <div className="my-2 overflow-x-auto rounded-md" style={{ border: "1px solid hsl(228, 6%, 20%)" }}>
                      <table className="w-full border-collapse text-sm" {...props}>{children}</table>
                    </div>
                  ),
                  thead: ({ children, ...props }) => (
                    <thead style={{ backgroundColor: "hsl(228, 7%, 12%)" }} {...props}>{children}</thead>
                  ),
                  tbody: ({ children, ...props }) => <tbody {...props}>{children}</tbody>,
                  tr: ({ children, ...props }) => (
                    <tr style={{ borderBottom: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</tr>
                  ),
                  th: ({ children, ...props }) => (
                    <th className="px-3 py-2 text-left font-semibold" style={{ color: "hsl(0, 0%, 100%)", borderRight: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</th>
                  ),
                  td: ({ children, ...props }) => (
                    <td className="px-3 py-2" style={{ borderRight: "1px solid hsl(228, 6%, 20%)" }} {...props}>{children}</td>
                  ),
                }}
              >
                {normalizeMarkdownBlockNewlines(streamingContent)}
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
