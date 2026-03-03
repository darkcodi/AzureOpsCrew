import { NextRequest, NextResponse } from "next/server"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

// POST /api/runs/[id]/approve — approve or deny an approval request
export async function POST(
    req: NextRequest,
    { params }: { params: Promise<{ id: string }> }
) {
    try {
        if (!getAccessToken(req)) {
            return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
        }

        const { id } = await params
        const body = await req.json()

        const response = await fetch(`${BACKEND_API_URL}/api/runs/${id}/approve`, {
            method: "POST",
            headers: buildBackendHeaders(req),
            body: JSON.stringify(body),
        })

        const data = await response.json()
        return NextResponse.json(data, { status: response.status })
    } catch (error) {
        console.error("Error approving run:", error)
        return NextResponse.json({ error: "Failed to process approval" }, { status: 500 })
    }
}
