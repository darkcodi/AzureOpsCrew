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
  customSystemPrompt?: string
  customModel?: string
}

export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ channelId: string }> }
) {
  if (!getAccessToken(req)) {
    return new Response(JSON.stringify({ error: "Unauthorized" }), {
      status: 401,
      headers: { "Content-Type": "application/json" },
    })
  }

  const { channelId } = await params
  const body: ChatRequest = await req.json()

  const aguiMessages = body.messages.map((msg) => ({
    id: crypto.randomUUID(),
    role: msg.role,
    content: msg.content,
  }))

  const runAgentInput = {
    threadId: crypto.randomUUID(),
    runId: crypto.randomUUID(),
    messages: aguiMessages,
    state: null,
    context: [],
    tools: undefined,
  }

  const backendUrl = `${BACKEND_API_URL}/api/channels/${channelId}/agui`

  try {
    const response = await fetch(backendUrl, {
      method: "POST",
      headers: buildBackendHeaders(req),
      body: JSON.stringify(runAgentInput),
      signal: req.signal,
    })

    if (!response.ok) {
      const errorText = await response.text()
      console.error(`Backend AGUI error: ${response.status} ${errorText}`)
      return new Response(
        JSON.stringify({ error: "Backend request failed", status: response.status }),
        { status: response.status, headers: { "Content-Type": "application/json" } }
      )
    }

    const contentType = response.headers.get("content-type")
    const isSSE = contentType?.includes("text/event-stream")

    if (!isSSE) {
      return new Response(response.body, {
        headers: {
          "Content-Type": contentType ?? "application/json",
        },
      })
    }

    const reader = response.body?.getReader()
    if (!reader) {
      return new Response(JSON.stringify({ error: "No response body" }), {
        status: 500,
        headers: { "Content-Type": "application/json" },
      })
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

                try {
                  const event: AGUIEvent = JSON.parse(data)

                  if (event.type === EventType.TEXT_MESSAGE_START) {
                    controller.enqueue(
                      encoder.encode(`data: ${JSON.stringify(event)}\n\n`)
                    )
                  }

                  if (event.type === EventType.TEXT_MESSAGE_CONTENT) {
                    controller.enqueue(
                      encoder.encode(`data: ${JSON.stringify(event)}\n\n`)
                    )
                  }

                  if (event.type === EventType.TEXT_MESSAGE_END) {
                    controller.enqueue(
                      encoder.encode(`data: ${JSON.stringify(event)}\n\n`)
                    )
                  }

                  if (
                    event.type === EventType.TOOL_CALL_START ||
                    event.type === EventType.TOOL_CALL_ARGS ||
                    event.type === EventType.TOOL_CALL_END ||
                    event.type === EventType.TOOL_CALL_RESULT
                  ) {
                    controller.enqueue(
                      encoder.encode(`data: ${JSON.stringify(event)}\n\n`)
                    )
                  }

                  if (
                    event.type === EventType.RUN_STARTED ||
                    event.type === EventType.RUN_FINISHED ||
                    event.type === EventType.RUN_ERROR
                  ) {
                    controller.enqueue(
                      encoder.encode(`data: ${JSON.stringify(event)}\n\n`)
                    )
                  }
                } catch (parseError) {
                  console.error("Failed to parse AGUI event:", parseError)
                }
              }
            }
          }
        } catch (error) {
          if ((error as Error)?.name !== "AbortError") {
            console.error("Stream error:", error)
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
    console.error("AGUI proxy error:", error)
    return new Response(JSON.stringify({ error: "Internal server error" }), {
      status: 500,
      headers: { "Content-Type": "application/json" },
    })
  }
}
