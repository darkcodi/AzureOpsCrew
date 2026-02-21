import { NextRequest, NextResponse } from "next/server"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

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
    const response = await fetch(`${BACKEND_API_URL}/api/auth/register/resend`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    })

    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      return NextResponse.json(
        { error: extractErrorMessage(data, "Unable to resend verification code") },
        { status: response.status }
      )
    }

    return NextResponse.json(data)
  } catch (error) {
    console.error("Error resending verification code:", error)
    return NextResponse.json({ error: "Internal server error" }, { status: 500 })
  }
}
