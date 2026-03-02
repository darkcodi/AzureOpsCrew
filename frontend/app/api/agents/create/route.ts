import { NextRequest, NextResponse } from "next/server"
import type { Agent } from "@/lib/agents"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface BackendAgent {
  id: string
  providerAgentId: string
  clientId: number
  providerId: string
  info: {
    username: string
    prompt: string
    model: string
    description: string | null
    availableTools: string[]
  }
  color: string
  dateCreated: string
}

export async function POST(req: NextRequest) {
  try {
    if (!getAccessToken(req)) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }

    const body = await req.json()
    const { name, model, systemPrompt, color, providerId } = body

    if (!name?.trim()) {
      return NextResponse.json(
        { error: "Agent name is required" },
        { status: 400 }
      )
    }

    if (!providerId) {
      return NextResponse.json(
        { error: "Provider ID is required" },
        { status: 400 }
      )
    }

    const backendBody = {
      info: {
        username: name.trim(),
        prompt: systemPrompt?.trim() || `You are ${name.trim()}, a helpful AI assistant.`,
        model,
      },
      providerId,
      color: color || "#43b581",
    }

    const response = await fetch(`${BACKEND_API_URL}/api/agents/create`, {
      method: "POST",
      headers: buildBackendHeaders(req),
      body: JSON.stringify(backendBody),
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to create agent" },
        { status: response.status }
      )
    }

    const backendAgent: BackendAgent = await response.json()

    const frontendAgent: Agent = {
      id: backendAgent.id,
      name: backendAgent.info.username,
      avatar: backendAgent.info.username[0].toUpperCase(),
      color: backendAgent.color,
      systemPrompt: backendAgent.info.prompt,
      model: backendAgent.info.model,
    }

    return NextResponse.json(frontendAgent)
  } catch (error) {
    console.error("Error creating agent:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
