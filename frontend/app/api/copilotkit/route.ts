import { HttpAgent } from "@ag-ui/client"
import {
  CopilotRuntime,
  ExperimentalEmptyAdapter,
  copilotRuntimeNextJSAppRouterEndpoint,
} from "@copilotkit/runtime"
import { NextRequest } from "next/server"

const baseAguiUrl =
  process.env.BACKEND_AGUI_URL ?? "http://localhost:5000/api/agents"
const aguiUrl = `${baseAguiUrl.replace(/\/$/, "")}/default/agui`

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
