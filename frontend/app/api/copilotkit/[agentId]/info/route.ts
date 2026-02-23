import { HttpAgent } from "@ag-ui/client"
import {
  CopilotRuntime,
  ExperimentalEmptyAdapter,
  copilotRuntimeNextJSAppRouterEndpoint,
} from "@copilotkit/runtime"
import { NextRequest } from "next/server"
import { getAccessToken } from "@/lib/server/auth"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ agentId: string }> }
) {
  const token = getAccessToken(req)
  if (!token) {
    return new Response(JSON.stringify({ error: "Unauthorized" }), {
      status: 401,
      headers: { "Content-Type": "application/json" },
    })
  }

  const { agentId } = await params
  const aguiAgent = new HttpAgent({
    url: `${BACKEND_API_URL}/api/agents/${agentId}/agui`,
    headers: { Authorization: `Bearer ${token}` },
  })

  const runtime = new CopilotRuntime({
    agents: { aguiAgent } as any,
  })

  const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
    runtime,
    serviceAdapter: new ExperimentalEmptyAdapter(),
    endpoint: `/api/copilotkit/${agentId}`,
  })

  const headers = new Headers(req.headers)
  headers.set("content-type", "application/json")
  headers.delete("content-length")

  // CopilotKit single-route runtime expects POST envelopes like { method: "info" }.
  const runtimeInfoRequest = new Request(
    new URL(`/api/copilotkit/${agentId}`, req.url),
    {
      method: "POST",
      headers,
      body: JSON.stringify({ method: "info" }),
    }
  )

  return handleRequest(runtimeInfoRequest as NextRequest)
}
