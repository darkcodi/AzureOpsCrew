"use client"

import {
  createContext,
  useCallback,
  useContext,
  useState,
  type ReactNode,
} from "react"

type AgentRuntimeContextValue = {
  agentId: string | null
  setAgentId: (id: string | null) => void
}

const AgentRuntimeContext = createContext<AgentRuntimeContextValue | null>(null)

export function AgentRuntimeProvider({ children }: { children: ReactNode }) {
  const [agentId, setAgentId] = useState<string | null>(null)
  return (
    <AgentRuntimeContext.Provider value={{ agentId, setAgentId }}>
      {children}
    </AgentRuntimeContext.Provider>
  )
}

export function useAgentRuntime() {
  const ctx = useContext(AgentRuntimeContext)
  if (!ctx) {
    return {
      agentId: null as string | null,
      setAgentId: (_: string | null) => {},
    }
  }
  return ctx
}
