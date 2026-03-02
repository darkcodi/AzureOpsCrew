import { NextRequest, NextResponse } from "next/server"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"
import type { IpInfo } from "@/components/my-ip-card"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface ChatHistoryMessage {
  id: string
  role: "user" | "assistant"
  content: string
  timestamp: string
  widget?:
    | { toolName: "showMyIp"; data: IpInfo }
    | { toolName: "showDeployment"; data?: null | object }
}

interface ChatHistoryResponse {
  messages: ChatHistoryMessage[]
}

export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ agentId: string }> }
) {
  try {
    if (!getAccessToken(req)) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }

    const { agentId } = await params
    const response = await fetch(`${BACKEND_API_URL}/api/agents/${agentId}/mind`, {
      method: "GET",
      headers: buildBackendHeaders(req),
    })

    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      return NextResponse.json(
        data?.error ? { error: data.error } : { error: "Failed to fetch chat history" },
        { status: response.status }
      )
    }

    return NextResponse.json(data)
  } catch (error) {
    console.error("Error fetching chat history:", error)
    return NextResponse.json({ error: "Internal server error" }, { status: 500 })
  }
}
