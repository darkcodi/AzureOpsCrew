import { HttpAgent } from "@ag-ui/client"
import {
  CopilotRuntime,
  ExperimentalEmptyAdapter,
  copilotRuntimeNextJSAppRouterEndpoint,
} from "@copilotkit/runtime"
import { NextRequest } from "next/server"

const baseAguiUrl =
  process.env.BACKEND_AGUI_URL ?? "http://localhost:5000/agui"

export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ agentId: string }> }
) {
  const { agentId } = await params
  const aguiUrl = `${baseAguiUrl.replace(/\/$/, "")}/${agentId}`
  const aguiAgent = new HttpAgent({ url: aguiUrl })
  const runtime = new CopilotRuntime({
    agents: { aguiAgent },
  })
  const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
    runtime,
    serviceAdapter: new ExperimentalEmptyAdapter(),
    endpoint: `/api/copilotkit/${agentId}`,
  })
  return handleRequest(req)
}
