"use client"

import { useState, useCallback, useEffect } from "react"
import { defaultAgents, defaultRooms, type Agent, type Room } from "@/lib/agents"
import { IconSidebar, type ViewMode } from "@/components/icon-sidebar"
import { ChannelSidebar } from "@/components/channel-sidebar"
import { ChatArea } from "@/components/chat-area"
import { DirectMessagesView } from "@/components/direct-messages-view"
import { ManageAgentsDialog } from "@/components/manage-agents-dialog"
import { AllAgentsSidebar } from "@/components/all-agents-sidebar"

export default function Home() {
  const [viewMode, setViewMode] = useState<ViewMode>("channels")
  const [agents, setAgents] = useState<Agent[]>(defaultAgents)
  const [isLoadingAgents, setIsLoadingAgents] = useState(true)
  const [rooms, setRooms] = useState<Room[]>(defaultRooms)
  const [activeRoomId, setActiveRoomId] = useState(defaultRooms[0].id)
  const [activeDMId, setActiveDMId] = useState<string | null>(
    () => defaultAgents[0]?.id ?? null
  )
  const [pendingDMMessage, setPendingDMMessage] = useState<string | null>(null)
  const activeRoom = rooms.find((r) => r.id === activeRoomId) ?? rooms[0]

  // Load agents from backend on mount
  useEffect(() => {
    async function loadAgents() {
      try {
        setIsLoadingAgents(true)
        const response = await fetch("/api/agents?clientId=1")
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

  const handleCreateRoom = useCallback((name: string) => {
    const id = name.toLowerCase().replace(/\s+/g, "-") + "-" + Date.now()
    const newRoom: Room = { id, name, agentIds: [] }
    setRooms((prev) => [...prev, newRoom])
    setActiveRoomId(id)
  }, [])

  const handleUpdateRoom = useCallback((updatedRoom: Room) => {
    setRooms((prev) =>
      prev.map((r) => (r.id === updatedRoom.id ? updatedRoom : r))
    )
  }, [])

  const handleAddAgent = useCallback(async (agent: Agent) => {
    // Reload agents from backend after creation to ensure consistency
    try {
      const response = await fetch("/api/agents?clientId=1")
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

  const handleDeleteAgent = useCallback((agentId: string) => {
    setAgents((prev) => prev.filter((a) => a.id !== agentId))
    setRooms((prev) =>
      prev.map((r) => ({
        ...r,
        agentIds: r.agentIds.filter((id) => id !== agentId),
      }))
    )
  }, [])

  const handleOpenAgentInDM = useCallback((agentId: string, message?: string) => {
    setViewMode("direct-messages")
    setActiveDMId(agentId)
    setPendingDMMessage(message ?? null)
  }, [])

  return (
    <main className="flex h-dvh w-full">
      <IconSidebar
        viewMode={viewMode}
        onViewChange={setViewMode}
      />

      {viewMode === "channels" && (
        <>
          <ChannelSidebar
            rooms={rooms}
            activeRoomId={activeRoomId}
            onRoomSelect={setActiveRoomId}
            onCreateRoom={handleCreateRoom}
          />
          <ChatArea
            key={activeRoom.id}
            room={activeRoom}
            allAgents={agents}
            onUpdateRoom={handleUpdateRoom}
            onAddAgent={handleAddAgent}
            onUpdateAgent={handleUpdateAgent}
            onDeleteAgent={handleDeleteAgent}
            onOpenInDM={handleOpenAgentInDM}
          />
        </>
      )}
      {viewMode === "direct-messages" && (
        <DirectMessagesView
          activeDMId={activeDMId}
          setActiveDMId={setActiveDMId}
          agents={agents}
          pendingDMMessage={pendingDMMessage}
          onClearPendingDMMessage={() => setPendingDMMessage(null)}
        />
      )}
      {viewMode === "all-agents" && (
        <>
          <AllAgentsSidebar />
          <div
            className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden"
            style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
          >
            <ManageAgentsDialog
              allAgents={agents}
              onAddAgent={handleAddAgent}
              onUpdateAgent={handleUpdateAgent}
              onDeleteAgent={handleDeleteAgent}
              embedded
            />
          </div>
        </>
      )}
    </main>
  )
}
