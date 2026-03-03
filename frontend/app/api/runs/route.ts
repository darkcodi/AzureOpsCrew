import { NextRequest, NextResponse } from "next/server"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

// GET /api/runs — list runs
export async function GET(req: NextRequest) {
    try {
        if (!getAccessToken(req)) {
            return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
        }

        const response = await fetch(`${BACKEND_API_URL}/api/runs`, {
            method: "GET",
            headers: buildBackendHeaders(req, { includeContentType: false }),
        })

        const data = await response.json()
        return NextResponse.json(data, { status: response.status })
    } catch (error) {
        console.error("Error fetching runs:", error)
        return NextResponse.json({ error: "Failed to fetch runs" }, { status: 500 })
    }
}

// POST /api/runs — create a new run
export async function POST(req: NextRequest) {
    try {
        if (!getAccessToken(req)) {
            return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
        }

        const body = await req.json()

        const response = await fetch(`${BACKEND_API_URL}/api/runs`, {
            method: "POST",
            headers: buildBackendHeaders(req),
            body: JSON.stringify(body),
        })

        const data = await response.json()
        return NextResponse.json(data, { status: response.status })
    } catch (error) {
        console.error("Error creating run:", error)
        return NextResponse.json({ error: "Failed to create run" }, { status: 500 })
    }
}
