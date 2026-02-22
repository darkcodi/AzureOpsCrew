import { NextRequest, NextResponse } from "next/server"
import type { Channel } from "@/lib/agents"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

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
    if (!getAccessToken(req)) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }

    const response = await fetch(`${BACKEND_API_URL}/api/channels`, {
      method: "GET",
      headers: buildBackendHeaders(req),
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to fetch channels" },
        { status: response.status }
      )
    }

    const backendChannels: BackendChannel[] = await response.json()

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
