import { NextRequest, NextResponse } from "next/server"
import { buildBackendHeaders, getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

// GET /api/runs/[id]/journal — get execution journal
export async function GET(
    req: NextRequest,
    { params }: { params: Promise<{ id: string }> }
) {
    try {
        if (!getAccessToken(req)) {
            return NextResponse.json({ error: "Unauthorized" }, { status: 401 })
        }

        const { id } = await params
        const { searchParams } = req.nextUrl
        const skip = searchParams.get("skip") ?? "0"
        const take = searchParams.get("take") ?? "100"

        const response = await fetch(
            `${BACKEND_API_URL}/api/runs/${id}/journal?skip=${skip}&take=${take}`,
            {
                method: "GET",
                headers: buildBackendHeaders(req, { includeContentType: false }),
            }
        )

        const data = await response.json()
        return NextResponse.json(data, { status: response.status })
    } catch (error) {
        console.error("Error fetching journal:", error)
        return NextResponse.json({ error: "Failed to fetch journal" }, { status: 500 })
    }
}
