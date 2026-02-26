"use client"

import { useState, useRef, useEffect, type KeyboardEvent } from "react"
import { ArrowUp } from "lucide-react"

interface MessageInputProps {
  /** Channel name for placeholder, e.g. "general". Ignored if placeholder is set. */
  channelName?: string
  /** Optional placeholder override (e.g. "Message @Agent..." for DMs). */
  placeholder?: string
  onSend: (text: string) => void
  /** When true, input and send button are disabled (e.g. temporarily for channels). */
  disabled?: boolean
}

export function MessageInput({
  channelName = "",
  placeholder: placeholderProp,
  onSend,
  disabled = false,
}: MessageInputProps) {
  const placeholder =
    placeholderProp ?? (channelName ? `Message #${channelName}` : "Message...")
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
    if (disabled) return
    const trimmed = value.trim()
    if (!trimmed) return
    onSend(trimmed)
    setValue("")
    if (textareaRef.current) {
      textareaRef.current.style.height = "auto"
    }
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (disabled) return
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
          onChange={(e) => !disabled && setValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          rows={1}
          disabled={disabled}
          className="max-h-[200px] min-h-[24px] flex-1 resize-none bg-transparent text-sm leading-relaxed outline-none placeholder:opacity-40 disabled:cursor-not-allowed disabled:opacity-60"
          style={{ color: "hsl(210, 3%, 90%)" }}
        />

        <button
          type="button"
          onClick={handleSend}
          disabled={disabled || !value.trim()}
          className="mb-0.5 shrink-0 transition-opacity hover:opacity-80 disabled:opacity-40 disabled:cursor-not-allowed"
          style={{ color: "hsl(214, 5%, 55%)" }}
          aria-label="Send"
        >
          <ArrowUp className="h-5 w-5" />
        </button>
      </div>
    </div>
  )
}
