"use client"

import { useState, useEffect } from "react"
import type { Agent } from "@/lib/agents"
import { X, Plus, Pencil, Trash2, ChevronLeft, Save, Loader2 } from "lucide-react"
import { ScrollArea } from "@/components/ui/scroll-area"
import { useToast } from "@/hooks/use-toast"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"

interface Provider {
  id: string
  name: string
  providerType: string
  status: string
  isEnabled: boolean
}

const availableModels = [
  { id: "gpt-5-2-chat", name: "GPT-5.2 Chat" },
  { id: "openai/gpt-4o-mini", name: "GPT-4o Mini" },
  { id: "openai/gpt-4o", name: "GPT-4o" },
  { id: "openai/gpt-4.1-mini", name: "GPT-4.1 Mini" },
  { id: "openai/gpt-4.1", name: "GPT-4.1" },
  { id: "anthropic/claude-sonnet-4-20250514", name: "Claude Sonnet 4" },
  { id: "anthropic/claude-haiku-3.5", name: "Claude Haiku 3.5" },
  { id: "xai/grok-3-mini-fast", name: "Grok 3 Mini Fast" },
]

interface ManageAgentsDialogProps {
  allAgents: Agent[]
  onClose?: () => void
  onAddAgent: (agent: Agent) => void | Promise<void>
  onUpdateAgent: (agent: Agent) => void
  onDeleteAgent: (agentId: string) => void | Promise<void>
  /** When true, render as full-page tab content (no overlay, no close button). */
  embedded?: boolean
  /** When true and embedded, hide the inner header (for use inside Settings). */
  hideHeader?: boolean
}

type View = "list" | "edit" | "create"

const agentColors = [
  "#43b581", "#7289da", "#faa61a", "#e91e63",
  "#f04747", "#3498db", "#9b59b6", "#e67e22",
  "#1abc9c", "#2ecc71", "#e74c3c", "#8e44ad",
]

