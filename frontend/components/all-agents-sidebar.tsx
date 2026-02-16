"use client"

import { useEffect, useState } from "react"
import { Server } from "lucide-react"

interface Provider {
  id: string
  backendId: string
  name: string
  providerType: string
  status: string
}

export function AllAgentsSidebar() {
  const [providers, setProviders] = useState<Provider[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    async function loadProviders() {
      try {
        setIsLoading(true)
        setError(null)
        const response = await fetch("/api/providers?clientId=1")
        if (!response.ok) {
          const data = await response.json().catch(() => ({}))
          throw new Error(data.error ?? "Failed to fetch providers")
        }
        const data: Provider[] = await response.json()
        const enabled = data.filter((p) => p.status === "enabled")
        setProviders(enabled)
      } catch (e) {
        setError(e instanceof Error ? e.message : "Unknown error")
        setProviders([])
      } finally {
        setIsLoading(false)
      }
    }
    loadProviders()
  }, [])

  return (
    <div
      className="flex h-full w-[220px] flex-col shrink-0"
      style={{ backgroundColor: "hsl(228, 7%, 14%)" }}
    >
      <div
        className="flex h-12 items-center gap-2 px-4"
        style={{
          borderBottom: "1px solid hsl(228, 6%, 10%)",
        }}
      >
        <Server className="h-4 w-4 shrink-0" style={{ color: "hsl(214, 5%, 55%)" }} />
        <span
          className="text-xs font-bold uppercase tracking-wider"
          style={{ color: "hsl(214, 5%, 55%)" }}
        >
          All providers
        </span>
      </div>
      <div className="flex-1 overflow-y-auto py-2">
        {isLoading && (
          <div className="px-4 py-2 text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
            Loading…
          </div>
        )}
        {error && (
          <div className="px-4 py-2 text-xs" style={{ color: "hsl(0, 70%, 55%)" }}>
            {error}
          </div>
        )}
        {!isLoading && !error && providers.length === 0 && (
          <div className="px-4 py-2 text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
            No enabled providers
          </div>
        )}
        {!isLoading && !error && providers.length > 0 && (
          <ul className="space-y-0.5">
            {providers.map((p) => (
              <li
                key={p.id}
                className="truncate px-4 py-2 text-sm"
                style={{
                  color: "hsl(214, 10%, 85%)",
                }}
                title={`${p.name} (${p.providerType})`}
              >
                {p.name}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}
