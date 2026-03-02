import { NextRequest, NextResponse } from "next/server"
import type { Agent } from "@/lib/agents"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface BackendAgent {
  id: string
  providerAgentId: string
  clientId: number
  info: {
    username: string
    prompt: string
    model: string
    description: string | null
    availableTools: string[]
  }
  provider: number
  color: string
  dateCreated: string
}

export async function GET(req: NextRequest) {
  try {
    if (!getAccessToken(req)) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }

    const response = await fetch(`${BACKEND_API_URL}/api/agents`, {
      method: "GET",
      headers: buildBackendHeaders(req),
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to fetch agents" },
        { status: response.status }
      )
    }

    const backendAgents: BackendAgent[] = await response.json()

    const frontendAgents: Agent[] = backendAgents.map((backendAgent) => ({
      id: backendAgent.id,
      name: backendAgent.info.username,
      avatar: backendAgent.info.username[0].toUpperCase(),
      color: backendAgent.color,
      systemPrompt: backendAgent.info.prompt,
      model: backendAgent.info.model,
      dateCreated: backendAgent.dateCreated,
    }))

    return NextResponse.json(frontendAgents)
  } catch (error) {
    console.error("Error fetching agents:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
