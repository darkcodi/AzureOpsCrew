"use client"

import { useState, useCallback, useEffect } from "react"
import { type Agent, type Channel } from "@/lib/agents"
import { IconSidebar, type ViewMode } from "@/components/icon-sidebar"
import { ChannelSidebar } from "@/components/channel-sidebar"
import { ChannelArea } from "@/components/channel-area"
import {
  clearCachedHumans,
  getCachedHumans,
  setCachedHumans,
  type HumanMember,
} from "@/lib/humans"

interface HomePageClientProps {
  initialHumans: HumanMember[]
}

export default function HomePageClient({ initialHumans }: HomePageClientProps) {
  const [viewMode, setViewMode] = useState<ViewMode>("channels")
  const [agents, setAgents] = useState<Agent[]>([])
  const [isLoadingAgents, setIsLoadingAgents] = useState(true)
  const [channels, setChannels] = useState<Channel[]>([])
  const [isLoadingChannels, setIsLoadingChannels] = useState(true)
  const [humans, setHumans] = useState<HumanMember[]>(() =>
    initialHumans.length > 0 ? initialHumans : getCachedHumans()
  )
  const [activeChannelId, setActiveChannelId] = useState<string>("")
  const [isAutoLoggingIn, setIsAutoLoggingIn] = useState(true)
  const [displayName, setDisplayName] = useState("Demo User")
  const activeChannel = channels.find((c) => c.id === activeChannelId) ?? channels[0]

  useEffect(() => {
    if (humans.length > 0) {
      setCachedHumans(humans)
    }
  }, [humans])

  // Auto-login on mount: call auto-login, then check /me
  useEffect(() => {
    let isCancelled = false

    async function autoLogin() {
      try {
        // Try /me first — maybe already logged in
        const meResp = await fetch("/api/auth/me")
        if (meResp.ok) {
          const meData = await meResp.json()
          if (!isCancelled) {
            setDisplayName(meData.displayName ?? "Demo User")
            setIsAutoLoggingIn(false)
          }
          return
        }

        // Not authenticated — auto-login
        const loginResp = await fetch("/api/auth/auto-login", { method: "POST" })
        if (!loginResp.ok && !isCancelled) {
          console.error("Auto-login failed:", loginResp.status)
          setIsAutoLoggingIn(false)
          return
        }

        const loginData = await loginResp.json()
        if (!isCancelled) {
          setDisplayName(loginData.user?.displayName ?? "Demo User")
          setIsAutoLoggingIn(false)
        }
      } catch (err) {
        console.error("Auto-login error:", err)
        if (!isCancelled) setIsAutoLoggingIn(false)
      }
    }

    void autoLogin()
    return () => { isCancelled = true }
  }, [])

  // Load agents from backend after auto-login completes
  useEffect(() => {
    if (isAutoLoggingIn) return
    async function loadAgents() {
      try {
        setIsLoadingAgents(true)
        const response = await fetch("/api/agents")
        if (response.ok) {
          const backendAgents: Agent[] = await response.json()
          if (backendAgents.length > 0) {
            setAgents(backendAgents)
          }
        }
      } catch (error) {
        console.error("Failed to load agents from backend:", error)
      } finally {
        setIsLoadingAgents(false)
      }
    }
    loadAgents()
  }, [isAutoLoggingIn])

  // Load channels from backend after auto-login completes
  useEffect(() => {
    if (isAutoLoggingIn) return
    async function loadChannels() {
      try {
        setIsLoadingChannels(true)
        const response = await fetch("/api/channels")
        if (response.ok) {
          const backendChannels: Channel[] = await response.json()
          if (backendChannels.length > 0) {
            setChannels(backendChannels)
            // Set active channel to first backend channel
            setActiveChannelId(backendChannels[0].id)
          }
        }
      } catch (error) {
        console.error("Failed to load channels from backend:", error)
      } finally {
        setIsLoadingChannels(false)
      }
    }
    loadChannels()
  }, [isAutoLoggingIn])

  // Load registered users and refresh presence periodically.
  useEffect(() => {
    if (isAutoLoggingIn) return
    let isCancelled = false

    async function loadHumans() {
      try {
        const response = await fetch("/api/users")
        if (!response.ok) return

        const users: HumanMember[] = await response.json()
        if (!isCancelled) {
          setHumans(users)
          setCachedHumans(users)
        }
      } catch (error) {
        console.error("Failed to load users from backend:", error)
      }
    }

    void loadHumans()
    const interval = window.setInterval(() => {
      void loadHumans()
    }, 30000)

    return () => {
      isCancelled = true
      window.clearInterval(interval)
    }
  }, [isAutoLoggingIn])

  const handleCreateChannel = useCallback(async (name: string) => {
    try {
      const response = await fetch("/api/channels/create", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name, agentIds: [] }),
      })

      if (response.ok) {
        const newChannel: Channel = await response.json()
        setChannels((prev) => [...prev, newChannel])
        setActiveChannelId(newChannel.id)
      } else {
        // Fallback to local creation
        const id = crypto.randomUUID()
        const newChannel: Channel = { id, name, agentIds: [], dateCreated: new Date().toISOString() }
        setChannels((prev) => [...prev, newChannel])
        setActiveChannelId(id)
      }
    } catch (error) {
      console.error("Failed to create channel:", error)
      // Fallback to local creation
      const id = crypto.randomUUID()
      const newChannel: Channel = { id, name, agentIds: [], dateCreated: new Date().toISOString() }
      setChannels((prev) => [...prev, newChannel])
      setActiveChannelId(id)
    }
  }, [])

  const handleUpdateChannel = useCallback((updatedChannel: Channel) => {
    setChannels((prev) =>
      prev.map((c) => (c.id === updatedChannel.id ? updatedChannel : c))
    )
  }, [])

  const handleDeleteChannel = useCallback(async (channelId: string) => {
    try {
      const response = await fetch(`/api/channels/${channelId}`, {
        method: "DELETE",
      })
      if (!response.ok) {
        const data = await response.json().catch(() => ({}))
        throw new Error(data.error ?? "Failed to delete channel")
      }
      setChannels((prev) => {
        const next = prev.filter((c) => c.id !== channelId)
        setActiveChannelId((current) => {
          if (current !== channelId) return current
          return next[0]?.id ?? ""
        })
        return next
      })
    } catch (error) {
      console.error("Failed to delete channel:", error)
      throw error
    }
  }, [])

  const handleAddAgent = useCallback(async (agent: Agent) => {
    // Reload agents from backend after creation to ensure consistency
    try {
      const response = await fetch("/api/agents")
      if (response.ok) {
        const backendAgents: Agent[] = await response.json()
        if (backendAgents.length > 0) {
          setAgents(backendAgents)
        }
      }
    } catch (error) {
      console.error("Failed to reload agents from backend:", error)
      // Fallback to local update
      setAgents((prev) => [...prev, agent])
    }
  }, [])

  const handleUpdateAgent = useCallback((agent: Agent) => {
    setAgents((prev) =>
      prev.map((a) => (a.id === agent.id ? agent : a))
    )
  }, [])

  const handleDeleteAgent = useCallback(async (agentId: string) => {
    const response = await fetch(`/api/agents/${agentId}`, {
      method: "DELETE",
    })
    if (!response.ok) {
      const data = await response.json().catch(() => ({}))
      throw new Error(data.error ?? "Failed to delete agent")
    }
    setAgents((prev) => prev.filter((a) => a.id !== agentId))
    setChannels((prev) =>
      prev.map((c) => ({
        ...c,
        agentIds: c.agentIds.filter((id) => id !== agentId),
      }))
    )
  }, [])

  const handleLogout = useCallback(async () => {
    clearCachedHumans()
    await fetch("/api/auth/logout", { method: "POST" })
    window.location.reload()
  }, [])

  if (isAutoLoggingIn) {
    return (
      <main className="flex h-dvh w-full items-center justify-center" style={{ backgroundColor: "hsl(228, 6%, 15%)" }}>
        <div className="flex flex-col items-center gap-4">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-white border-t-transparent" />
          <p style={{ color: "hsl(214, 5%, 55%)" }}>Connecting to Azure Ops Crew...</p>
        </div>
      </main>
    )
  }

  return (
    <main className="flex h-dvh w-full">
      <IconSidebar
        viewMode={viewMode}
        onViewChange={setViewMode}
        onLogout={handleLogout}
      />

      <ChannelSidebar
        channels={channels}
        activeChannelId={activeChannelId}
        onChannelSelect={setActiveChannelId}
        onCreateChannel={handleCreateChannel}
        onChannelDelete={handleDeleteChannel}
      />
      {activeChannel ? (
        <ChannelArea
          key={activeChannel.id}
          channel={activeChannel}
          allAgents={agents}
          humans={humans}
          displayName={displayName}
          onUpdateChannel={handleUpdateChannel}
          onAddAgent={handleAddAgent}
          onUpdateAgent={handleUpdateAgent}
          onDeleteAgent={handleDeleteAgent}
          onOpenInDM={() => { }}
        />
      ) : isLoadingChannels ? (
        <div
          className="flex flex-1 items-center justify-center"
          style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
        >
          <div style={{ color: "hsl(214, 5%, 55%)" }}>Loading channels...</div>
        </div>
      ) : (
        <div
          className="flex flex-1 items-center justify-center"
          style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
        >
          <div style={{ color: "hsl(214, 5%, 55%)" }}>No channels found</div>
        </div>
      )}
    </main>
  )
}
