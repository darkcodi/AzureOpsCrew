import { NextRequest, NextResponse } from "next/server"
import type { Room } from "@/lib/agents"

// Backend API URL - configurable via BACKEND_API_URL env var
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

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

export async function GET(req: NextRequest) {
  try {
    const roomsUrl = `${BACKEND_API_URL}/api/chats?clientId=1`

    const response = await fetch(roomsUrl, {
      method: "GET",
      headers: {
        "Content-Type": "application/json",
      },
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to fetch rooms" },
        { status: response.status }
      )
    }

    const backendChats: BackendChat[] = await response.json()

    // Transform backend chats to frontend Room interface
    const frontendRooms: Room[] = backendChats.map((chat) => ({
      id: chat.id,
      name: chat.name,
      agentIds: chat.agentIds || [],
    }))

    return NextResponse.json(frontendRooms)
  } catch (error) {
    console.error("Error fetching rooms:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
