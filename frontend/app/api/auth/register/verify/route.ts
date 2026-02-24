import { NextRequest, NextResponse } from "next/server"
import { ACCESS_TOKEN_COOKIE_NAME, getAuthCookieOptions } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface BackendAuthResponse {
  accessToken: string
  expiresAtUtc: string
  user: {
    id: number
    email: string
    displayName: string
  }
}

function isValidBackendAuthResponse(value: unknown): value is BackendAuthResponse {
  if (!value || typeof value !== "object") return false

  const data = value as Record<string, unknown>
  if (typeof data.accessToken !== "string" || data.accessToken.length === 0) return false
  if (typeof data.expiresAtUtc !== "string" || Number.isNaN(Date.parse(data.expiresAtUtc))) return false

  const user = data.user
  if (!user || typeof user !== "object") return false

  const typedUser = user as Record<string, unknown>
  return (
    typeof typedUser.id === "number" &&
    typeof typedUser.email === "string" &&
    typedUser.email.length > 0 &&
    typeof typedUser.displayName === "string"
  )
}

function extractErrorMessage(data: any, fallback: string) {
  if (typeof data?.error === "string") return data.error
  if (typeof data?.Error === "string") return data.Error
  if (data?.errors && typeof data.errors === "object") {
    const first = Object.values(data.errors)[0]
    if (typeof first === "string") return first
  }
  if (data?.Errors && typeof data.Errors === "object") {
    const first = Object.values(data.Errors)[0]
    if (typeof first === "string") return first
  }
  return fallback
}

export async function POST(req: NextRequest) {
  try {
    const body = await req.json()
    const response = await fetch(`${BACKEND_API_URL}/api/auth/register/verify`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    })

    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      return NextResponse.json(
        { error: extractErrorMessage(data, "Verification failed") },
        { status: response.status }
      )
    }

    if (!isValidBackendAuthResponse(data)) {
      return NextResponse.json({ error: "Invalid auth response from backend" }, { status: 502 })
    }

    const authData = data
    const nextResponse = NextResponse.json({
      expiresAtUtc: authData.expiresAtUtc,
      user: authData.user,
    })
    nextResponse.cookies.set(ACCESS_TOKEN_COOKIE_NAME, authData.accessToken, getAuthCookieOptions())

    return nextResponse
  } catch (error) {
    console.error("Error verifying registration code:", error)
    return NextResponse.json({ error: "Internal server error" }, { status: 500 })
  }
}
