import { NextRequest } from "next/server"
import { AGUIEvent, AGUI_EVENT_TYPES } from "@/lib/types/agui"

// Backend API URL - configurable via BACKEND_API_URL env var
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

export const maxDuration = 300

interface ChatMessage {
  role: string
  content: string
}

interface ChatRequest {
  messages: ChatMessage[]
  agentId: string
  customSystemPrompt?: string
  customModel?: string
}

/**
 * POST /api/channel-agui/[channelId]
 *
 * Proxies requests to the backend AGUI endpoint at /api/channels/{channelId}/agui
 */
export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ channelId: string }> }
) {
  const { channelId } = await params

  // Parse request body
  const body: ChatRequest = await req.json()

  // Map frontend chat messages to AGUI message format
  const aguiMessages = body.messages.map((msg) => ({
    id: crypto.randomUUID(),
    role: msg.role,
    content: msg.content,
  }))

  // Prepare AGUI request payload (RunAgentInput format)
  const runAgentInput = {
    threadId: crypto.randomUUID(),
    runId: crypto.randomUUID(),
    messages: aguiMessages,
    state: null,
    context: [],
    // tools could be passed from client in the future
    tools: undefined,
  }

  // Build backend AGUI URL
  const backendUrl = `${BACKEND_API_URL}/api/channels/${channelId}/agui`

  try {
    // Forward request to backend
    const response = await fetch(backendUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      // Forward authorization header if present
        ...(req.headers.get("authorization")
          ? { Authorization: req.headers.get("authorization")! }
          : {}),
      },
      body: JSON.stringify(runAgentInput),
      // Forward abort signal for cancellation
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

    // Check if response is SSE (text/event-stream)
    const contentType = response.headers.get("content-type")
    const isSSE = contentType?.includes("text/event-stream")

    if (!isSSE) {
      // If not SSE, just return the response as-is
      return new Response(response.body, {
        headers: {
          "Content-Type": contentType ?? "application/json",
        },
      })
    }

    // Stream SSE events and transform to frontend-compatible format
    const reader = response.body?.getReader()
    if (!reader) {
      return new Response(JSON.stringify({ error: "No response body" }), {
        status: 500,
        headers: { "Content-Type": "application/json" },
      })
    }

    const encoder = new TextEncoder()
    const decoder = new TextDecoder()

    // Create a readable stream for transformed events
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

                  // Transform AGUI events to frontend-compatible format
                  // TextMessageContentEvent -> text-delta format
                  if (event.type === AGUI_EVENT_TYPES.TEXT_MESSAGE_CONTENT) {
                    const delta = (event as any).delta
                    if (delta) {
                      controller.enqueue(
                        encoder.encode(`data: ${JSON.stringify({ type: "text-delta", textDelta: delta })}\n\n`)
                      )
                    }
                  }

                  // For now, also forward tool events and other events as-is
                  // The frontend can be extended to handle these
                  if (
                    event.type === AGUI_EVENT_TYPES.TOOL_CALL_START ||
                    event.type === AGUI_EVENT_TYPES.TOOL_CALL_ARGS ||
                    event.type === AGUI_EVENT_TYPES.TOOL_CALL_END ||
                    event.type === AGUI_EVENT_TYPES.TOOL_CALL_RESULT
                  ) {
                    controller.enqueue(
                      encoder.encode(`data: ${JSON.stringify(event)}\n\n`)
                    )
                  }

                  // Forward run events for monitoring
                  if (
                    event.type === AGUI_EVENT_TYPES.RUN_STARTED ||
                    event.type === AGUI_EVENT_TYPES.RUN_FINISHED ||
                    event.type === AGUI_EVENT_TYPES.RUN_ERROR
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
        "Connection": "keep-alive",
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
