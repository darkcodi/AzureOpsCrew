import { NextRequest, NextResponse } from "next/server"
import type { Room } from "@/lib/agents"

// Backend API URL - configurable via BACKEND_API_URL env var
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

// Backend DTO structure
interface CreateChatBodyDto {
  clientId: number
  name: string
  description?: string | null
  agentIds: string[]
}

// Backend response structure
interface BackendChat {
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
        { error: "Room name is required" },
        { status: 400 }
      )
    }

    const backendBody: CreateChatBodyDto = {
      clientId: 1,
      name: name.trim(),
      description: null,
      agentIds: agentIds || [],
    }

    const createUrl = `${BACKEND_API_URL}/api/chats/create`

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
        { error: errorData || "Failed to create room" },
        { status: response.status }
      )
    }

    const result = await response.json()
    // Backend returns { chatId: "guid" }

    // Return the room in frontend format
    const frontendRoom: Room = {
      id: result.chatId || crypto.randomUUID(),
      name: name.trim(),
      agentIds: agentIds || [],
    }

    return NextResponse.json(frontendRoom)
  } catch (error) {
    console.error("Error creating room:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
