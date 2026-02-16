import { NextRequest, NextResponse } from "next/server"
import type { Channel } from "@/lib/agents"

// Backend API URL - configurable via BACKEND_API_URL env var
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

// Backend DTO structure
interface CreateChannelBodyDto {
  clientId: number
  name: string
  description?: string | null
  agentIds: string[]
}

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

export async function POST(req: NextRequest) {
  try {
    const body = await req.json()
    const { name, agentIds } = body

    // Validate required fields
    if (!name?.trim()) {
      return NextResponse.json(
        { error: "Channel name is required" },
        { status: 400 }
      )
    }

    const backendBody: CreateChannelBodyDto = {
      clientId: 1,
      name: name.trim(),
      description: null,
      agentIds: agentIds || [],
    }

    const createUrl = `${BACKEND_API_URL}/api/channels/create`

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
        { error: errorData || "Failed to create channel" },
        { status: response.status }
      )
    }

    const result = await response.json()
    // Backend returns { channelId: "guid" }

    // Fetch the newly created channel to get the full data including dateCreated
    const channelId = result.channelId
    let frontendChannel: Channel

    if (channelId) {
      try {
        const channelResponse = await fetch(`${BACKEND_API_URL}/api/channels?clientId=1`)
        if (channelResponse.ok) {
          const channels: Channel[] = await channelResponse.json()
          const newChannel = channels.find((c) => c.id === channelId)
          if (newChannel) {
            frontendChannel = newChannel
            return NextResponse.json(frontendChannel)
          }
        }
      } catch {
        // Fall through to creating a channel with current timestamp
      }
    }

    // Fallback: create channel with current timestamp
    frontendChannel = {
      id: channelId || crypto.randomUUID(),
      name: name.trim(),
      agentIds: agentIds || [],
      dateCreated: new Date().toISOString(),
    }

    return NextResponse.json(frontendChannel)
  } catch (error) {
    console.error("Error creating channel:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
