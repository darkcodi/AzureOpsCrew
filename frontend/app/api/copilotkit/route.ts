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
const aguiUrl = `${BACKEND_API_URL}/api/agents/default/agui`

const aguiAgent = new HttpAgent({ url: aguiUrl })

const runtime = new CopilotRuntime({
  agents: { aguiAgent },
})

export async function POST(req: NextRequest) {
  const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
    runtime,
    serviceAdapter: new ExperimentalEmptyAdapter(),
    endpoint: "/api/copilotkit",
  })
  return handleRequest(req)
}
