import { NextRequest, NextResponse } from "next/server"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:5000"

interface BackendProviderConfig {
  id: string
  clientId: number
  name: string
  providerType: number
  hasApiKey: boolean
  apiEndpoint: string | null
  defaultModel: string | null
  selectedModels: string | null
  isEnabled: boolean
  modelsCount: number
  dateCreated?: string
}

const PROVIDER_TYPE_TO_NAME: Record<number, string> = {
  100: "OpenAI",
  200: "Anthropic",
  300: "Ollama (Local)",
  400: "OpenRouter",
  500: "AzureFoundry",
}

export async function GET(req: NextRequest) {
  try {
    const { searchParams } = new URL(req.url)
    const clientId = searchParams.get("clientId") ?? "1"
    const url = `${BACKEND_API_URL}/api/providers?clientId=${clientId}`

    const response = await fetch(url, {
      method: "GET",
      headers: { "Content-Type": "application/json" },
    })

    if (!response.ok) {
      const text = await response.text()
      return NextResponse.json(
        { error: text || "Failed to fetch providers" },
        { status: response.status }
      )
    }

    const backend: BackendProviderConfig[] = await response.json()

    const providers = backend
      .map((p) => {
        const typeName = PROVIDER_TYPE_TO_NAME[p.providerType] ?? "OpenAI"
        return {
          backendId: p.id,
          id: p.id,
          name: p.name,
          providerType: typeName,
          status: p.isEnabled ? "enabled" : "disabled",
          modelsCount: p.modelsCount ?? 0,
          apiKey: "",
          hasApiKey: p.hasApiKey ?? false,
          baseUrl: p.apiEndpoint ?? "",
          defaultModel: p.defaultModel ?? "",
          selectedModels: p.selectedModels ? JSON.parse(p.selectedModels) as string[] : [],
          timeout: 30,
          rateLimit: 60,
          availableModels: [] as string[],
          isDefault: false,
          dateCreated: p.dateCreated,
        }
      })
      .sort((a, b) => {
        const tA = a.dateCreated ? new Date(a.dateCreated).getTime() : 0
        const tB = b.dateCreated ? new Date(b.dateCreated).getTime() : 0
        return tA - tB
      })

    return NextResponse.json(providers)
  } catch (error) {
    console.error("Error fetching providers:", error)
    return NextResponse.json(
      { error: "Internal server error" },
      { status: 500 }
    )
  }
}
