import { NextRequest, NextResponse } from "next/server"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

const PROVIDER_TYPE_FROM_NAME: Record<string, number> = {
  OpenAI: 100,
  "Azure OpenAI": 100,
  Anthropic: 200,
  "Ollama (Local)": 300,
  Ollama: 300,
  OpenRouter: 400,
  AzureFoundry: 500,
  "Azure Foundry": 500,
}

export async function POST(req: NextRequest) {
  try {
    const body = await req.json() as {
      providerType?: string
      name?: string
      apiKey?: string
      baseUrl?: string
      apiEndpoint?: string
      defaultModel?: string
    }
    const providerType =
      PROVIDER_TYPE_FROM_NAME[body.providerType ?? ""] ?? 100
    const apiEndpoint = body.apiEndpoint ?? body.baseUrl ?? ""
    const backendBody = {
      providerType,
      apiKey: body.apiKey ?? "",
      apiEndpoint,
      defaultModel: body.defaultModel ?? null,
      name: body.name ?? null,
    }
    const response = await fetch(`${BACKEND_API_URL}/api/providers/test`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(backendBody),
    })
    const data = await response.json().catch(() => ({}))
    if (!response.ok) {
      return NextResponse.json(
        data?.message ? { success: false, message: data.message } : data,
        { status: response.status }
      )
    }
    return NextResponse.json(data)
  } catch (error) {
    console.error("Error testing provider connection:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
