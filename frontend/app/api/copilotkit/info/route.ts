import { NextRequest } from "next/server"

export async function GET(req: NextRequest) {
  // Use an explicit allow-list to avoid forwarding user-controlled headers
  // (e.g. Host, x-forwarded-host) to the internal CopilotKit route.
  const forwardHeaders = new Headers()
  forwardHeaders.set("content-type", "application/json")
  const allowList = ["authorization", "x-request-id", "cookie"]
  for (const header of allowList) {
    const value = req.headers.get(header)
    if (value) forwardHeaders.set(header, value)
  }

  const response = await fetch(new URL("/api/copilotkit", req.url), {
    method: "POST",
    headers: forwardHeaders,
    body: JSON.stringify({ method: "info" }),
    cache: "no-store",
  })

  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers: response.headers,
  })
}
