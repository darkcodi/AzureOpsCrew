import { streamText } from "ai"
import { defaultAgents } from "@/lib/agents"

export const maxDuration = 60

export async function POST(req: Request) {
  const {
    messages,
    agentId,
    customSystemPrompt,
    customModel,
  }: {
    messages: { role: string; content: string }[]
    agentId: string
    customSystemPrompt?: string
    customModel?: string
  } = await req.json()

  const agent = defaultAgents.find((a) => a.id === agentId)

  const systemPrompt =
    agent?.systemPrompt ?? customSystemPrompt ?? "You are a helpful AI assistant."
  const model = agent?.model ?? customModel ?? "openai/gpt-4o-mini"

  const result = streamText({
    model,
    system: systemPrompt,
    messages: messages.map((m) => ({
      role: m.role as "user" | "assistant" | "system",
      content: m.content,
    })),
    abortSignal: req.signal,
  })

  return result.toUIMessageStreamResponse()
}
