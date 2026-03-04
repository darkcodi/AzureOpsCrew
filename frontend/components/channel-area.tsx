"use client"

import { useState, useCallback, useRef, useEffect } from "react"
import type { Channel, Agent, ChatMessage } from "@/lib/agents"
import { ChannelHeader } from "@/components/channel-header"
import { MessageList } from "@/components/message-list"
import { MessageInput } from "@/components/message-input"
import { MemberList } from "@/components/member-list"
import { AgentActivityLog, type ActivityPhase, type ActivityLogEntry } from "@/components/agent-activity-log"
import { TaskTreePanel, type RunData } from "@/components/task-tree-panel"
import { ApprovalDialog, type ApprovalRequest } from "@/components/approval-dialog"
import { RunStatusBar, type RunPhase, type ToolCallInfo } from "@/components/run-status-bar"
import type { AGUIEvent } from "@ag-ui/core"
import { EventType } from "@ag-ui/core"
import type { HumanMember } from "@/lib/humans"

interface ChannelAreaProps {
  channel: Channel
  allAgents: Agent[]
  humans: HumanMember[]
  displayName: string
  onUpdateChannel: (channel: Channel) => void
  onAddAgent: (agent: Agent) => void
  onUpdateAgent: (agent: Agent) => void
  onDeleteAgent: (agentId: string) => void
  onOpenInDM?: (agentId: string, message?: string) => void
}

