/**
 * AGUI (Agent UI) Event Types
 * Based on backend types in backend/src/Api/Endpoints/Dtos/AGUI/AGUIEvent.cs
 *
 * NOTE: Backend uses SNAKE_CASE with underscores (e.g., "TEXT_MESSAGE_CONTENT")
 */

// ===== Event Type Constants (matching backend) =====
export const AGUI_EVENT_TYPES = {
  RUN_STARTED: "RUN_STARTED",
  RUN_FINISHED: "RUN_FINISHED",
  RUN_ERROR: "RUN_ERROR",
  TEXT_MESSAGE_START: "TEXT_MESSAGE_START",
  TEXT_MESSAGE_CONTENT: "TEXT_MESSAGE_CONTENT",
  TEXT_MESSAGE_END: "TEXT_MESSAGE_END",
  TOOL_CALL_START: "TOOL_CALL_START",
  TOOL_CALL_ARGS: "TOOL_CALL_ARGS",
  TOOL_CALL_END: "TOOL_CALL_END",
  TOOL_CALL_RESULT: "TOOL_CALL_RESULT",
  STATE_SNAPSHOT: "STATE_SNAPSHOT",
  STATE_DELTA: "STATE_DELTA",
} as const

/** Base event type with discriminator */
export interface BaseEvent {
  type: string
}

// ===== Lifecycle Events =====

export interface RunStartedEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.RUN_STARTED
  threadId: string
  runId: string
}

export interface RunFinishedEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.RUN_FINISHED
  threadId: string
  runId: string
  result?: unknown
}

export interface RunErrorEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.RUN_ERROR
  message: string
  code?: string
}

// ===== Text Message Events =====

export interface TextMessageStartEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.TEXT_MESSAGE_START
  messageId: string
  role: string
}

export interface TextMessageContentEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.TEXT_MESSAGE_CONTENT
  messageId: string
  delta: string
}

export interface TextMessageEndEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.TEXT_MESSAGE_END
  messageId: string
}

// ===== Tool Call Events =====

export interface ToolCallStartEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.TOOL_CALL_START
  toolCallId: string
  toolCallName: string
  parentMessageId?: string
}

export interface ToolCallArgsEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.TOOL_CALL_ARGS
  toolCallId: string
  delta: string
}

export interface ToolCallEndEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.TOOL_CALL_END
  toolCallId: string
}

export interface ToolCallResultEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.TOOL_CALL_RESULT
  messageId?: string
  toolCallId: string
  content: string
  role?: string
}

// ===== State Events =====

export interface StateSnapshotEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.STATE_SNAPSHOT
  snapshot?: unknown
}

export interface StateDeltaEvent extends BaseEvent {
  type: typeof AGUI_EVENT_TYPES.STATE_DELTA
  delta?: unknown
}

// ===== Union Type =====

export type AGUIEvent =
  | RunStartedEvent
  | RunFinishedEvent
  | RunErrorEvent
  | TextMessageStartEvent
  | TextMessageContentEvent
  | TextMessageEndEvent
  | ToolCallStartEvent
  | ToolCallArgsEvent
  | ToolCallEndEvent
  | ToolCallResultEvent
  | StateSnapshotEvent
  | StateDeltaEvent

// ===== SSE Format for Frontend Compatibility =====

/**
 * Transforms AGUI event to frontend-compatible SSE format
 * Maps TextMessageContentEvent.delta to the text-delta format
 */
export interface TextDeltaEvent {
  type: "text-delta"
  textDelta: string
}

export interface ToolCallEvent {
  type: "tool-call"
  toolCallId: string
  toolCallName: string
  args?: string
  result?: string
}

/**
 * Converts AGUI event to frontend text-delta format
 */
export function toTextDelta(event: AGUIEvent): TextDeltaEvent | null {
  if (event.type === AGUI_EVENT_TYPES.TEXT_MESSAGE_CONTENT) {
    return {
      type: "text-delta",
      textDelta: (event as TextMessageContentEvent).delta,
    }
  }
  return null
}
