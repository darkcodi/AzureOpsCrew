import { NextRequest, NextResponse } from "next/server"
import type { Agent } from "@/lib/agents"

// Backend API URL - configurable via BACKEND_API_URL env var
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

// Backend response structure
interface BackendAgent {
  id: string
  providerAgentId: string
  clientId: number
  info: {
    name: string
    prompt: string
    model: string
    description: string | null
    avaliableTools: string[]
  }
  provider: number
  color: string
  dateCreated: string
}

export async function GET(req: NextRequest) {
  try {
    const agentsUrl = `${BACKEND_API_URL}/api/agents?clientId=1`

    const response = await fetch(agentsUrl, {
      method: "GET",
      headers: {
        "Content-Type": "application/json",
      },
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to fetch agents" },
        { status: response.status }
      )
    }

    const backendAgents: BackendAgent[] = await response.json()

    // Transform backend agents to frontend Agent interface
    const frontendAgents: Agent[] = backendAgents.map((backendAgent) => ({
      id: backendAgent.id,
      name: backendAgent.info.name,
      avatar: backendAgent.info.name[0].toUpperCase(),
      color: backendAgent.color,
      systemPrompt: backendAgent.info.prompt,
      model: backendAgent.info.model,
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
