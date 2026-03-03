"use client"

import { useState, useCallback, useEffect } from "react"
import { type Agent, type Channel } from "@/lib/agents"
import { IconSidebar, type ViewMode } from "@/components/icon-sidebar"
import { ChannelSidebar } from "@/components/channel-sidebar"
import { ChannelArea } from "@/components/channel-area"
import { DirectMessagesView } from "@/components/direct-messages-view"
import { SettingsView, getUsernameFromStorage } from "@/components/settings/settings-view"
import {
  clearCachedHumans,
  getCachedHumans,
  setCachedHumans,
  type HumanMember,
} from "@/lib/humans"
import { fetchWithErrorHandling } from "@/lib/fetch"
import { ChannelEventsClient } from "@/lib/signalr-client"

interface HomePageClientProps {
  initialHumans: HumanMember[]
}

export default function HomePageClient({ initialHumans }: HomePageClientProps) {
  const [viewMode, setViewMode] = useState<ViewMode>("direct-messages")
  const [agents, setAgents] = useState<Agent[]>([])
  const [isLoadingAgents, setIsLoadingAgents] = useState(true)
  const [channels, setChannels] = useState<Channel[]>([])
  const [isLoadingChannels, setIsLoadingChannels] = useState(true)
  const [humans, setHumans] = useState<HumanMember[]>(() =>
    initialHumans.length > 0 ? initialHumans : getCachedHumans()
  )
  const [activeChannelId, setActiveChannelId] = useState<string>("")
  const [activeDMId, setActiveDMId] = useState<string | null>(null)
  const [username, setUsername] = useState(() =>
    typeof window !== "undefined" ? getUsernameFromStorage() : "User"
  )
  const activeChannel = channels.find((c) => c.id === activeChannelId) ?? channels[0]

  // Refresh username from persisted settings when returning from Settings
  useEffect(() => {
    if (viewMode !== "settings") setUsername(getUsernameFromStorage())
  }, [viewMode])

  useEffect(() => {
    if (humans.length > 0) {
      setCachedHumans(humans)
    }
  }, [humans])

  useEffect(() => {
    let isCancelled = false

    async function ensureAuthenticated() {
      try {
        const response = await fetchWithErrorHandling("/api/auth/me")
        // Only log out on 401 Unauthorized, not on 500 server errors
        if (response.status === 401 && !isCancelled) {
          clearCachedHumans()
          await fetch("/api/auth/logout", { method: "POST" })
          window.location.href = "/login"
        }
      } catch {
        // Network errors (user offline, etc.) don't warrant logout
      }
    }

    void ensureAuthenticated()
    return () => {
      isCancelled = true
    }
  }, [])

  // Load agents from backend on mount
  useEffect(() => {
    async function loadAgents() {
      try {
        setIsLoadingAgents(true)
        const response = await fetchWithErrorHandling("/api/agents")
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
  }, [])

  // Load channels from backend on mount
  useEffect(() => {
    async function loadChannels() {
      try {
        setIsLoadingChannels(true)
        const response = await fetchWithErrorHandling("/api/channels")
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
  }, [])

  // Load registered users on mount (no polling)
  useEffect(() => {
    let isCancelled = false

    async function loadHumans() {
      try {
        const response = await fetchWithErrorHandling("/api/users")
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
    return () => {
      isCancelled = true
    }
  }, [])

  // Listen for user presence updates via SignalR
  useEffect(() => {
    const backendUrl = process.env.NEXT_PUBLIC_BACKEND_API_URL ?? "http://localhost:5000"
    // Use empty GUID as a convention for the global presence channel
    const globalChannelId = "00000000-0000-0000-0000-000000000000"
    const hubUrl = `${backendUrl}/channels/${globalChannelId}/events`

    const presenceClient = new ChannelEventsClient(globalChannelId)
    let mounted = true

    presenceClient.start().then(() => {
      if (!mounted) return

      presenceClient.onUserPresence((event) => {
        setHumans((prev) =>
          prev.map((h) =>
            h.userId === event.userId
              ? { ...h, status: event.isOnline ? ("Online" as const) : ("Offline" as const) }
              : h
          )
        )
      })
    }).catch((err) => {
      console.error("Failed to connect to presence hub:", err)
    })

    return () => {
      mounted = false
      presenceClient.stop()
    }
  }, [])

  const handleCreateChannel = useCallback(async (name: string) => {
    try {
      const response = await fetchWithErrorHandling("/api/channels/create", {
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
      const response = await fetchWithErrorHandling(`/api/channels/${channelId}`, {
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
      const response = await fetchWithErrorHandling("/api/agents")
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
    const response = await fetchWithErrorHandling(`/api/agents/${agentId}`, {
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

  const handleOpenAgentInDM = useCallback((agentId: string) => {
    setViewMode("direct-messages")
    setActiveDMId(agentId)
  }, [])

  const handleLogout = useCallback(async () => {
    clearCachedHumans()
    await fetch("/api/auth/logout", { method: "POST" })
    window.location.href = "/login"
  }, [])

  return (
    <main className="flex h-dvh w-full">
      <IconSidebar
        viewMode={viewMode}
        onViewChange={setViewMode}
        onLogout={handleLogout}
      />

      {viewMode === "channels" && (
        <>
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
              username={username}
              onUpdateChannel={handleUpdateChannel}
              onAddAgent={handleAddAgent}
              onUpdateAgent={handleUpdateAgent}
              onDeleteAgent={handleDeleteAgent}
              onOpenInDM={handleOpenAgentInDM}
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
        </>
      )}
      {viewMode === "direct-messages" && (
        <DirectMessagesView
          activeDMId={activeDMId}
          setActiveDMId={setActiveDMId}
          agents={agents}
          humans={humans}
        />
      )}
      {viewMode === "settings" && (
        <SettingsView
          allAgents={agents}
          onAddAgent={handleAddAgent}
          onUpdateAgent={handleUpdateAgent}
          onDeleteAgent={handleDeleteAgent}
        />
      )}
    </main>
  )
}
