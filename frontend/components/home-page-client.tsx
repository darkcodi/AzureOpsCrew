"use client"

import { useState, useCallback, useEffect } from "react"
import { type Agent, type Channel } from "@/lib/agents"
import { IconSidebar, type ViewMode } from "@/components/icon-sidebar"
import { ChannelSidebar } from "@/components/channel-sidebar"
import { ChannelArea } from "@/components/channel-area"
import { DirectMessagesView } from "@/components/direct-messages-view"
import { SettingsView, getDisplayNameFromStorage } from "@/components/settings/settings-view"
import type { HumanMember } from "@/lib/humans"

interface HomePageClientProps {
  initialHumans: HumanMember[]
}

export default function HomePageClient({ initialHumans }: HomePageClientProps) {
  const [viewMode, setViewMode] = useState<ViewMode>("channels")
  const [agents, setAgents] = useState<Agent[]>([])
  const [isLoadingAgents, setIsLoadingAgents] = useState(true)
  const [channels, setChannels] = useState<Channel[]>([])
  const [isLoadingChannels, setIsLoadingChannels] = useState(true)
  const [humans, setHumans] = useState<HumanMember[]>(initialHumans)
  const [activeChannelId, setActiveChannelId] = useState<string>("")
  const [activeDMId, setActiveDMId] = useState<string | null>(null)
  const [pendingDMMessage, setPendingDMMessage] = useState<string | null>(null)
  const [displayName, setDisplayName] = useState(() =>
    typeof window !== "undefined" ? getDisplayNameFromStorage() : "User"
  )
  const activeChannel = channels.find((c) => c.id === activeChannelId) ?? channels[0]

  // Refresh display name from persisted settings when returning from Settings
  useEffect(() => {
    if (viewMode !== "settings") setDisplayName(getDisplayNameFromStorage())
  }, [viewMode])

  useEffect(() => {
    let isCancelled = false

    async function ensureAuthenticated() {
      const response = await fetch("/api/auth/me")
      if (!response.ok && !isCancelled) {
        await fetch("/api/auth/logout", { method: "POST" })
        window.location.href = "/login"
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
  }, [])

  // Load channels from backend on mount
  useEffect(() => {
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
  }, [])

  // Load registered users and refresh presence periodically.
  useEffect(() => {
    let isCancelled = false

    async function loadHumans() {
      try {
        const response = await fetch("/api/users")
        if (!response.ok) return

        const users: HumanMember[] = await response.json()
        if (!isCancelled) {
          setHumans(users)
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
  }, [])

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
        return next
      })
      setActiveChannelId((current) => {
        if (current !== channelId) return current
        const remaining = channels.filter((c) => c.id !== channelId)
        return remaining[0]?.id ?? ""
      })
    } catch (error) {
      console.error("Failed to delete channel:", error)
      throw error
    }
  }, [channels])

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

  const handleOpenAgentInDM = useCallback((agentId: string, message?: string) => {
    setViewMode("direct-messages")
    setActiveDMId(agentId)
    setPendingDMMessage(message ?? null)
  }, [])

  const handleLogout = useCallback(async () => {
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
              displayName={displayName}
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
          pendingDMMessage={pendingDMMessage}
          onClearPendingDMMessage={() => setPendingDMMessage(null)}
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
