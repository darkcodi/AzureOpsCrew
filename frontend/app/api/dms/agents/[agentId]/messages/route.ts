import { NextRequest, NextResponse } from "next/server"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ agentId: string }> }
) {
  try {
    if (!getAccessToken(req)) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }

    const { agentId } = await params
    const body = await req.json()

    // Get current user's ID
    const meResponse = await fetch(`${BACKEND_API_URL}/api/auth/me`, {
      method: "GET",
      headers: buildBackendHeaders(req),
    })

    if (!meResponse.ok) {
      return NextResponse.json({ error: "Failed to get current user" }, { status: meResponse.status })
    }

    const meData = await meResponse.json().catch(() => ({}))
    const userId = meData.id || meData.userId

    if (!userId) {
      return NextResponse.json({ error: "User ID not found" }, { status: 400 })
    }

    // Proxy to backend DM messages endpoint
    const response = await fetch(
      `${BACKEND_API_URL}/api/users/${userId}/dms/agents/${agentId}/messages`,
      {
        method: "POST",
        headers: buildBackendHeaders(req),
        body: JSON.stringify(body),
      }
    )

    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      return NextResponse.json(
        data?.error ? { error: data.error } : { error: "Failed to send message" },
        { status: response.status }
      )
    }

    return NextResponse.json(data)
  } catch (error) {
    console.error("Error sending DM message:", error)
    return NextResponse.json({ error: "Internal server error" }, { status: 500 })
  }
}
