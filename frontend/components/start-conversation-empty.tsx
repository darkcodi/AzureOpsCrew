"use client"

interface StartConversationEmptyProps {
  /** Heading text. Defaults to channel "not implemented" message; pass e.g. "Start a conversation" for DMs. */
  title?: string
  /** Subtitle below the main message. Defaults to text that mentions DMs are available. */
  subtitle?: string
}

const DEFAULT_TITLE = "Sorry, not yet implemented"
const DEFAULT_SUBTITLE =
  "This feature (Channels) is not available yet. You can use direct messages (DMs) to chat with agents."

export function StartConversationEmpty({
  title = DEFAULT_TITLE,
  subtitle = DEFAULT_SUBTITLE,
}: StartConversationEmptyProps) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 p-8">
      <div
        className="flex h-16 w-16 items-center justify-center rounded-full text-2xl font-bold"
        style={{ backgroundColor: "hsl(235, 86%, 65%)", color: "#fff" }}
      >
        #
      </div>
      <div className="text-center">
        <h2
          className="text-xl font-bold"
          style={{ color: "hsl(0, 0%, 100%)" }}
        >
          {title}
        </h2>
        <p
          className="mt-1 text-sm"
          style={{ color: "hsl(214, 5%, 55%)" }}
        >
          {subtitle}
        </p>
      </div>
    </div>
  )
}
