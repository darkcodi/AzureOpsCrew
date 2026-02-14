"use client"

interface StartConversationEmptyProps {
  /** Subtitle below "Start a conversation". Defaults to the Groups/Chats copy. */
  subtitle?: string
}

const DEFAULT_SUBTITLE =
  "Send a message and all active agents in this room will respond."

export function StartConversationEmpty({
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
          Start a conversation
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