export function ChannelArea({
  channel,
  allAgents,
  humans,
  displayName,
  onUpdateChannel,
  onAddAgent,
  onUpdateAgent,
  onDeleteAgent,
  onOpenInDM,
}: ChannelAreaProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [streamingAgentId, setStreamingAgentId] = useState<string | null>(null)
  const [streamingContent, setStreamingContent] = useState("")
  const [isProcessing, setIsProcessing] = useState(false)
  const [showMembers, setShowMembers] = useState(true)
  const [runPhase, setRunPhase] = useState<ActivityPhase>("idle")
  const [activeAgentName, setActiveAgentName] = useState<string | null>(null)
  const [activityLogEntries, setActivityLogEntries] = useState<ActivityLogEntry[]>([])
  const [runStartTime, setRunStartTime] = useState<number | null>(null)
  const [elapsedSeconds, setElapsedSeconds] = useState(0)
  const abortRef = useRef<AbortController | null>(null)
  const messageCounterRef = useRef(0)
  const logEntryCounterRef = useRef(0)

  // Execution engine state
  const [executionRunId, setExecutionRunId] = useState<string | null>(null)
  const [executionRun, setExecutionRun] = useState<RunData | null>(null)
  const [pendingApprovals, setPendingApprovals] = useState<ApprovalRequest[]>([])
  const [showTaskTree, setShowTaskTree] = useState(false)

  // Elapsed time counter
  useEffect(() => {
    if (!runStartTime) return
    const interval = setInterval(() => {
      setElapsedSeconds(Math.floor((Date.now() - runStartTime) / 1000))
    }, 1000)
    return () => clearInterval(interval)
  }, [runStartTime])

  // Poll execution run status
  useEffect(() => {
    if (!executionRunId) return
    let cancelled = false

    const poll = async () => {
      try {
        const res = await fetch(`/api/runs/${executionRunId}`)
        if (!res.ok || cancelled) return
        const data: RunData = await res.json()
        setExecutionRun(data)
        setShowTaskTree(data.tasks.length > 0)

        // Fetch pending approvals
        if (data.pendingApprovals.length > 0) {
          const appRes = await fetch(`/api/runs/${executionRunId}/approvals`)
          if (appRes.ok && !cancelled) {
            const approvals: ApprovalRequest[] = await appRes.json()
            setPendingApprovals(approvals.filter(a => a.status === "Pending"))
          }
        } else {
          setPendingApprovals([])
        }

        // Stop polling if terminal
        if (["Succeeded", "Failed", "BudgetExhausted", "Cancelled"].includes(data.status)) {
          return
        }
      } catch {
        // Ignore fetch errors
      }
    }

    poll()
    const interval = setInterval(poll, 3000)
    return () => {
      cancelled = true
      clearInterval(interval)
    }
  }, [executionRunId])

  const activeAgents = allAgents.filter((a) => channel.agentIds.includes(a.id))

  const handleToggleAgent = async (agentId: string) => {
    const isAdding = !channel.agentIds.includes(agentId)
    const endpoint = isAdding
      ? `/api/channels/${channel.id}/add-agent`
      : `/api/channels/${channel.id}/remove-agent`

    const response = await fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ agentId }),
    })

    if (!response.ok) {
      const data = await response.json().catch(() => ({}))
      throw new Error(data.error ?? "Failed to update agent in channel")
    }

    const newIds = isAdding
      ? [...channel.agentIds, agentId]
      : channel.agentIds.filter((id) => id !== agentId)
    onUpdateChannel({ ...channel, agentIds: newIds })
  }

  const handleKickMember = useCallback(
    async (agentId: string) => {
      const response = await fetch(
        `/api/channels/${channel.id}/remove-agent`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ agentId }),
        }
      )

      if (!response.ok) {
        const data = await response.json().catch(() => ({}))
        throw new Error(data.error ?? "Failed to kick member from channel")
      }

      const newIds = channel.agentIds.filter((id) => id !== agentId)
      onUpdateChannel({ ...channel, agentIds: newIds })
    },
    [channel, onUpdateChannel]
  )

  const handleSend = useCallback(
    async (text: string) => {
      if (isProcessing || activeAgents.length === 0) return

      setIsProcessing(true)
      setRunPhase("triaging")
      setActivityLogEntries([])
      setRunStartTime(Date.now())
      setElapsedSeconds(0)
      setActiveAgentName(null)

      // Log phase change
      setActivityLogEntries(prev => [...prev, {
        id: `log-${logEntryCounterRef.current++}`,
        timestamp: new Date(),
        type: "phase-change",
        phase: "triaging",
      }])

      const userMsg: ChatMessage = {
        id: "user-" + Date.now(),
        role: "user",
        content: text,
        timestamp: new Date(),
      }

      setMessages((prev) => [...prev, userMsg])

      // Build conversation history
      // The backend's workflow agent handles context across agents
      const history = [
        ...messages.map((m) => ({
          role: m.role,
          content: m.content,
        })),
        { role: "user", content: text },
      ]

      abortRef.current = new AbortController()

      try {
        // SINGLE call to backend - no loop!
        // The backend workflow handles running all agents in sequence
        const response = await fetch(`/api/channel-agui/${channel.id}`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            messages: history,
          }),
          signal: abortRef.current.signal,
        })

        if (!response.ok || !response.body) {
          throw new Error("Failed to get AGUI response")
        }

        const reader = response.body.getReader()
        const decoder = new TextDecoder()
        let buffer = ""

        // Track the current message being streamed
        let currentMessageId: string | null = null
        let currentContent = ""
        let currentAgentName: string | null = null

        while (true) {
          const { done, value } = await reader.read()
          if (done) break

          buffer += decoder.decode(value, { stream: true })
          const lines = buffer.split("\n")
          buffer = lines.pop() || ""

          for (const line of lines) {
            const trimmed = line.trim()
            if (trimmed.startsWith("data:")) {
              const data = trimmed.slice(5).trim()
              if (data === "[DONE]") continue
              try {
                const event: AGUIEvent = JSON.parse(data)

                // TEXT_MESSAGE_START: A new message starts
                if (event.type === EventType.TEXT_MESSAGE_START) {
                  currentMessageId = event.messageId
                  currentContent = ""
                  // Extract authorName from message ID (format: "AuthorName|OriginalMessageId")
                  const pipeIndex = event.messageId.indexOf("|")
                  currentAgentName = pipeIndex !== -1 ? event.messageId.slice(0, pipeIndex) : null

                  // Show typing indicator for the agent
                  const agent = currentAgentName
                    ? activeAgents.find((a) => a.name === currentAgentName)
                    : null
                  setStreamingAgentId(agent?.id ?? "channel")
                  setStreamingContent("")
                  setActiveAgentName(currentAgentName)

                  // Log agent switch
                  if (currentAgentName) {
                    setActivityLogEntries(prev => [...prev, {
                      id: `log-${logEntryCounterRef.current++}`,
                      timestamp: new Date(),
                      type: "agent-switch",
                      agentName: currentAgentName,
                    }])
                    setActivityLogEntries(prev => [...prev, {
                      id: `log-${logEntryCounterRef.current++}`,
                      timestamp: new Date(),
                      type: "message-start",
                      agentName: currentAgentName,
                    }])
                  }
                }
                // TEXT_MESSAGE_CONTENT: Streaming content
                else if (event.type === EventType.TEXT_MESSAGE_CONTENT) {
                  const incomingMessageId = event.messageId ?? null

                  // If the backend doesn't emit TEXT_MESSAGE_START, initialize implicitly
                  if (!currentMessageId) {
                    currentMessageId = incomingMessageId ?? `implicit-${Date.now()}`
                    currentContent = ""

                    if (!currentAgentName && incomingMessageId) {
                      const pipeIndex = incomingMessageId.indexOf("|")
                      currentAgentName = pipeIndex !== -1 ? incomingMessageId.slice(0, pipeIndex) : null
                    }

                    const fallbackAgent = !currentAgentName
                      ? activeAgents.find((a) => a.name === "Manager")
                      : null
                    const agent = currentAgentName
                      ? activeAgents.find((a) => a.name === currentAgentName)
                      : fallbackAgent
                    setStreamingAgentId(agent?.id ?? "channel")
                    setStreamingContent("")
                    setActiveAgentName(currentAgentName)

                    if (currentAgentName) {
                      setActivityLogEntries(prev => [...prev, {
                        id: `log-${logEntryCounterRef.current++}`,
                        timestamp: new Date(),
                        type: "message-start",
                        agentName: currentAgentName,
                      }])
                    }
                  }

                  // If a new messageId arrives without a start event, flush the previous message first
                  if (incomingMessageId && incomingMessageId !== currentMessageId) {
                    if (currentContent) {
                      const fallbackAgent = !currentAgentName
                        ? activeAgents.find((a) => a.name === "Manager")
                        : null
                      const agent = currentAgentName
                        ? activeAgents.find((a) => a.name === currentAgentName)
                        : fallbackAgent
                      const agentMsg: ChatMessage = {
                        id: `channel-${channel.id}-${messageCounterRef.current++}`,
                        role: "assistant",
                        content: currentContent,
                        timestamp: new Date(),
                        agentId: agent?.id,
                      }
                      setMessages((prev) => [...prev, agentMsg])
                    }

                    const pipeIndex = incomingMessageId.indexOf("|")
                    currentAgentName = pipeIndex !== -1 ? incomingMessageId.slice(0, pipeIndex) : null
                    currentMessageId = incomingMessageId
                    currentContent = ""

                    const fallbackAgent = !currentAgentName
                      ? activeAgents.find((a) => a.name === "Manager")
                      : null
                    const agent = currentAgentName
                      ? activeAgents.find((a) => a.name === currentAgentName)
                      : fallbackAgent
                    setStreamingAgentId(agent?.id ?? "channel")
                    setStreamingContent("")
                    setActiveAgentName(currentAgentName)

                    if (currentAgentName) {
                      setActivityLogEntries(prev => [...prev, {
                        id: `log-${logEntryCounterRef.current++}`,
                        timestamp: new Date(),
                        type: "message-start",
                        agentName: currentAgentName,
                      }])
                    }
                  }

                  currentContent += event.delta
                  setStreamingContent(currentContent)

                  // Detect run phase from structured markers in content
                  let newPhase: ActivityPhase | null = null
                  if (currentContent.includes("[TRIAGE]")) newPhase = "triaging"
                  else if (currentContent.includes("[PLAN]")) newPhase = "investigating"
                  else if (currentContent.includes("[EVIDENCE]")) newPhase = "investigating"
                  else if (currentContent.includes("[APPROVAL REQUIRED]")) newPhase = "waiting-approval"
                  else if (currentContent.includes("[RESOLVED]")) newPhase = "resolved"

                  if (newPhase) {
                    setRunPhase(newPhase)
                    setActivityLogEntries(prev => [...prev, {
                      id: `log-${logEntryCounterRef.current++}`,
                      timestamp: new Date(),
                      type: "phase-change",
                      phase: newPhase,
                    }])
                  }
                }
                // TEXT_MESSAGE_END: Message finished
                else if (event.type === EventType.TEXT_MESSAGE_END) {
                  if (event.messageId === currentMessageId && currentContent) {
                    // Map authorName to agentId
                    const fallbackAgent = !currentAgentName
                      ? activeAgents.find((a) => a.name === "Manager")
                      : null
                    const agent = currentAgentName
                      ? activeAgents.find((a) => a.name === currentAgentName)
                      : fallbackAgent
                    const agentMsg: ChatMessage = {
                      id: `channel-${channel.id}-${messageCounterRef.current++}`,
                      role: "assistant",
                      content: currentContent,
                      timestamp: new Date(),
                      agentId: agent?.id,
                    }
                    setMessages((prev) => [...prev, agentMsg])

                    // Log message end
                    if (currentAgentName) {
                      setActivityLogEntries(prev => [...prev, {
                        id: `log-${logEntryCounterRef.current++}`,
                        timestamp: new Date(),
                        type: "message-end",
                        agentName: currentAgentName,
                      }])
                    }

                    // Reset for next message
                    currentMessageId = null
                    currentContent = ""
                    currentAgentName = null
                    setStreamingAgentId(null)
                    setStreamingContent("")
                  }
                }
                // TOOL_CALL_START: Agent is calling a tool
                else if (event.type === EventType.TOOL_CALL_START) {
                  setRunPhase("executing-tools")
                  const tcId = event.toolCallId ?? `tc-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
                  const toolName = event.toolCallName ?? "unknown"

                  // Log tool call start
                  setActivityLogEntries(prev => {
                    // Prevent duplicates
                    if (prev.some(e => e.type === "tool-call" && e.toolName === toolName && e.toolStatus === "running")) {
                      return prev
                    }
                    return [...prev, {
                      id: `log-${logEntryCounterRef.current++}`,
                      timestamp: new Date(),
                      type: "tool-call",
                      toolName,
                      agentName: currentAgentName ?? "Agent",
                      toolStatus: "running" as const,
                    }]
                  })

                  // Also log phase change if not already in executing-tools
                  setActivityLogEntries(prev => {
                    const lastPhaseChange = [...prev].reverse().find(e => e.type === "phase-change")
                    if (!lastPhaseChange || lastPhaseChange.phase !== "executing-tools") {
                      return [...prev, {
                        id: `log-${logEntryCounterRef.current++}`,
                        timestamp: new Date(),
                        type: "phase-change",
                        phase: "executing-tools" as ActivityPhase,
                      }]
                    }
                    return prev
                  })
                }
                // TOOL_CALL_END: Tool finished
                else if (event.type === EventType.TOOL_CALL_END) {
                  const toolName = event.toolCallName ?? "unknown"

                  // Update tool call status in log
                  setActivityLogEntries(prev => {
                    // Find the most recent running tool call with this name
                    const entries = [...prev]
                    for (let i = entries.length - 1; i >= 0; i--) {
                      if (entries[i].type === "tool-call" &&
                        entries[i].toolName === toolName &&
                        entries[i].toolStatus === "running") {
                        // Mark as completed
                        entries[i] = {
                          ...entries[i],
                          toolStatus: "completed" as const,
                        }
                        break
                      }
                    }
                    return entries
                  })

                  setRunPhase("investigating")
                  setActivityLogEntries(prev => [...prev, {
                    id: `log-${logEntryCounterRef.current++}`,
                    timestamp: new Date(),
                    type: "phase-change",
                    phase: "investigating" as ActivityPhase,
                  }])
                }
                // RUN_FINISHED: All done
                else if (event.type === EventType.RUN_FINISHED) {
                  if (currentContent) {
                    const fallbackAgent = !currentAgentName
                      ? activeAgents.find((a) => a.name === "Manager")
                      : null
                    const agent = currentAgentName
                      ? activeAgents.find((a) => a.name === currentAgentName)
                      : fallbackAgent
                    const agentMsg: ChatMessage = {
                      id: `channel-${channel.id}-${messageCounterRef.current++}`,
                      role: "assistant",
                      content: currentContent,
                      timestamp: new Date(),
                      agentId: agent?.id,
                    }
                    setMessages((prev) => [...prev, agentMsg])
                  }

                  setStreamingAgentId(null)
                  setStreamingContent("")
                  setRunPhase("resolved")
                  setRunStartTime(null)

                  setActivityLogEntries(prev => [...prev, {
                    id: `log-${logEntryCounterRef.current++}`,
                    timestamp: new Date(),
                    type: "phase-change",
                    phase: "resolved" as ActivityPhase,
                  }])
                }
                // RUN_ERROR: Error occurred
                else if (event.type === EventType.RUN_ERROR) {
                  console.error("AGUI run error:", event.message)
                  setRunPhase("error")
                  setRunStartTime(null)

                  setActivityLogEntries(prev => [...prev, {
                    id: `log-${logEntryCounterRef.current++}`,
                    timestamp: new Date(),
                    type: "error",
                    error: event.message ?? "Unknown error",
                  }])
                }
              } catch {
                /* skip invalid events */
              }
            }
          }
        }
      } catch (err) {
        if (!(err instanceof DOMException && err.name === "AbortError")) {
          console.error("Error processing channel stream:", err)

          // Log the error
          setActivityLogEntries(prev => [...prev, {
            id: `log-${logEntryCounterRef.current++}`,
            timestamp: new Date(),
            type: "error",
            error: err instanceof Error ? err.message : "Unknown error occurred",
          }])

          setRunPhase("error")

          // Add a message to the chat with error info
          const errorMsg: ChatMessage = {
            id: `error-${Date.now()}`,
            role: "assistant",
            content: `⚠️ An error occurred during processing. ${err instanceof Error ? err.message : "Please try again."}\n\nPartial results may have been saved above.`,
            timestamp: new Date(),
          }
          setMessages((prev) => [...prev, errorMsg])
        }
      } finally {
        setStreamingAgentId(null)
        setStreamingContent("")
        setIsProcessing(false)
        setActiveAgentName(null)
        setRunStartTime(null)
      }
    },
    [isProcessing, activeAgents, messages, channel.id, channel.name]
  )

  const handleApprove = useCallback(
    async (action: string) => {
      // Send approval as a user message to continue the run
      const approvalText = `[APPROVED] ${action}`
      await handleSend(approvalText)
    },
    [handleSend]
  )

  const handleApprovalDecision = useCallback(
    async (approvalId: string, approved: boolean, reason?: string) => {
      if (!executionRunId) return
      try {
        const res = await fetch(`/api/runs/${executionRunId}/approve`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            approvalRequestId: approvalId,
            approved,
            reason,
          }),
        })
        if (res.ok) {
          const data: RunData = await res.json()
          setExecutionRun(data)
          setPendingApprovals(prev => prev.filter(a => a.id !== approvalId))
        }
      } catch (err) {
        console.error("Failed to submit approval:", err)
      }
    },
    [executionRunId]
  )

  return (
    <div className="flex flex-1 overflow-hidden">
      <div
        className="flex flex-1 flex-col"
        style={{ backgroundColor: "hsl(228, 6%, 22%)" }}
      >
        <ChannelHeader
          channel={channel}
          onManageAgents={() => {/* gear icon in header - no-op for now, wrench handles it */ }}
          showMembers={showMembers}
          onToggleMembers={() => setShowMembers((prev) => !prev)}
        />

        <AgentActivityLog
          phase={runPhase}
          activeAgentName={activeAgentName}
          entries={activityLogEntries}
          elapsedSeconds={elapsedSeconds}
          defaultExpanded={false}
        />

        {/* Execution engine: task tree panel */}
        {showTaskTree && executionRunId && (
          <div className="mx-4 mt-2">
            <TaskTreePanel
              runId={executionRunId}
              run={executionRun}
              onRefresh={() => setExecutionRunId(prev => prev)}
            />
          </div>
        )}

        {/* Execution engine: approval dialogs */}
        {pendingApprovals.map((approval) => (
          <ApprovalDialog
            key={approval.id}
            runId={executionRunId!}
            approval={approval}
            onDecision={(approved, reason) =>
              handleApprovalDecision(approval.id, approved, reason)
            }
            isProcessing={isProcessing}
          />
        ))}

        <MessageList
          messages={messages}
          agents={allAgents}
          streamingAgentId={streamingAgentId}
          streamingContent={streamingContent}
          onApprove={handleApprove}
        />

        <MessageInput
          channelName={channel.name}
          onSend={handleSend}
          disabled={isProcessing}
        />
      </div>

      {showMembers && (
        <MemberList
          allAgents={allAgents}
          humans={humans}
          activeAgentIds={channel.agentIds}
          streamingAgentId={streamingAgentId}
          displayName={displayName}
          onToggleAgent={handleToggleAgent}
          onOpenInDM={onOpenInDM}
          onKickMember={handleKickMember}
        />
      )}
    </div>
  )
}
