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

interface FrontendProvider {
  id: string
  backendId?: string
  name: string
  providerType?: string
  status: string
  apiKey: string
  baseUrl: string
  defaultModel: string
  isDefault?: boolean
}

export async function PUT(req: NextRequest) {
  try {
    const body = await req.json()
    const { providers } = body as { providers: FrontendProvider[] }

    if (!Array.isArray(providers)) {
      return NextResponse.json(
        { error: "providers array is required" },
        { status: 400 }
      )
    }

    const clientId = 1
    const results: { id: string; backendId: string }[] = []

    for (const p of providers) {
      const providerType =
        PROVIDER_TYPE_FROM_NAME[p.providerType ?? p.name] ?? 100
      const isEnabled = p.status !== "disabled"

      if (p.backendId) {
        const updateUrl = `${BACKEND_API_URL}/api/providers/${p.backendId}`
        const res = await fetch(updateUrl, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            name: p.name,
            apiKey: p.apiKey,
            apiEndpoint: p.baseUrl || null,
            defaultModel: p.defaultModel || null,
            isEnabled,
          }),
        })
        if (!res.ok) {
          const text = await res.text()
          return NextResponse.json(
            { error: text || `Failed to update provider ${p.name}` },
            { status: res.status }
          )
        }
        results.push({ id: p.id, backendId: p.backendId })
      } else {
        const createUrl = `${BACKEND_API_URL}/api/providers/create`
        const res = await fetch(createUrl, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            clientId,
            name: p.name,
            providerType,
            apiKey: p.apiKey,
            apiEndpoint: p.baseUrl || null,
            defaultModel: p.defaultModel || null,
            isEnabled,
          }),
        })
        if (!res.ok) {
          const text = await res.text()
          return NextResponse.json(
            { error: text || `Failed to create provider ${p.name}` },
            { status: res.status }
          )
        }
        const created = await res.json()
        results.push({ id: p.id, backendId: created.id })
      }
    }

    return NextResponse.json({ ok: true, providers: results })
  } catch (error) {
    console.error("Error saving settings:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
