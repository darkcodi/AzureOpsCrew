import { NextRequest, NextResponse } from "next/server"
import type { Channel } from "@/lib/agents"

// Backend API URL - configurable via BACKEND_API_URL env var
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

// Backend response structure
interface BackendChannel {
  id: string
  clientId: number
  name: string
  description: string | null
  conversationId: string | null
  agentIds: string[]
  dateCreated: string
}

export async function GET(req: NextRequest) {
  try {
    const channelsUrl = `${BACKEND_API_URL}/api/channels?clientId=1`

    const response = await fetch(channelsUrl, {
      method: "GET",
      headers: {
        "Content-Type": "application/json",
      },
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to fetch channels" },
        { status: response.status }
      )
    }

    const backendChannels: BackendChannel[] = await response.json()

    // Transform backend channels to frontend Channel interface
    const frontendChannels: Channel[] = backendChannels.map((channel) => ({
      id: channel.id,
      name: channel.name,
      agentIds: channel.agentIds || [],
      dateCreated: channel.dateCreated,
    }))

    return NextResponse.json(frontendChannels)
  } catch (error) {
    console.error("Error fetching channels:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
