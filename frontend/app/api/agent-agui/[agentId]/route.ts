import { NextRequest } from "next/server"
import type { AGUIEvent } from "@ag-ui/core"
import { EventType } from "@ag-ui/core"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"
export const maxDuration = 300

interface ChatMessage {
  role: string
  content: string
}

interface ChatRequest {
  messages: ChatMessage[]
}

export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ agentId: string }> }
) {
  if (!getAccessToken(req)) {
    return new Response(JSON.stringify({ error: "Unauthorized" }), {
      status: 401,
      headers: { "Content-Type": "application/json" },
    })
  }

  const { agentId } = await params
  const body: ChatRequest = await req.json()

  // Convert to AGUI format
  const aguiMessages = body.messages.map((msg) => ({
    id: crypto.randomUUID(),
    role: msg.role,
    content: msg.content,
  }))

  const runAgentInput = {
    threadId: agentId, // Use agentId as threadId for persistence
    runId: crypto.randomUUID(),
    messages: aguiMessages,
    state: null,
    context: [],
    tools: undefined,
  }

  const backendUrl = `${BACKEND_API_URL}/api/agents/${agentId}/agui`

  try {
    const response = await fetch(backendUrl, {
      method: "POST",
      headers: buildBackendHeaders(req),
      body: JSON.stringify(runAgentInput),
      signal: req.signal,
    })

    if (!response.ok) {
      return new Response(
        JSON.stringify({ error: "Backend request failed" }),
        { status: response.status, headers: { "Content-Type": "application/json" } }
      )
    }

    // Stream SSE events (same pattern as channel-agui)
    const reader = response.body?.getReader()
    if (!reader) {
      return new Response(JSON.stringify({ error: "No response body" }), { status: 500 })
    }

    const encoder = new TextEncoder()
    const decoder = new TextDecoder()

    const stream = new ReadableStream({
      async start(controller) {
        let buffer = ""
        try {
          while (true) {
            const { done, value } = await reader.read()
            if (done) break

            buffer += decoder.decode(value, { stream: true })
            const lines = buffer.split("\n")
            buffer = lines.pop() || ""

            for (const line of lines) {
              const trimmed = line.trim()
              if (trimmed.startsWith("data:")) {
                const data = trimmed.slice(5).trim()
                if (!data || data === "[DONE]") continue

                const event: AGUIEvent = JSON.parse(data)
                // Forward all AGUI events
                controller.enqueue(encoder.encode(`data: ${JSON.stringify(event)}\n\n`))
              }
            }
          }
        } finally {
          controller.close()
        }
      },
    })

    return new Response(stream, {
      headers: {
        "Content-Type": "text/event-stream",
        "Cache-Control": "no-cache, no-store",
        Connection: "keep-alive",
      },
    })
  } catch (error) {
    return new Response(JSON.stringify({ error: "Internal server error" }), { status: 500 })
  }
}
