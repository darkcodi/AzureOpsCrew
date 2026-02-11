import OpenAI from "openai"
import {
  CopilotRuntime,
  OpenAIAdapter,
  copilotRuntimeNextJSAppRouterEndpoint,
} from "@copilotkit/runtime"
import { NextRequest } from "next/server"

const azureApiKey = process.env.OPENAI_API_KEY
const deployment =
  process.env.AZURE_OPENAI_DEPLOYMENT || "gpt-4o"

const openai = new OpenAI({
  apiKey: azureApiKey,
  baseURL: `https://azureopscrewglobfoundry.cognitiveservices.azure.com/openai/deployments/${deployment}`,
  defaultQuery: { "api-version": "2025-04-01-preview" },
  defaultHeaders: azureApiKey ? { "api-key": azureApiKey } : undefined,
})

const serviceAdapter = new OpenAIAdapter({
  openai,
  model: deployment,
})

const runtime = new CopilotRuntime()

export const POST = async (req: NextRequest) => {
  const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
    runtime,
    serviceAdapter,
    endpoint: "/api/copilotkit",
  })
  return handleRequest(req)
}
