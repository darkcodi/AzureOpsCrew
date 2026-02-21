import { NextRequest, NextResponse } from "next/server"
import type { Channel } from "@/lib/agents"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface CreateChannelBodyDto {
  name: string
  description?: string | null
  agentIds: string[]
}

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
    if (!getAccessToken(req)) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }

    const body = await req.json()
    const { name, agentIds } = body

    if (!name?.trim()) {
      return NextResponse.json(
        { error: "Channel name is required" },
        { status: 400 }
      )
    }

    const backendBody: CreateChannelBodyDto = {
      name: name.trim(),
      description: null,
      agentIds: agentIds || [],
    }

    const response = await fetch(`${BACKEND_API_URL}/api/channels/create`, {
      method: "POST",
      headers: buildBackendHeaders(req),
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
    const channelId = result.channelId

    if (channelId) {
      const channelResponse = await fetch(`${BACKEND_API_URL}/api/channels/${channelId}`, {
        method: "GET",
        headers: buildBackendHeaders(req),
      })

      if (channelResponse.ok) {
        const channel: BackendChannel = await channelResponse.json()
        const frontendChannel: Channel = {
          id: channel.id,
          name: channel.name,
          agentIds: channel.agentIds || [],
          dateCreated: channel.dateCreated,
        }
        return NextResponse.json(frontendChannel)
      }
    }

    const fallbackChannel: Channel = {
      id: channelId || crypto.randomUUID(),
      name: name.trim(),
      agentIds: agentIds || [],
      dateCreated: new Date().toISOString(),
    }

    return NextResponse.json(fallbackChannel)
  } catch (error) {
    console.error("Error creating channel:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
