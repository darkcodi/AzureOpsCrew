# Frontend — Детальный план изменений

## Общий подход

Оставляем Discord-стиль, убираем всё лишнее (Settings, Direct Messages, Auth pages, Provider CRUD). 
Один канал "Ops Room", захардкоженные агенты, фокус на чат с мультиагентной системой.

---

## 1. Файлы на УДАЛЕНИЕ

```
frontend/components/settings/              (весь каталог)
frontend/components/direct-messages-area.tsx
frontend/components/direct-messages-right-pane.tsx
frontend/components/direct-messages-sidebar.tsx
frontend/components/direct-messages-view.tsx
frontend/components/dm-messages.tsx
frontend/components/manage-agents-dialog.tsx
frontend/components/auth-start-redirect.tsx
frontend/app/login/                        (весь каталог) 
frontend/app/signup/                       (весь каталог)
frontend/app/api/auth/                     (весь каталог — переписываем)
frontend/app/api/settings/
frontend/app/api/providers/
frontend/app/api/chat/
```

## 2. Файлы на УПРОЩЕНИЕ

```
frontend/components/icon-sidebar.tsx      → убрать DM и Settings вкладки
frontend/components/channel-sidebar.tsx   → статичный, без создания/удаления каналов
frontend/components/home-page-client.tsx  → упростить, убрать DM/Settings  
frontend/components/member-list.tsx       → только отображение (без toggle/kick)
frontend/app/page.tsx                     → auto-login + redirect to chat
```

## 3. Файлы ОСТАВИТЬ КАК ЕСТЬ

```
frontend/components/channel-area.tsx       (можно слегка доработать)
frontend/components/channel-header.tsx
frontend/components/message-list.tsx
frontend/components/message-input.tsx
frontend/components/copilot-actions.tsx    ← КЛЮЧЕВОЙ: интерактивные карточки
frontend/components/copilotkit-provider.tsx
frontend/components/start-conversation-empty.tsx
frontend/components/agent-profile-popover.tsx
frontend/components/theme-provider.tsx
frontend/components/ui/                   (все UI компоненты)
frontend/contexts/agent-runtime-context.tsx
frontend/lib/utils.ts
frontend/hooks/
```

---

## 4. Детальные изменения по файлам

### 4.1 `app/page.tsx` (ПЕРЕПИСАТЬ)

Вместо проверки авторизации — auto-login при первом заходе:

```tsx
import { cookies } from "next/headers"
import { redirect } from "next/navigation"
import HomePageClient from "@/components/home-page-client"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:42100"

export default async function Page() {
  const cookieStore = await cookies()
  let token = cookieStore.get("access_token")?.value

  // Auto-login: если нет токена, логинимся автоматически
  if (!token) {
    try {
      const res = await fetch(`${BACKEND_API_URL}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({}), // demo user, no credentials needed
      })
      
      if (res.ok) {
        const data = await res.json()
        token = data.accessToken
        // Сохраняем токен в cookie (через response headers)
        // В Next.js server component нельзя setcookie напрямую,
        // поэтому делаем через API route
      }
    } catch (e) {
      console.error("Auto-login failed:", e)
    }
  }

  return <HomePageClient />
}
```

> **Примечание**: Для auto-login лучше использовать API route `/api/auth/auto-login` 
> который выставит cookie и redirect'нет.

### 4.2 `app/api/auth/auto-login/route.ts` (НОВЫЙ)

```typescript
import { NextRequest, NextResponse } from "next/server"

const BACKEND_API_URL = process.env.BACKEND_API_URL ?? "http://localhost:42100"

