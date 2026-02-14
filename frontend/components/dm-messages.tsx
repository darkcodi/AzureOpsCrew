"use client"

import { useEffect, useRef } from "react"
import { useCopilotChatInternal } from "@copilotkit/react-core"
import { useChatContext } from "@copilotkit/react-ui"
import type { MessagesProps } from "@copilotkit/react-ui"
import { StartConversationEmpty } from "@/components/start-conversation-empty"

const DM_EMPTY_SUBTITLE = "Send a message to get started."

/**
 * Custom Messages component for Direct Messages. When there are no messages,
 * shows the same "Start a conversation" empty state as Groups/Chats instead of
 * the default "Send a message to start the conversation..." placeholder.
 */
export function DMMessages(props: MessagesProps) {
  const {
    messages,
    inProgress,
    children,
    RenderMessage,
    AssistantMessage,
    UserMessage,
    ErrorMessage,
    ImageRenderer,
    onRegenerate,
    onCopy,
    onThumbsUp,
    onThumbsDown,
    messageFeedback,
    markdownTagRenderers,
    chatError,
  } = props

  const { icons } = useChatContext()
  const { interrupt } = useCopilotChatInternal()
  const messagesContainerRef = useRef<HTMLDivElement>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" })
  }, [messages.length, inProgress])

  if (messages.length === 0) {
    return (
      <div className="copilotKitMessages">
        <div className="copilotKitMessagesContainer min-h-0 flex-1 flex flex-col justify-center">
          <StartConversationEmpty subtitle={DM_EMPTY_SUBTITLE} />
        </div>
        <footer className="copilotKitMessagesFooter">{children}</footer>
      </div>
    )
  }

  const LoadingIcon = () => <span>{icons.activityIcon}</span>

  return (
    <div className="copilotKitMessages" ref={messagesContainerRef}>
      <div className="copilotKitMessagesContainer">
        {messages.map((message, index) => {
          const isCurrentMessage = index === messages.length - 1
          return (
            <RenderMessage
              key={index}
              message={message}
              messages={messages}
              inProgress={inProgress}
              index={index}
              isCurrentMessage={isCurrentMessage}
              AssistantMessage={AssistantMessage}
              UserMessage={UserMessage}
              ImageRenderer={ImageRenderer}
              onRegenerate={onRegenerate}
              onCopy={onCopy}
              onThumbsUp={onThumbsUp}
              onThumbsDown={onThumbsDown}
              messageFeedback={messageFeedback}
              markdownTagRenderers={markdownTagRenderers}
            />
          )
        })}
        {messages[messages.length - 1]?.role === "user" && inProgress && (
          <LoadingIcon />
        )}
        {interrupt}
        {chatError && ErrorMessage && (
          <ErrorMessage error={chatError} isCurrentMessage />
        )}
      </div>
      <footer className="copilotKitMessagesFooter" ref={messagesEndRef}>
        {children}
      </footer>
    </div>
  )
}
