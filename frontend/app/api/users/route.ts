import { NextRequest, NextResponse } from "next/server"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"
import type { HumanMember } from "@/lib/humans"
import { toHumanCardId } from "@/lib/humans"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface BackendUserPresence {
  id: number
  displayName: string
  isOnline: boolean
  isCurrentUser: boolean
  lastSeenAtUtc: string | null
}

export async function GET(req: NextRequest) {
  try {
    if (!getAccessToken(req)) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
    }

    const response = await fetch(`${BACKEND_API_URL}/api/users`, {
      method: "GET",
      headers: buildBackendHeaders(req),
    })

    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      return NextResponse.json(
        data?.error ? { error: data.error } : { error: "Failed to fetch users" },
        { status: response.status }
      )
    }

    const users = (data as BackendUserPresence[]).map(
      (user): HumanMember => ({
        id: toHumanCardId(user.id),
        userId: user.id,
        name: user.displayName,
        status: user.isOnline ? "Online" : "Offline",
        isCurrentUser: user.isCurrentUser,
      })
    )

    return NextResponse.json(users)
  } catch (error) {
    console.error("Error fetching users:", error)
    return NextResponse.json({ error: "Internal server error" }, { status: 500 })
  }
}
