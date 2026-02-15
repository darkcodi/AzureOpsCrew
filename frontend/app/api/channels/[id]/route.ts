import { NextRequest, NextResponse } from "next/server"

// Backend API URL - configurable via BACKEND_API_URL env var
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

export async function DELETE(
  _req: NextRequest,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await params

    if (!id) {
      return NextResponse.json(
        { error: "Channel ID is required" },
        { status: 400 }
      )
    }

    const response = await fetch(`${BACKEND_API_URL}/api/channels/${id}`, {
      method: "DELETE",
    })

    if (!response.ok) {
      const errorData = await response.text()
      return NextResponse.json(
        { error: errorData || "Failed to delete channel" },
        { status: response.status }
      )
    }

    return NextResponse.json({ success: true })
  } catch (error) {
    console.error("Error deleting channel:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
