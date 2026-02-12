"use client"

import { useState, useCallback } from "react"
import { defaultAgents, defaultRooms, type Agent, type Room } from "@/lib/agents"
import { IconSidebar, type ViewMode } from "@/components/icon-sidebar"
import { ChannelSidebar } from "@/components/channel-sidebar"
import { ChatArea } from "@/components/chat-area"
import { DirectMessagesSidebar } from "@/components/direct-messages-sidebar"
import { DirectMessagesArea } from "@/components/direct-messages-area"

export default function Home() {
  const [viewMode, setViewMode] = useState<ViewMode>("channels")
  const [agents, setAgents] = useState<Agent[]>(defaultAgents)
  const [rooms, setRooms] = useState<Room[]>(defaultRooms)
  const [activeRoomId, setActiveRoomId] = useState(defaultRooms[0].id)
  const [activeDMId, setActiveDMId] = useState<string | null>("assistant")
  const [showAgentManager, setShowAgentManager] = useState(false)
  const activeRoom = rooms.find((r) => r.id === activeRoomId) ?? rooms[0]

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

  const handleAddAgent = useCallback((agent: Agent) => {
    setAgents((prev) => [...prev, agent])
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

  return (
    <main className="flex h-dvh w-full">
      <IconSidebar
        viewMode={viewMode}
        onViewChange={setViewMode}
        onOpenAgentManager={() => setShowAgentManager(true)}
      />

      {viewMode === "channels" ? (
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
            showAgentManager={showAgentManager}
            onCloseAgentManager={() => setShowAgentManager(false)}
          />
        </>
      ) : (
        <>
          <DirectMessagesSidebar
            agents={agents}
            activeId={activeDMId}
            onSelect={setActiveDMId}
          />
          <DirectMessagesArea />
        </>
      )}
    </main>
  )
}