export function ManageAgentsDialog({
  allAgents,
  onClose,
  onAddAgent,
  onUpdateAgent,
  onDeleteAgent,
  embedded = false,
  hideHeader = false,
}: ManageAgentsDialogProps) {
  const [view, setView] = useState<View>("list")
  const [editingAgent, setEditingAgent] = useState<Agent | null>(null)
  const [deleteAgentPending, setDeleteAgentPending] = useState<Agent | null>(null)
  const [isDeletingAgent, setIsDeletingAgent] = useState(false)
  const { toast } = useToast()

  // Providers state
  const [providers, setProviders] = useState<Provider[]>([])
  const [isLoadingProviders, setIsLoadingProviders] = useState(true)

  // Fetch providers on mount
  useEffect(() => {
    fetch("/api/providers?clientId=1")
      .then((res) => res.json())
      .then((data: Provider[]) => {
        setProviders(data.filter((p) => p.isEnabled))
        setIsLoadingProviders(false)
      })
      .catch(() => {
        setIsLoadingProviders(false)
      })
  }, [])

  // Form state
  const [name, setName] = useState("")
  const [model, setModel] = useState(availableModels[0].id)
  const [prompt, setPrompt] = useState("")
  const [color, setColor] = useState(agentColors[0])
  const [providerId, setProviderId] = useState("")
  const [isSaving, setIsSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  const openCreate = () => {
    setEditingAgent(null)
    setName("")
    setModel(availableModels[0].id)
    setPrompt("")
    setColor(agentColors[allAgents.length % agentColors.length])
    setProviderId(providers.length > 0 ? providers[0].id : "")
    setSaveError(null)
    setView("create")
  }

  const openEdit = (agent: Agent) => {
    setEditingAgent(agent)
    setName(agent.name)
    setModel(agent.model)
    setPrompt(agent.systemPrompt)
    setColor(agent.color)
    setSaveError(null)
    setView("edit")
  }

  const handleSave = async () => {
    const trimmed = name.trim()
    if (!trimmed) return

    if (view === "create" && !providerId) {
      setSaveError("Please select a provider")
      return
    }

    setIsSaving(true)
    setSaveError(null)

    try {
      if (view === "create") {
        // Call backend API to create agent
        const response = await fetch("/api/agents/create", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            name: trimmed,
            model,
            systemPrompt: prompt.trim() || `You are ${trimmed}, a helpful AI assistant.`,
            color,
            providerId,
          }),
        })

        if (!response.ok) {
          const errorData = await response.json()
          throw new Error(errorData.error || "Failed to create agent")
        }

        const newAgent = await response.json()
        await onAddAgent(newAgent)
      } else if (editingAgent) {
        // For edit, still use local update for now
        // TODO: Implement backend update endpoint
        onUpdateAgent({
          ...editingAgent,
          name: trimmed,
          avatar: trimmed[0].toUpperCase(),
          color,
          systemPrompt: prompt.trim() || `You are ${trimmed}, a helpful AI assistant.`,
          model,
        })
      }
      setView("list")
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : "Failed to save agent")
    } finally {
      setIsSaving(false)
    }
  }

  const content = (
    <>
    <div
      className={embedded ? "flex min-h-0 flex-1 flex-col overflow-hidden" : "relative z-10 flex w-full max-w-lg flex-col overflow-hidden rounded-lg"}
      style={{ backgroundColor: embedded ? "transparent" : "hsl(228, 6%, 20%)", ...(embedded ? {} : { maxHeight: "85vh" }) }}
    >
      {/* Header */}
      {!(embedded && hideHeader) && (
        <div
          className="flex shrink-0 items-center gap-3 px-5 py-4"
          style={{ borderBottom: "1px solid hsl(228, 6%, 28%)" }}
        >
          {view !== "list" && (
            <button
              type="button"
              onClick={() => setView("list")}
              className="transition-opacity hover:opacity-80"
              style={{ color: "hsl(214, 5%, 55%)" }}
              aria-label="Back to list"
            >
              <ChevronLeft className="h-5 w-5" />
            </button>
          )}
          <h2 className="flex-1 text-lg font-semibold" style={{ color: "hsl(0, 0%, 100%)" }}>
            {view === "list" ? "All Agents" : view === "create" ? "New Agent" : "Edit Agent"}
          </h2>
          {!embedded && onClose && (
            <button
              type="button"
              onClick={onClose}
              className="transition-opacity hover:opacity-80"
              style={{ color: "hsl(214, 5%, 55%)" }}
              aria-label="Close"
            >
              <X className="h-5 w-5" />
            </button>
          )}
        </div>
      )}

      {/* Back button when header is hidden (e.g. Settings) and user is in create/edit */}
      {embedded && hideHeader && view !== "list" && (
        <div
          className="flex shrink-0 items-center border-b px-5 py-2"
          style={{ borderColor: "hsl(228, 6%, 28%)" }}
        >
          <button
            type="button"
            onClick={() => setView("list")}
            className="flex items-center gap-1.5 transition-opacity hover:opacity-80"
            style={{ color: "hsl(214, 5%, 55%)" }}
            aria-label="Back to list"
          >
            <ChevronLeft className="h-5 w-5" />
            <span className="text-sm">Back to list</span>
          </button>
        </div>
      )}

        {/* Body */}
        {view === "list" ? (
          <>
            <ScrollArea className="flex-1 min-h-0 px-5 py-3" style={embedded ? { flex: 1, minHeight: 0 } : { maxHeight: "60vh" }}>
              {allAgents.map((agent) => (
                <div
                  key={agent.id}
                  className="mb-1 flex items-center gap-3 rounded-md px-3 py-3 transition-colors"
                  onMouseEnter={(e) => {
                    e.currentTarget.style.backgroundColor = "hsl(228, 6%, 26%)"
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.backgroundColor = "transparent"
                  }}
                >
                  <div
                    className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-sm font-bold"
                    style={{ backgroundColor: agent.color, color: "#fff" }}
                  >
                    {agent.avatar}
                  </div>
                  <div className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-sm font-medium" style={{ color: "hsl(210, 3%, 90%)" }}>
                      {agent.name}
                    </span>
                    <span className="truncate text-xs" style={{ color: "hsl(214, 5%, 55%)" }}>
                      {availableModels.find((m) => m.id === agent.model)?.name ?? agent.model}
                    </span>
                  </div>
                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      onClick={() => openEdit(agent)}
                      className="rounded-md p-1.5 transition-opacity hover:opacity-80"
                      style={{ color: "hsl(210, 3%, 80%)" }}
                      aria-label={"Edit " + agent.name}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </button>
                    <button
                      type="button"
                      onClick={() => setDeleteAgentPending(agent)}
                      className="rounded-md p-1.5 transition-opacity hover:opacity-80"
                      style={{ color: "hsl(0, 70%, 55%)" }}
                      aria-label={"Delete " + agent.name}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                </div>
              ))}
            </ScrollArea>
            <div className="shrink-0 px-5 pb-4 pt-2">
              <button
                type="button"
                onClick={openCreate}
                className="flex w-full items-center justify-center gap-1.5 rounded-md py-2.5 text-sm font-medium transition-opacity hover:opacity-80"
                style={{ backgroundColor: "hsl(235, 86%, 65%)", color: "#fff" }}
              >
                <Plus className="h-4 w-4" />
                <span>Create New Agent</span>
              </button>
            </div>
          </>
        ) : (
          <ScrollArea className="flex-1 min-h-0" style={embedded ? { flex: 1, minHeight: 0 } : { maxHeight: "70vh" }}>
            <div className="flex flex-col gap-4 px-5 py-4">
              {/* Name */}
              <div className="flex flex-col gap-1.5">
                <label
                  htmlFor="agent-name"
                  className="text-xs font-bold uppercase tracking-wider"
                  style={{ color: "hsl(214, 5%, 55%)" }}
                >
                  Agent Name
                </label>
                <input
                  id="agent-name"
                  type="text"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="e.g. Research Assistant"
                  className="w-full rounded-md px-3 py-2 text-sm outline-none"
                  style={{
                    backgroundColor: "hsl(228, 7%, 14%)",
                    color: "hsl(210, 3%, 90%)",
                    border: "1px solid hsl(228, 6%, 30%)",
                  }}
                />
              </div>

              {/* Provider */}
              {view === "create" && (
                <div className="flex flex-col gap-1.5">
                  <label
                    htmlFor="agent-provider"
                    className="text-xs font-bold uppercase tracking-wider"
                    style={{ color: "hsl(214, 5%, 55%)" }}
                  >
                    Provider
                  </label>
                  {isLoadingProviders ? (
                    <div className="flex items-center gap-2 text-sm" style={{ color: "hsl(214, 5%, 55%)" }}>
                      <Loader2 className="h-4 w-4 animate-spin" />
                      <span>Loading providers...</span>
                    </div>
                  ) : providers.length === 0 ? (
                    <div className="text-sm" style={{ color: "hsl(0, 70%, 55%)" }}>
                      No enabled providers found. Please enable a provider in Settings.
                    </div>
                  ) : (
                    <select
                      id="agent-provider"
                      value={providerId}
                      onChange={(e) => setProviderId(e.target.value)}
                      className="w-full appearance-none rounded-md px-3 py-2 text-sm outline-none"
                      style={{
                        backgroundColor: "hsl(228, 7%, 14%)",
                        color: "hsl(210, 3%, 90%)",
                        border: "1px solid hsl(228, 6%, 30%)",
                      }}
                    >
                      {providers.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name} ({p.providerType})
                        </option>
                      ))}
                    </select>
                  )}
                </div>
              )}

              {/* Color */}
              <div className="flex flex-col gap-1.5">
                <span
                  className="text-xs font-bold uppercase tracking-wider"
                  style={{ color: "hsl(214, 5%, 55%)" }}
                >
                  Color
                </span>
                <div className="flex flex-wrap gap-2">
                  {agentColors.map((c) => (
                    <button
                      key={c}
                      type="button"
                      onClick={() => setColor(c)}
                      className="h-7 w-7 rounded-full transition-all"
                      style={{
                        backgroundColor: c,
                        outline: color === c ? "2px solid hsl(0, 0%, 100%)" : "none",
                        outlineOffset: "2px",
                      }}
                      aria-label={"Select color " + c}
                    />
                  ))}
                </div>
              </div>

              {/* LLM Model */}
              <div className="flex flex-col gap-1.5">
                <label
                  htmlFor="agent-model"
                  className="text-xs font-bold uppercase tracking-wider"
                  style={{ color: "hsl(214, 5%, 55%)" }}
                >
                  LLM Model
                </label>
                <select
                  id="agent-model"
                  value={model}
                  onChange={(e) => setModel(e.target.value)}
                  className="w-full appearance-none rounded-md px-3 py-2 text-sm outline-none"
                  style={{
                    backgroundColor: "hsl(228, 7%, 14%)",
                    color: "hsl(210, 3%, 90%)",
                    border: "1px solid hsl(228, 6%, 30%)",
                  }}
                >
                  {availableModels.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.name}
                    </option>
                  ))}
                </select>
              </div>

              {/* System Prompt */}
              <div className="flex flex-col gap-1.5">
                <label
                  htmlFor="agent-prompt"
                  className="text-xs font-bold uppercase tracking-wider"
                  style={{ color: "hsl(214, 5%, 55%)" }}
                >
                  System Prompt
                </label>
                <textarea
                  id="agent-prompt"
                  value={prompt}
                  onChange={(e) => setPrompt(e.target.value)}
                  placeholder="Describe the agent's role and behavior..."
                  rows={4}
                  className="w-full resize-none rounded-md px-3 py-2 text-sm leading-relaxed outline-none"
                  style={{
                    backgroundColor: "hsl(228, 7%, 14%)",
                    color: "hsl(210, 3%, 90%)",
                    border: "1px solid hsl(228, 6%, 30%)",
                  }}
                />
              </div>

              {/* Save button */}
              <button
                type="button"
                onClick={handleSave}
                disabled={!name.trim() || isSaving || (view === "create" && !providerId)}
                className="flex items-center justify-center gap-2 rounded-md py-2.5 text-sm font-medium transition-opacity hover:opacity-80 disabled:opacity-40"
                style={{ backgroundColor: "hsl(235, 86%, 65%)", color: "#fff" }}
              >
                <Save className="h-4 w-4" />
                <span>{isSaving ? "Saving..." : view === "create" ? "Create Agent" : "Save Changes"}</span>
              </button>

              {/* Error message */}
              {saveError && (
                <div className="text-xs text-red-400 text-center">{saveError}</div>
              )}
            </div>
          </ScrollArea>
        )}
      </div>

    <Dialog
      open={!!deleteAgentPending}
      onOpenChange={(open) => {
        if (!open) {
          setDeleteAgentPending(null)
          setIsDeletingAgent(false)
        }
      }}
    >
      <DialogContent
        className="rounded-lg border-0 p-6 shadow-lg"
        style={{
          backgroundColor: "rgb(49, 51, 56)",
          color: "rgb(255, 255, 255)",
        }}
      >
        <DialogHeader className="space-y-2 text-left">
          <DialogTitle className="text-lg font-semibold text-white">
            Delete Agent
          </DialogTitle>
          <DialogDescription asChild>
            <div className="space-y-1 text-sm" style={{ color: "rgb(163, 163, 163)" }}>
              <p>
                Are you sure you want to delete{" "}
                <span className="font-medium text-white">
                  {deleteAgentPending?.name ?? ""}
                </span>
                ?
              </p>
              <p>This cannot be undone.</p>
            </div>
          </DialogDescription>
        </DialogHeader>
        <DialogFooter className="flex flex-row justify-end gap-2 sm:justify-end">
          <button
            type="button"
            onClick={() => setDeleteAgentPending(null)}
            className="rounded-md px-4 py-2 text-sm font-medium transition-colors hover:opacity-90"
            style={{
              backgroundColor: "rgb(64, 66, 72)",
              color: "rgb(255, 255, 255)",
            }}
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={isDeletingAgent}
            onClick={async () => {
              if (!deleteAgentPending) return
              setIsDeletingAgent(true)
              try {
                await onDeleteAgent(deleteAgentPending.id)
                setDeleteAgentPending(null)
              } catch {
                setIsDeletingAgent(false)
                toast({ title: "Failed to delete agent", variant: "destructive" })
              }
            }}
            className="flex items-center justify-center gap-2 rounded-md px-4 py-2 text-sm font-medium text-white transition-colors hover:opacity-90 disabled:pointer-events-none disabled:opacity-70"
            style={{ backgroundColor: "rgb(220, 53, 69)" }}
          >
            {isDeletingAgent ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              "Delete Agent"
            )}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
    </>
  )

  if (embedded) {
    return content
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="absolute inset-0"
        style={{ backgroundColor: "rgba(0,0,0,0.7)" }}
        onClick={onClose}
        onKeyDown={(e) => { if (e.key === "Escape") onClose?.() }}
        role="button"
        tabIndex={0}
        aria-label="Close dialog"
      />
      {content}
    </div>
  )
}
