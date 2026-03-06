# I) Операционная устойчивость и Anti-Chaos паттерны — Claude Code

## Почему Claude Code стабильно работает?

Анализ CHANGELOG (2069 строк, 384 архитектурных сигнала) и документации плагинов выявил систематическую работу над устойчивостью. Ниже — все обнаруженные паттерны и механизмы.

## 1. Memory Leak Prevention

### Проблемы (из CHANGELOG — 20+ fixes)

| Версия | Утечка | Решение |
|---|---|---|
| v2.1.11 | Tool result accumulation | Compaction при threshold |
| v2.1.15 | MCP connection leak | Graceful disconnect + reconnect |
| v2.1.20 | Context accumulation | Auto-compact fix |
| v2.1.27 | Notification buffer | CircularBuffer implementation |
| v2.1.33 | Session state growth | GC для completed tasks |
| v2.1.39 | Compacted data on disk | Disk offloading |
| v2.1.45 | Stream buffer leaks | Stream cleanup on tool end |
| v2.1.49 | Worktree orphans | WorktreeRemove cleanup hooks |
| v2.1.52 | MCP server processes | Process cleanup on exit |
| v2.1.56 | Todo state bloat | Checklist preservation optimization |
| v2.1.62 | Plugin reload leaks | Complete unload before reload |
| v2.1.66 | Infinite compaction | Loop detection break |

### Паттерны решения

```
1. CircularBuffer для всех растущих коллекций
   → Фиксированный размер, FIFO вытеснение

2. GC для завершённых задач
   → Completed tasks → summary → удаление деталей

3. Compaction to disk
   → Старый контекст → disk, не в памяти

4. Process cleanup
   → MCP servers, worktrees → cleanup при exit/SIGTERM

5. Reconnection без накопления
   → Новое соединение заменяет старое, не добавляется
```

## 2. Компакция как anti-chaos

### Проблема: Context Window Exhaustion

Без компакции любой AI-агент "забывает" начало разговора при длинных сессиях.

### Решение в Claude Code

```
CONTEXT LIFECYCLE:
                                                              
Messages accumulate ───────▶ 80% threshold ───▶ Compaction
                                                     │
                                              ┌──────┴──────┐
                                              │ PRESERVED:   │
                                              │ • System     │
                                              │ • CLAUDE.md  │
                                              │ • Memory     │
                                              │ • Todo list  │
                                              │ • Last N     │
                                              ├─────────────┤
                                              │ COMPRESSED:  │
                                              │ • Old msgs → │
                                              │   Summary    │
                                              └─────────────┘
```

### Anti-chaos гарантии компакции

1. **Todo list pinned** — задачи никогда не теряются (v2.1.56+)
2. **Auto-memory persistent** — learnings выживают компакцию (v2.1.59+)
3. **No infinite loops** — loop detection breaks (v2.1.66+)
4. **PreCompact hook** — можно добавить кастомную логику перед сжатием
5. **Disk offloading** — compacted data на диске, не в RAM

## 3. MCP Reconnection & Recovery

### Проблемы MCP

| Проблема | Описание | Решение |
|---|---|---|
| Server crash | MCP процесс падает | Auto-reconnect |
| Timeout | Сервер не отвечает | Configurable timeout + fallback |
| Protocol error | Невалидный JSON/response | Error isolation, не crash agent |
| Connection leak | Соединения не закрываются | Cleanup on disconnect |
| Startup failure | Сервер не стартует | Retry + user notification |

### Reconnection Strategy

```
MCP Server Connection:

1. Connect attempt
   └─ Success → Use
   └─ Failure → Retry (3 attempts, exponential backoff)
       └─ All failed → Mark as unavailable
           └─ Periodic health check → Reconnect when available

During session:
   └─ Connection lost → Immediate reconnect attempt
       └─ Success → Resume
       └─ Failure → Mark unavailable, notify Claude
           └─ Claude can continue without this MCP server

On session end:
   └─ Graceful disconnect all MCP servers
   └─ Kill child processes
```

## 4. Tool Execution Safety

### Timeout Management

```
Tool execution:
├── Bash → configurable timeout (default ~120s)
├── Read → fast, no timeout needed
├── Write → fast, no timeout needed
├── Agent → inherits parent timeout context
├── MCP tools → server-specific timeout
└── WebFetch → HTTP timeout

On timeout:
├── Kill the process
├── Return timeout error to Claude
├── Claude can retry or take alternative action
└── NO zombie processes (PID tracking + SIGKILL fallback)
```

### Error Handling

```
Tool error handling:
├── Soft errors → Return error message to Claude
│   └── Claude decides: retry, skip, or alternative
├── Hard errors → Abort tool, return failure
│   └── Claude continues with other tools
└── Critical errors → Session recovery
    └── --resume to continue from last state
```

## 5. Session Recovery (--resume)

### Что делает resume

```
claude --resume <session-id>

1. Load session state from disk
   ├── Conversation history (or compacted summary)
   ├── Todo list
   ├── Auto-memory
   └── Working directory

2. Reconnect
   ├── MCP servers → restart + reconnect
   └── Validate environment

3. Resume from last message
   └── Claude sees full (compacted) context
   └── Can continue where it left off
```

