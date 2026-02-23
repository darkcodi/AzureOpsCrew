import { NextRequest } from "next/server"

export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ agentId: string }> }
) {
  const { agentId } = await params
  const headers = new Headers(req.headers)
  headers.set("content-type", "application/json")
  headers.delete("content-length")

  const response = await fetch(new URL(`/api/copilotkit/${agentId}`, req.url), {
    method: "POST",
    headers,
    body: JSON.stringify({ method: "info" }),
    cache: "no-store",
  })

  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers: response.headers,
  })
}