export async function GET(req: NextRequest) {
  try {
    const res = await fetch(`${BACKEND_API_URL}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({}),
    })

    if (!res.ok) {
      return NextResponse.json({ error: "Login failed" }, { status: 500 })
    }

    const data = await res.json()
    
    const response = NextResponse.redirect(new URL("/", req.url))
    response.cookies.set("access_token", data.accessToken, {
      httpOnly: true,
      secure: process.env.NODE_ENV === "production",
      sameSite: "lax",
      maxAge: 60 * 60 * 8, // 8 hours
      path: "/",
    })
    
    return response
  } catch (error) {
    console.error("Auto-login error:", error)
    return NextResponse.json({ error: "Internal error" }, { status: 500 })
  }
}
```

### 4.3 `components/home-page-client.tsx` (УПРОЩЁННЫЙ)

```tsx
"use client"

import { useState, useCallback, useEffect } from "react"
import type { Agent, Channel } from "@/lib/agents"
import { IconSidebar } from "@/components/icon-sidebar"
import { ChannelSidebar } from "@/components/channel-sidebar"
import { ChannelArea } from "@/components/channel-area"

export default function HomePageClient() {
  const [agents, setAgents] = useState<Agent[]>([])
  const [channels, setChannels] = useState<Channel[]>([])
  const [activeChannelId, setActiveChannelId] = useState<string>("")
  const [isLoading, setIsLoading] = useState(true)

  const activeChannel = channels.find((c) => c.id === activeChannelId) ?? channels[0]

  // Auto-login check
  useEffect(() => {
    async function checkAuth() {
      try {
        const res = await fetch("/api/auth/me")
        if (!res.ok) {
          // Redirect to auto-login
          window.location.href = "/api/auth/auto-login"
          return
        }
      } catch {
        window.location.href = "/api/auth/auto-login"
        return
      }
    }
    checkAuth()
  }, [])

  // Load agents
  useEffect(() => {
    async function loadAgents() {
      try {
        const res = await fetch("/api/agents")
        if (res.ok) {
          const data: Agent[] = await res.json()
          setAgents(data)
        }
      } catch (e) {
        console.error("Failed to load agents:", e)
      }
    }
    loadAgents()
  }, [])

  // Load channels
  useEffect(() => {
    async function loadChannels() {
      try {
        const res = await fetch("/api/channels")
        if (res.ok) {
          const data: Channel[] = await res.json()
          setChannels(data)
          if (data.length > 0) setActiveChannelId(data[0].id)
        }
      } catch (e) {
        console.error("Failed to load channels:", e)
      } finally {
        setIsLoading(false)
      }
    }
    loadChannels()
  }, [])

  if (isLoading) {
    return (
      <main className="flex h-dvh w-full items-center justify-center"
            style={{ backgroundColor: "hsl(228, 6%, 22%)" }}>
        <div style={{ color: "hsl(214, 5%, 55%)" }}>Loading...</div>
      </main>
    )
  }

  return (
    <main className="flex h-dvh w-full">
      <IconSidebar />
      
      <ChannelSidebar
        channels={channels}
        activeChannelId={activeChannelId}
        onChannelSelect={setActiveChannelId}
      />

      {activeChannel ? (
        <ChannelArea
          key={activeChannel.id}
          channel={activeChannel}
          allAgents={agents}
        />
      ) : (
        <div className="flex flex-1 items-center justify-center"
             style={{ backgroundColor: "hsl(228, 6%, 22%)" }}>
          <div style={{ color: "hsl(214, 5%, 55%)" }}>No channels found</div>
        </div>
      )}
    </main>
  )
}
```

### 4.4 `components/icon-sidebar.tsx` (УПРОЩЁННЫЙ)

Убрать DM и Settings. Оставить только:
- Логотип/иконку приложения
- Channels tab (активна всегда)

```tsx
"use client"

import { Hash, Cpu } from "lucide-react"

export function IconSidebar() {
  return (
    <div
      className="flex w-[72px] flex-col items-center gap-2 py-3"
      style={{ backgroundColor: "hsl(228, 6%, 13%)" }}
    >
      {/* Logo */}
      <div className="mb-2 flex h-12 w-12 items-center justify-center rounded-2xl"
           style={{ backgroundColor: "hsl(235, 86%, 65%)" }}>
        <Cpu className="h-6 w-6 text-white" />
      </div>
      
      <div className="mx-2 mb-2 h-[2px] w-8 rounded-full"
           style={{ backgroundColor: "hsl(228, 6%, 20%)" }} />

      {/* Channels */}
      <div className="flex h-12 w-12 items-center justify-center rounded-2xl transition-all"
           style={{ backgroundColor: "hsl(235, 86%, 65%)" }}
           title="Channels">
        <Hash className="h-5 w-5 text-white" />
      </div>
    </div>
  )
}
```

### 4.5 `components/channel-sidebar.tsx` (УПРОЩЁННЫЙ)

Убрать создание/удаление каналов. Просто список каналов (один "Ops Room"):

```tsx
"use client"

import type { Channel } from "@/lib/agents"
import { Hash } from "lucide-react"

interface ChannelSidebarProps {
  channels: Channel[]
  activeChannelId: string
  onChannelSelect: (id: string) => void
}

export function ChannelSidebar({
  channels,
  activeChannelId,
  onChannelSelect,
}: ChannelSidebarProps) {
  return (
    <div
      className="flex w-60 flex-col"
      style={{ backgroundColor: "hsl(228, 6%, 17%)" }}
    >
      {/* Header */}
      <div className="flex h-12 items-center border-b px-4"
           style={{ borderColor: "hsl(228, 6%, 13%)" }}>
        <h2 className="text-base font-semibold" style={{ color: "hsl(0, 0%, 100%)" }}>
          Azure Ops Crew
        </h2>
      </div>

      {/* Channel list */}
      <div className="flex-1 overflow-y-auto px-2 pt-4">
        <div className="mb-1 px-2 text-xs font-semibold uppercase tracking-wide"
             style={{ color: "hsl(214, 5%, 55%)" }}>
          Channels
        </div>
        {channels.map((channel) => {
          const isActive = channel.id === activeChannelId
          return (
            <button
              key={channel.id}
              onClick={() => onChannelSelect(channel.id)}
              className="mt-0.5 flex w-full items-center gap-1.5 rounded-md px-2 py-1.5 text-left text-sm transition-colors"
              style={{
                color: isActive ? "hsl(0, 0%, 100%)" : "hsl(214, 5%, 55%)",
                backgroundColor: isActive ? "hsl(228, 6%, 25%)" : "transparent",
              }}
            >
              <Hash className="h-4 w-4 shrink-0 opacity-60" />
              {channel.name}
            </button>
          )
        })}
      </div>
    </div>
  )
}
```

### 4.6 `components/channel-area.tsx` (ДОРАБОТКА)

Основные изменения:
1. Убрать `handleToggleAgent`, `handleKickMember` — агенты фиксированные
2. Упростить `handleSend` — вызывать `/api/crew-chat/{channelId}` (или оставить `/api/channel-agui/{channelId}`)
3. Убрать `onAddAgent`, `onUpdateAgent`, `onDeleteAgent`, `onOpenInDM` props

```tsx
"use client"

import { useState, useCallback, useRef } from "react"
import type { Channel, Agent, ChatMessage } from "@/lib/agents"
import { ChannelHeader } from "@/components/channel-header"
import { MessageList } from "@/components/message-list"
import { MessageInput } from "@/components/message-input"
import { MemberList } from "@/components/member-list"
import type { AGUIEvent } from "@ag-ui/core"
import { EventType } from "@ag-ui/core"

interface ChannelAreaProps {
  channel: Channel
  allAgents: Agent[]
}

export function ChannelArea({ channel, allAgents }: ChannelAreaProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [streamingAgentId, setStreamingAgentId] = useState<string | null>(null)
  const [streamingContent, setStreamingContent] = useState("")
  const [isProcessing, setIsProcessing] = useState(false)
  const [showMembers, setShowMembers] = useState(true)
  const abortRef = useRef<AbortController | null>(null)

  const activeAgents = allAgents.filter((a) => channel.agentIds.includes(a.id))

  const handleSend = useCallback(
    async (text: string) => {
      if (isProcessing || activeAgents.length === 0) return
      setIsProcessing(true)

      const userMsg: ChatMessage = {
        id: "user-" + Date.now(),
        role: "user",
        content: text,
        timestamp: new Date(),
      }
      setMessages((prev) => [...prev, userMsg])

      const history = [
        ...messages.map((m) => ({ role: m.role, content: m.content })),
        { role: "user", content: text },
      ]

      abortRef.current = new AbortController()

      try {
        const response = await fetch(`/api/channel-agui/${channel.id}`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ messages: history }),
          signal: abortRef.current.signal,
        })

        if (!response.ok || !response.body) throw new Error("Failed to get response")

        const reader = response.body.getReader()
        const decoder = new TextDecoder()
        let buffer = ""
        let currentMessageId: string | null = null
        let currentContent = ""
        let currentAgentName: string | null = null
        let messageIndex = 0

        while (true) {
          const { done, value } = await reader.read()
          if (done) break

          buffer += decoder.decode(value, { stream: true })
          const lines = buffer.split("\n")
          buffer = lines.pop() || ""

          for (const line of lines) {
            const trimmed = line.trim()
            if (!trimmed.startsWith("data:")) continue
            const data = trimmed.slice(5).trim()
            if (data === "[DONE]") continue

            try {
              const event: AGUIEvent = JSON.parse(data)

              if (event.type === EventType.TEXT_MESSAGE_START) {
                currentMessageId = event.messageId
                currentContent = ""
                const pipeIndex = event.messageId.indexOf("|")
                currentAgentName = pipeIndex !== -1 
                  ? event.messageId.slice(0, pipeIndex) 
                  : null

                const agent = currentAgentName
                  ? activeAgents.find((a) => a.name === currentAgentName)
                  : null
                setStreamingAgentId(agent?.id ?? "channel")
                setStreamingContent("")
              }
              else if (event.type === EventType.TEXT_MESSAGE_CONTENT) {
                if (event.messageId === currentMessageId) {
                  currentContent += event.delta
                  setStreamingContent(currentContent)
                }
              }
              else if (event.type === EventType.TEXT_MESSAGE_END) {
                if (event.messageId === currentMessageId && currentContent) {
                  const agent = currentAgentName
                    ? activeAgents.find((a) => a.name === currentAgentName)
                    : null
                  const agentMsg: ChatMessage = {
                    id: "msg-" + messageIndex++,
                    role: "assistant",
                    content: currentContent,
                    timestamp: new Date(),
                    agentId: agent?.id,
                  }
                  setMessages((prev) => [...prev, agentMsg])
                  currentMessageId = null
                  currentContent = ""
                  currentAgentName = null
                  setStreamingAgentId(null)
                  setStreamingContent("")
                }
              }
              else if (event.type === EventType.RUN_FINISHED) {
                setStreamingAgentId(null)
                setStreamingContent("")
              }
            } catch { /* skip invalid */ }
          }
        }
      } catch (err) {
        if (!(err instanceof DOMException && err.name === "AbortError")) {
          console.error("Stream error:", err)
        }
      } finally {
        setStreamingAgentId(null)
        setStreamingContent("")
        setIsProcessing(false)
      }
    },
    [isProcessing, activeAgents, messages, channel.id]
  )

  return (
    <div className="flex flex-1 overflow-hidden">
      <div className="flex flex-1 flex-col" style={{ backgroundColor: "hsl(228, 6%, 22%)" }}>
        <ChannelHeader
          channel={channel}
          showMembers={showMembers}
          onToggleMembers={() => setShowMembers((p) => !p)}
        />
        <MessageList
          messages={messages}
          agents={allAgents}
          streamingAgentId={streamingAgentId}
          streamingContent={streamingContent}
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
          activeAgentIds={channel.agentIds}
          streamingAgentId={streamingAgentId}
        />
      )}
    </div>
  )
}
```

### 4.7 `components/member-list.tsx` (УПРОЩЁННЫЙ)

Только отображение списка агентов в канале. Без toggle, kick, humans.

```tsx
"use client"

import type { Agent } from "@/lib/agents"
import { AgentProfilePopover } from "@/components/agent-profile-popover"

interface MemberListProps {
  allAgents: Agent[]
  activeAgentIds: string[]
  streamingAgentId: string | null
}

export function MemberList({
  allAgents,
  activeAgentIds,
  streamingAgentId,
}: MemberListProps) {
  const activeAgents = allAgents.filter((a) => activeAgentIds.includes(a.id))

  return (
    <div className="w-60 overflow-y-auto border-l px-3 py-4"
         style={{ 
           backgroundColor: "hsl(228, 6%, 17%)", 
           borderColor: "hsl(228, 6%, 13%)" 
         }}>
      
      {/* User */}
      <div className="mb-1 px-2 text-xs font-semibold uppercase tracking-wide"
           style={{ color: "hsl(214, 5%, 55%)" }}>
        You
      </div>
      <div className="mb-4 flex items-center gap-2 rounded-md px-2 py-1.5">
        <div className="flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold"
             style={{ backgroundColor: "hsl(168, 76%, 42%)", color: "#fff" }}>
          U
        </div>
        <span className="text-sm" style={{ color: "hsl(0, 0%, 100%)" }}>
          Demo User
        </span>
        <span className="ml-auto h-2 w-2 rounded-full" 
              style={{ backgroundColor: "#43b581" }} />
      </div>

      {/* Agents */}
      <div className="mb-1 px-2 text-xs font-semibold uppercase tracking-wide"
           style={{ color: "hsl(214, 5%, 55%)" }}>
        Agents — {activeAgents.length}
      </div>
      {activeAgents.map((agent) => {
        const isStreaming = streamingAgentId === agent.id
        return (
          <div key={agent.id}
               className="flex items-center gap-2 rounded-md px-2 py-1.5 transition-colors"
               style={{
                 backgroundColor: isStreaming ? "hsl(228, 6%, 25%)" : "transparent",
               }}>
            <div className="relative">
              <div className="flex h-8 w-8 items-center justify-center rounded-full text-xs font-bold"
                   style={{ backgroundColor: agent.color, color: "#fff" }}>
                {agent.avatar}
              </div>
              {isStreaming && (
                <span className="absolute -bottom-0.5 -right-0.5 h-3 w-3 rounded-full border-2"
                      style={{ 
                        borderColor: "hsl(228, 6%, 17%)",
                        backgroundColor: "#faa61a"
                      }} />
              )}
            </div>
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm" 
                   style={{ color: "hsl(0, 0%, 100%)" }}>
                {agent.name}
              </div>
              {isStreaming && (
                <div className="text-xs" style={{ color: "#faa61a" }}>
                  Thinking...
                </div>
              )}
            </div>
          </div>
        )
      })}
    </div>
  )
}
```

### 4.8 `components/copilot-actions.tsx` — ОСТАВИТЬ КАК ЕСТЬ

Этот файл содержит интерактивные карточки (PipelineStatus, WorkItems, ResourceInfo, Deployment, Metrics) — это именно то, что нам нужно для visual feedback от агентов. Кнопки "Approve", "Retry", "Rollback" — это наш human-in-the-loop.

**Единственное уточнение**: эти actions работают через CopilotKit. Если мы используем direct AG-UI streaming (channel-area.tsx), то нужно либо:
- (a) Оборачивать channel-area в CopilotKit для tool rendering, либо
- (b) Вынести card-рендеринг из CopilotKit actions в standalone компоненты

**Рекомендация**: Для начала (a) — проще. На фронте уже есть `CopilotKitProvider` wrapper. Channel area может использовать CopilotKit actions параллельно с direct SSE streaming.

### 4.9 `app/api/channel-agui/[channelId]/route.ts` — ОСТАВИТЬ

Этот маршрут уже работает как прокси к бэкенду. Он отправляет AG-UI RunAgentInput на `/api/channels/{channelId}/agui` и стримит SSE обратно на фронт. Оставляем.

### 4.10 `lib/agents.ts` — ОБНОВИТЬ

```typescript
export interface Agent {
  id: string
  name: string
  avatar: string
  color: string
  systemPrompt: string
  model: string
  description?: string
  role?: string // "manager" | "devops" | "developer"
  status?: string
}

export interface Channel {
  id: string
  name: string
  agentIds: string[]
  description?: string
  dateCreated?: string
}

export interface ChatMessage {
  id: string
  role: "user" | "assistant"
  content: string
  agentId?: string
  timestamp: Date
}

// Дефолты больше не нужны — всё приходит с бэкенда через seeder
```

---

## 5. Итого: минимальные frontend файлы для MVP

```
frontend/
├── app/
│   ├── globals.css                (оставить)
│   ├── layout.tsx                 (упростить — убрать auth redirect)
│   ├── page.tsx                   (переписать — auto-login)
│   └── api/
│       ├── auth/
│       │   ├── auto-login/route.ts  ← НОВЫЙ
│       │   ├── me/route.ts          (оставить)
│       │   └── logout/route.ts      (оставить)
│       ├── agents/route.ts          (оставить)
│       ├── channels/route.ts        (оставить)
│       ├── channel-agui/[channelId]/route.ts  (оставить)
│       └── copilotkit/              (оставить для interactive cards)
│
├── components/
│   ├── home-page-client.tsx         (упростить)
│   ├── icon-sidebar.tsx             (упростить)
│   ├── channel-sidebar.tsx          (упростить)
│   ├── channel-area.tsx             (доработать)
│   ├── channel-header.tsx           (оставить)
│   ├── message-list.tsx             (оставить)
│   ├── message-input.tsx            (оставить)
│   ├── member-list.tsx              (упростить)
│   ├── copilot-actions.tsx          (оставить — interactive cards!)
│   ├── copilotkit-provider.tsx      (оставить)
│   ├── agent-profile-popover.tsx    (оставить)
│   ├── start-conversation-empty.tsx (оставить)
│   ├── theme-provider.tsx           (оставить)
│   └── ui/                          (оставить все)
│
├── contexts/
│   └── agent-runtime-context.tsx    (оставить)
│
├── lib/
│   ├── agents.ts                    (упростить)
│   ├── utils.ts                     (оставить)
│   └── server/auth.ts               (оставить)
│
├── hooks/                           (оставить)
├── package.json                     (оставить)
└── ...configs                       (оставить)
```

---

## 6. Human-in-the-Loop (интерактивные действия)

Уже реализовано через `copilot-actions.tsx`:

| Карточка | Actions | HitL? |
|----------|---------|-------|
| PipelineStatus | Retry Stage, View Logs | ✅ "Retry failed stage" |
| WorkItems | Assign, Change Status | ✅ клик = follow-up |
| ResourceInfo | Restart, Scale, View Metrics | ✅ "Restart resource" |
| Deployment | **Approve Deployment**, Rollback, Retry | ✅✅ Approve = key HitL |
| Metrics | Analyze, Compare | ✅ drill-down |

Когда агент вызывает `showDeployment` с environment в статусе `pending`, пользователь видит кнопку **"Approve Deployment"** — это ключевой human-in-the-loop момент.

Агент через `showPipelineStatus` показывает failed stage → пользователь жмёт "Retry" → это отправляет follow-up message → агент вызывает `pipeline_run`.
