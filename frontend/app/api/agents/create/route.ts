import { NextRequest, NextResponse } from "next/server"

// Backend API URL - configurable via BACKEND_API_URL env var
// Defaults to localhost:5000 for local development
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

// Provider enum values matching backend
enum Provider {
  Local0 = 0,
  Local1 = 1,
  MicrosoftFoundry = 10,
}

export async function POST(req: NextRequest) {
  try {
    const body = await req.json()
    const { name, model, systemPrompt, color } = body

    // Validate required fields
    if (!name?.trim()) {
      return NextResponse.json(
        { error: "Agent name is required" },
        { status: 400 }
      )
    }

    // Backend expects CreateAgentBodyDto structure:
    // { info: { name, prompt, model }, clientId, provider }
    const backendBody = {
      info: {
        name: name.trim(),
        prompt: systemPrompt?.trim() || `You are ${name.trim()}, a helpful AI assistant.`,
        model,
      },
      clientId: 1, // Default client ID
      provider: Provider.Local0, // Default provider
    }

    const createUrl = `${BACKEND_API_URL}/api/agents/create`

    const response = await fetch(createUrl, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(backendBody),
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to create agent" },
        { status: response.status }
      )
    }

    const data = await response.json()
    return NextResponse.json(data)
  } catch (error) {
    console.error("Error creating agent:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
