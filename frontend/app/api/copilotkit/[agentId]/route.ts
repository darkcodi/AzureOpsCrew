import { HttpAgent } from "@ag-ui/client"
import {
  CopilotRuntime,
  ExperimentalEmptyAdapter,
  copilotRuntimeNextJSAppRouterEndpoint,
} from "@copilotkit/runtime"
import { NextRequest } from "next/server"
import { getAccessToken } from "@/lib/server/auth"

// Backend API URL - use BACKEND_API_URL for consistency
// The /api/agents/{id}/agui endpoint is for single agent invocation
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

function unauthorized() {
  return new Response(JSON.stringify({ error: "Unauthorized" }), {
    status: 401,
    headers: { "Content-Type": "application/json" },
  })
}

async function handleCopilotRequest(
  req: NextRequest,
  { params }: { params: Promise<{ agentId: string }> }
) {
  const token = getAccessToken(req)
  if (!token) {
    return unauthorized()
  }

  const { agentId } = await params
  const aguiUrl = `${BACKEND_API_URL}/api/agents/${agentId}/agui`
  const aguiAgent = new HttpAgent({
    url: aguiUrl,
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
  return handleRequest(req)
}

export async function GET(
  req: NextRequest,
  context: { params: Promise<{ agentId: string }> }
) {
  return handleCopilotRequest(req, context)
}

export async function POST(
  req: NextRequest,
  context: { params: Promise<{ agentId: string }> }
) {
  return handleCopilotRequest(req, context)
}
