import { NextRequest, NextResponse } from "next/server"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

export async function GET(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params

    if (!id) {
      return NextResponse.json(
        { error: "Provider ID is required" },
        { status: 400 }
      )
    }

    const response = await fetch(
      `${BACKEND_API_URL}/api/providers/${id}/models`,
      { headers: { "Content-Type": "application/json" } }
    )

    if (!response.ok) {
      const text = await response.text()
      return NextResponse.json(
        { error: text || "Failed to fetch models" },
        { status: response.status }
      )
    }

    const models = await response.json()
    return NextResponse.json(models)
  } catch (error) {
    console.error("Error fetching provider models:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
