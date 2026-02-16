import { HttpAgent } from "@ag-ui/client"
import {
  CopilotRuntime,
  ExperimentalEmptyAdapter,
  copilotRuntimeNextJSAppRouterEndpoint,
} from "@copilotkit/runtime"
import { NextRequest } from "next/server"

// Backend API URL - use BACKEND_API_URL for consistency
// The /api/agents/{id}/agui endpoint is for single agent invocation
const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ agentId: string }> }
) {
  const { agentId } = await params
  const aguiUrl = `${BACKEND_API_URL}/api/agents/${agentId}/agui`
  const aguiAgent = new HttpAgent({ url: aguiUrl })
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
