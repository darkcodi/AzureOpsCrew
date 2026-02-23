import { NextRequest } from "next/server"

export async function GET(req: NextRequest) {
  const headers = new Headers(req.headers)
  headers.set("content-type", "application/json")
  headers.delete("content-length")

  const response = await fetch(new URL("/api/copilotkit", req.url), {
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
