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

export async function POST(req: NextRequest) {
  try {
    const body = await req.json()
    const response = await fetch(`${BACKEND_API_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    })

    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      return NextResponse.json(
        data?.error ? { error: data.error } : { error: "Login failed" },
        { status: response.status }
      )
    }

    const authData = data as BackendAuthResponse
    if (!authData.accessToken) {
      return NextResponse.json({ error: "Missing access token" }, { status: 502 })
    }

    const nextResponse = NextResponse.json({
      expiresAtUtc: authData.expiresAtUtc,
      user: authData.user,
    })
    nextResponse.cookies.set(ACCESS_TOKEN_COOKIE_NAME, authData.accessToken, getAuthCookieOptions())

    return nextResponse
  } catch (error) {
    console.error("Error logging in:", error)
    return NextResponse.json({ error: "Internal server error" }, { status: 500 })
  }
}
