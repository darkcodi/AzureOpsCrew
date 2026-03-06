# B) Orchestration Core — Claude Code

## Архитектурный паттерн

Claude Code — это **единый агент с tool-calling**, который может **спавнить субагентов** (teammates) через специальный инструмент `Agent`. Это НЕ framework с роутером между агентами, а именно один "мозг" с возможностью делегирования.

### Главный цикл (Event Loop)

```
┌─────────────────────────────────────────────────────────────┐
│                    MAIN AGENT LOOP                          │
│                                                             │
│  1. Получить user input (CLI prompt / SDK message)          │
│  2. Собрать system prompt (base + project + memory + tools) │
│  3. Отправить в Claude API (messages + tools schema)        │
│  4. Получить response:                                      │
│     ├── text → рендерить в UI                               │
│     ├── tool_use → executeс Tool Router                     │
│     │    ├── PreToolUse hooks (parallel)                     │
│     │    ├── Permission check (allow/deny/ask)              │
│     │    ├── Execute tool                                   │
│     │    ├── PostToolUse hooks (parallel)                    │
│     │    └── Return tool_result                             │
│     └── end_turn → check Stop hooks → finish or continue    │
│  5. Если tool_result → добавить в messages → goto 3         │
│  6. Если контекст > threshold → auto-compact                │
│  7. Если end_turn + Stop hooks approve → завершить          │
└─────────────────────────────────────────────────────────────┘
```

### State machine задачи/агента

Реконструкция из CHANGELOG и plugin-dev документации:

```
                    ┌──────────┐
                    │  Created │
                    └────┬─────┘
                         │ user message received
                    ┌────▼─────┐
                    │ Planning │ (optional: "think mode")
                    └────┬─────┘
                         │ plan ready
                    ┌────▼─────┐
              ┌────►│ Running  │◄────────────────────┐
              │     └────┬─────┘                      │
              │          │ model returns tool_use     │
              │     ┌────▼─────┐                      │
              │     │ ToolUse  │                      │
              │     │ (hooks)  │                      │
              │     └────┬─────┘                      │
              │          │ tool executed              │
              │     ┌────▼──────┐                     │
              │     │ToolResult │─────────────────────┘
              │     └───────────┘   add result, continue
              │
              │  model returns end_turn
              │     ┌────▼──────┐
              │     │   Stop    │
              │     │  (hooks)  │
              │     └────┬──┬───┘
              │          │  │
              │  approve │  │ block (hook says "continue")
              │     ┌────▼┐ │
              │     │Done │ └──────────────────────────┘
              │     └─────┘
              │
              │  error / timeout
              │     ┌────▼──┐
              └─────│Failed │
                    └───────┘
```

### Реальные стейты из кода (evidence из CHANGELOG)

- **TaskCompleted** — hook event, задача завершена
- **TeammateIdle** — hook event, субагент простаивает
- **SubagentStop** — hook event, субагент пытается остановиться
- **Stop** — hook event, главный агент пытается остановиться
- **SessionStart** / **SessionEnd** — lifecycle сессии

## Субагенты (Subagents / Teammates)

### Как запускаются

Главный агент вызывает инструмент `Agent` с параметрами:
- `name` — имя агента (из agent definitions)
- `prompt` — задание для субагента
- Субагент получает **собственный контекст** (не полный контекст родителя)

### Evidence из CHANGELOG

```
- "Fixed teammates accidentally spawning nested teammates via the Agent tool's name parameter"
- "Fixed memory retention in in-process teammates where the parent's full conversation history was pinned"
- "Fixed memory leak in agent teams where completed teammate tasks were never garbage collected"
- "Fixed API 400 errors in forked agents (autocompact, summarization)"
- "Subagents support isolation: 'worktree' for working in a temporary git worktree"
- "Agent definitions support background: true to always run as background task"
- "Added Ctrl+F keybinding to kill background agents"
- "Reduced token usage on multi-agent tasks with more concise subagent final reports"
```

### Типы агентов

| Тип | Описание | Изоляция |
|---|---|---|
| **In-process subagent** | Запуск в том же процессе, свой контекст | Shared working directory |
| **Background agent** | `background: true`, работает параллельно | Shared или worktree |
| **Worktree agent** | `isolation: worktree`, git worktree | Полная файловая изоляция |
| **Custom agent** | `--agent` CLI flag | Собственный system prompt |

### Sequence Diagram: Мульти-агентный флоу

```
Main Agent                  Subagent 1              Subagent 2
    │                           │                       │
    │  Agent tool(prompt="...")  │                       │
    ├──────────────────────────►│                       │
    │                           │  Read, Grep, Glob     │
    │                           ├──────► tools          │
    │                           │◄──────               │
    │                           │                       │
    │  Agent tool(prompt="...")  │                       │
    ├───────────────────────────┼──────────────────────►│
    │                           │                       │ Read, Bash
    │                           │                       ├──► tools
    │                           │                       │◄──
    │                           │                       │
    │  summary report           │                       │
    │◄──────────────────────────┤                       │
    │                           │                       │
    │  summary report           │                       │
    │◄──────────────────────────┼──────────────────────┤
    │                           │                       │
    │  Synthesize + continue    │                       │
    │                           │                       │
```

### Конкурентность

- Субагенты **могут запускаться параллельно** (evidence: "Launch 2-3 agents in parallel" в feature-dev command)
- Фоновые агенты работают **асинхронно**, можно убить через Ctrl+F
- **Ограничения**: нет вложенных teammates (fixed: "teammates spawning nested teammates")
- **Cancellation**: ESC → interrupt main, Ctrl+F → kill all background
- **Timeouts**: tool-level timeouts (hooks: `timeout` field)

### Data Flow: messages + tool results + memory

```
┌─────────────────────────────────────────────┐
│              Main Agent Context              │
│                                              │
│  System Prompt (base + CLAUDE.md + memory)  │
│  ↓                                          │
│  User Messages                              │
│  ↓                                          │
│  Assistant Messages (with tool_use)          │
│  ↓                                          │
│  Tool Results (может быть > 50K → disk)      │
│  ↓                                          │
│  Subagent Summaries (compressed)             │
│  ↓                                          │
│  [Auto-compact если context > threshold]     │
│  → Summary + pinned facts + last turns      │
└─────────────────────────────────────────────┘
```

## Пример из реального плагина: code-review

Лучший пример мультиагентной оркестрации — `plugins/code-review/commands/code-review.md`:

1. Запуск **haiku agent** → проверка: PR закрыт? Draft? Уже ревьювили?
2. Запуск **haiku agent** → найти все CLAUDE.md файлы
3. Запуск **sonnet agent** → саммари PR
4. Запуск **4 агентов параллельно**:
   - 2× Sonnet → CLAUDE.md compliance
   - 1× Opus → Bug scan (diff only)
   - 1× Opus → Security/logic issues
5. **Validation**: для каждого issue → параллельный субагент проверяет, реальная ли проблема
6. Фильтрация false positives
7. Публикация inline comments через MCP GitHub tool

Это **9+ агентов** в одном workflow, с параллелизацией и validation pipeline.
