"use client"

import { useState, useRef, useEffect, type KeyboardEvent } from "react"
import { AlertCircle } from "lucide-react"
import type { InputProps } from "@copilotkit/react-ui"

interface MessageInputProps {
  /** Channel/room name for placeholder, e.g. "general". Ignored if placeholder is set. */
  roomName?: string
  /** Optional placeholder override (e.g. "Message @Agent..." for DMs). */
  placeholder?: string
  onSend: (text: string) => void
  disabled: boolean
}

export function MessageInput({
  roomName = "",
  placeholder: placeholderProp,
  onSend,
  disabled,
}: MessageInputProps) {
  const placeholder =
    placeholderProp ?? (roomName ? `Message #${roomName}` : "Message...")
  const [value, setValue] = useState("")
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto"
      textareaRef.current.style.height =
        Math.min(textareaRef.current.scrollHeight, 200) + "px"
    }
  }, [value])

  const handleSend = () => {
    const trimmed = value.trim()
    if (!trimmed || disabled) return
    onSend(trimmed)
    setValue("")
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto"
    }
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  return (
    <div className="px-4 pb-6 pt-1">
      <div
        className="flex items-end gap-3 rounded-lg px-4 py-2.5"
        style={{ backgroundColor: "hsl(228, 6%, 26%)" }}
      >
        <textarea
          ref={textareaRef}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          rows={1}
          disabled={disabled}
          className="max-h-[200px] min-h-[24px] flex-1 resize-none bg-transparent text-sm leading-relaxed outline-none placeholder:opacity-40"
          style={{ color: "hsl(210, 3%, 90%)" }}
        />

        <button
          type="button"
          className="mb-0.5 shrink-0 transition-opacity hover:opacity-80"
          style={{ color: "hsl(214, 5%, 55%)" }}
          aria-label="Info"
        >
          <AlertCircle className="h-5 w-5" />
        </button>
      </div>
    </div>
  )
}

/** Adapter so MessageInput can be used as CopilotChat's Input (e.g. in Direct Messages). */
export type MessageInputAdapterProps = InputProps & { placeholder: string }

export function MessageInputAdapter({
  inProgress,
  onSend,
  placeholder,
}: MessageInputAdapterProps) {
  const handleSend = (text: string) => {
    onSend(text).catch(() => {})
  }
  return (
    <MessageInput
      placeholder={placeholder}
      onSend={handleSend}
      disabled={inProgress}
    />
  )
}
