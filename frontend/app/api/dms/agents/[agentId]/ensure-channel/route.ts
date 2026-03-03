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

    // Proxy to backend ensure-channel endpoint
    const response = await fetch(
      `${BACKEND_API_URL}/api/dms/agents/${agentId}/ensure-channel`,
      {
        method: "POST",
        headers: buildBackendHeaders(req),
      }
    )

    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      return NextResponse.json(
        data?.error ? { error: data.error } : { error: "Failed to ensure DM channel" },
        { status: response.status }
      )
    }

    return NextResponse.json(data)
  } catch (error) {
    console.error("Error ensuring DM channel:", error)
    return NextResponse.json({ error: "Internal server error" }, { status: 500 })
  }
}