### Что НЕ восстанавливается

- Background processes (terminals, watchers)
- Worktree agents (need recreation)
- Temporary state (clipboard, etc.)
- Running MCP server state (restart from scratch)

## 6. Parallel Execution Safety

### Проблема: Race Conditions

Несколько агентов работают параллельно → могут конфликтовать на файлах.

### Решение Claude Code

```
ISOLATION STRATEGIES:

1. Worktree isolation (strongest)
   └── Каждый агент в своём git worktree
   └── Полная изоляция файловой системы
   └── Merge через git

2. Read-only agents (safest)
   └── tools: [Read, Grep, Glob, LS]
   └── Не могут писать → нет конфликтов
   └── Идеально для code review

3. Sequential coordination (simple)
   └── Main agent ждёт результата каждого субагента
   └── Один пишет → результат → другой пишет
   └── Нет параллелизма при записи

4. Domain partitioning (practical)
   └── Каждый агент работает с определёнными файлами
   └── Нет перекрытия по файлам
   └── Main agent координирует distribution
```

### Пример из Code Review Plugin

```
9 агентов параллельно, НО:
  • Все read-only (Read, Grep, Glob, LS)
  • Каждый анализирует свой аспект
  • Никто не модифицирует файлы
  • Main agent собирает результаты
  → БЕЗОПАСНАЯ параллелизация
```

## 7. Tree-Sitter Integration Stability

### Проблемы (из CHANGELOG)

| Версия | Проблема | Фикс |
|---|---|---|
| v2.1.15 | WASM module crash | Reset on error |
| v2.1.27 | Parser memory leak | Lazy loading |
| v2.1.39 | Language detection failure | Fallback to text |
| v2.1.52 | Large file parsing OOM | Size limits |

### Стратегия устойчивости

```
Tree-sitter usage:
├── Try to parse with language grammar
│   └── Success → Syntax-aware operations
│   └── Failure → Fallback to plain text
├── WASM reset on error
│   └── Don't crash → Reset module → Retry once
└── Size limits
    └── Large files → Skip parsing → Plain text mode
```

## 8. Output & Streaming Stability

### Stream-JSON Protection

```
Claude response → Streaming:
├── Partial JSON handling
│   └── Buffer until complete JSON
│   └── No partial parse failures
├── Interrupted stream
│   └── Retry from last complete message
│   └── Don't lose partial progress
└── Large output
    └── Truncation with notification
    └── "Output truncated (showing last N lines)"
```

## 9. Таблица anti-chaos паттернов

| # | Паттерн | Проблема | Реализация | Версия появления |
|---|---|---|---|---|
| 1 | CircularBuffer | Unbounded growth | Fixed-size FIFO | Early |
| 2 | Auto-compaction | Context overflow | Summary + pin + recent | v2.1.11 |
| 3 | GC completed tasks | State bloat | Remove details, keep summary | v2.1.33 |
| 4 | MCP reconnect | Connection loss | Auto-reconnect + backoff | v2.1.15 |
| 5 | Process cleanup | Zombie processes | SIGTERM + SIGKILL chain | Early |
| 6 | Read-only agents | Race conditions | Restricted tools | Design |
| 7 | Worktree isolation | FS conflicts | Git worktrees | v2.1.49 |
| 8 | Loop detection | Infinite compaction | Counter + break | v2.1.66 |
| 9 | Tree-sitter fallback | Parser crash | Reset + fallback to text | v2.1.15 |
| 10 | Session resume | Crash recovery | Disk persistence | Early |
| 11 | Timeout enforcement | Hung processes | Kill + error return | Design |
| 12 | Disk offloading | Memory pressure | Compacted data → disk | v2.1.39 |
| 13 | Symlink resolution | Security bypass | Real path validation | v2.1.27 |
| 14 | Hook parallelism | Slow hooks | Parallel exec + timeout | v2.1.49 |
| 15 | Plugin reload | Leak on re-register | Full unload → load | v2.1.62 |

## 10. Рекомендации для AzureOpsCrew

### КРИТИЧЕСКИ важно реализовать:

1. **Context compaction** — без этого агент "забывает" при длинных сессиях
2. **Todo list pinning** — задачи выживают компакцию
3. **Process cleanup** — MCP, субагенты, терминалы — всё чистить при exit
4. **Read-only agents по умолчанию** — safe parallelism
5. **Timeout enforcement** — всё с timeout, kill при превышении

### Желательно:

6. **MCP reconnection** — не теряем внешние сервисы при временных сбоях
7. **Session persistence** — resume после crashes
8. **Worktree isolation** — для code generation tasks
9. **CircularBuffer** — для любых растущих коллекций
10. **Loop detection** — всегда проверять на бесконечные циклы

### Архитектурный принцип:

> **Всё должно быть bounded**: bounded buffers, bounded retries, bounded timeouts, bounded agent nesting (max depth = 1), bounded context window. Unbounded = chaos.
