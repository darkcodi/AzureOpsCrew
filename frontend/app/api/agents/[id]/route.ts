import { NextRequest, NextResponse } from "next/server"
import type { Agent } from "@/lib/agents"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface BackendAgent {
  id: string
  providerAgentId: string
  clientId: number
  providerId: string
  info: {
    name: string
    prompt: string
    model: string
    description: string | null
    availableTools: string[]
  }
  color: string
  dateCreated: string
}

export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params

    if (!id) {
      return NextResponse.json(
        { error: "Agent ID is required" },
        { status: 400 }
      )
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
        name: name.trim(),
        prompt: systemPrompt?.trim() || `You are ${name.trim()}, a helpful AI assistant.`,
        model: model ?? "",
      },
      providerId,
      color: color ?? "#43b581",
    }

    const response = await fetch(`${BACKEND_API_URL}/api/agents/${id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(backendBody),
    })

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}))
      return NextResponse.json(
        { error: (errorData as { error?: string }).error ?? "Failed to update agent" },
        { status: response.status }
      )
    }

    const backendAgent: BackendAgent = await response.json()

    const frontendAgent: Agent = {
      id: backendAgent.id,
      name: backendAgent.info.name,
      avatar: backendAgent.info.name[0].toUpperCase(),
      color: backendAgent.color,
      systemPrompt: backendAgent.info.prompt,
      model: backendAgent.info.model,
    }

    return NextResponse.json(frontendAgent)
  } catch (error) {
    console.error("Error updating agent:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}

export async function DELETE(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params

    if (!id) {
      return NextResponse.json(
        { error: "Agent ID is required" },
        { status: 400 }
      )
    }

    const response = await fetch(`${BACKEND_API_URL}/api/agents/${id}`, {
      method: "DELETE",
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to delete agent" },
        { status: response.status }
      )
    }

    return NextResponse.json({ success: true })
  } catch (error) {
    console.error("Error deleting agent:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
