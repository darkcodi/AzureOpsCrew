# Claude Code — Итоговый навигатор

> Полный реверс-инжиниринг архитектуры Claude Code  
> Цель: воспроизвести аналогичную архитектуру в AzureOpsCrew

---

## Быстрая навигация по разделам

| # | Раздел | Файл | Что внутри |
|---|---|---|---|
| A | **Карта репозитория** | [01-REPO-MAPPING.md](01-REPO-MAPPING.md) | Структура репо, модули, data flow |
| B | **Ядро оркестрации** | [02-ORCHESTRATION-CORE.md](02-ORCHESTRATION-CORE.md) | Main loop, state machine, субагенты, concurrency |
| C | **Prompt Stack** | [03-PROMPT-STACK.md](03-PROMPT-STACK.md) | 10-layer prompt pipeline, все источники промптов |
| D | **Агенты и команды** | [04-AGENTS-SUBAGENTS-TEAMS.md](04-AGENTS-SUBAGENTS-TEAMS.md) | Формат агентов, протоколы, изоляция, примеры |
| E | **Tools, MCP, Hooks** | [05-TOOLS-MCP-HOOKS.md](05-TOOLS-MCP-HOOKS.md) | 15 built-in tools, MCP config, 15 hook events |
| F | **Память и контекст** | [06-MEMORY-CONTEXT.md](06-MEMORY-CONTEXT.md) | Auto-memory, CLAUDE.md, компакция, resume |
| G | **Безопасность** | [07-SECURITY-GUARDRAILS.md](07-SECURITY-GUARDRAILS.md) | Permissions, sandbox, hooks as guardrails |
| H | **Плагины** | [08-PLUGINS-EXTENSIBILITY.md](08-PLUGINS-EXTENSIBILITY.md) | Plugin system, manifest, marketplace, blueprint |
| I | **Устойчивость** | [09-OPERATIONAL-ROBUSTNESS.md](09-OPERATIONAL-ROBUSTNESS.md) | Memory leaks, recovery, anti-chaos patterns |

---

## Ключевые выводы (Executive Summary)

### 1. Claude Code — НЕ мульти-агентный фреймворк

Это **один агент** с **инструментом Agent** для порождения субагентов. Субагенты:
- Не имеют доступа к истории parent
- Не могут вызывать Agent tool (нет вложенности)
- Не могут общаться друг с другом
- Получают ограниченный набор tools

### 2. Всё — Markdown

- Агенты = `.md` файлы с YAML frontmatter
- Команды = `.md` файлы с frontmatter
- Skills = `SKILL.md` + references/
- Промпты = Markdown body файлов
- Нет JSON schemas, нет классов, нет кода — **только Markdown**

### 3. Стабильность через bounded design

| Принцип | Реализация |
|---|---|
| Bounded context | Compaction при 80% |
| Bounded nesting | Max depth = 1 (нет вложенных субагентов) |
| Bounded tools | Per-agent tool restrictions |
| Bounded buffers | CircularBuffer для всех коллекций |
| Bounded retries | Exponential backoff + max attempts |
| Bounded permissions | Allow/deny/ask per tool |

### 4. Hooks — основной extension point

15 hook events покрывают **весь lifecycle**:
- PreToolUse / PostToolUse — валидация и logging
- Stop / SubagentStop — контроль завершения
- SessionStart / SessionEnd — init и cleanup
- PreCompact — подготовка к сжатию контекста
- UserPromptSubmit — фильтрация user input
- WorktreeCreate / WorktreeRemove — lifecycle worktrees

### 5. MCP — стандартный протокол интеграции

4 типа транспорта (stdio, SSE, HTTP, WebSocket), конфигурация через `.mcp.json`, поддержка в плагинах. MCP tools наследуют permission model.

---

## Что нужно AzureOpsCrew (Top-10 приоритетов)

| # | Компонент | Приоритет | Сложность | Описание |
|---|---|---|---|---|
| 1 | **Agent Loop** | 🔴 Critical | Medium | Main agent с tool-calling loop |
| 2 | **Agent Tool** | 🔴 Critical | Medium | Механизм порождения субагентов |
| 3 | **Compaction** | 🔴 Critical | High | Сжатие контекста с pinned items |
| 4 | **Permission Model** | 🔴 Critical | Low | Allow/deny/ask per tool |
| 5 | **Hook System** | 🟡 High | Medium | PreToolUse/PostToolUse минимум |
| 6 | **CLAUDE.md equiv.** | 🟡 High | Low | Project instructions loading |
| 7 | **Tool Restrictions** | 🟡 High | Low | Per-agent tool whitelisting |
| 8 | **MCP Integration** | 🟡 High | Varies | Стандартный протокол для external tools |
| 9 | **Session Resume** | 🟢 Medium | Medium | Восстановление после crash |
| 10 | **Plugin System** | 🟢 Medium | High | Расширяемость через плагины |

---

## Архитектурная схема Claude Code (полная)

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         CLAUDE CODE RUNTIME                              │
│                                                                          │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────────────────┐     │
│  │ Config Loader │   │ Plugin Loader│   │ MCP Client Manager      │     │
│  │ • settings    │   │ • manifests  │   │ • stdio/SSE/HTTP/WS     │     │
│  │ • managed     │   │ • commands   │   │ • reconnect             │     │
│  │ • project     │   │ • agents     │   │ • tool registration     │     │
│  └──────┬───────┘   │ • skills     │   └──────────┬─────────────┘     │
│         │           │ • hooks      │              │                     │
│         │           └──────┬───────┘              │                     │
│         └──────────────────┼──────────────────────┘                     │
│                            │                                            │
│                   ┌────────▼────────┐                                   │
│                   │  SESSION MANAGER │                                   │
│                   │  • context       │                                   │
│                   │  • memory        │                                   │
│                   │  • resume        │                                   │
│                   └────────┬────────┘                                   │
│                            │                                            │
│  ┌─────────────────────────▼────────────────────────────────────────┐   │
│  │                    MAIN AGENT LOOP                                │   │
│  │                                                                   │   │
│  │  ┌──────────┐   ┌──────────┐   ┌──────────┐   ┌──────────┐     │   │
│  │  │ Prompt   │──▶│ LLM Call │──▶│ Parse    │──▶│ Execute  │     │   │
│  │  │ Assembly │   │ (Claude) │   │ Response │   │ Tools    │     │   │
│  │  └──────────┘   └──────────┘   └──────────┘   └────┬─────┘     │   │
│  │       ▲                                             │           │   │
│  │       │           ┌─────────────────────────────────┘           │   │
│  │       │           │                                             │   │
│  │       │     ┌─────▼─────┐                                      │   │
│  │       │     │ Permission│──deny──▶ Block                       │   │
│  │       │     │ Check     │                                      │   │
│  │       │     └─────┬─────┘                                      │   │
│  │       │           │allow                                       │   │
│  │       │     ┌─────▼─────┐                                      │   │
│  │       │     │ PreToolUse│──block──▶ Block                      │   │
│  │       │     │ Hooks     │                                      │   │
│  │       │     └─────┬─────┘                                      │   │
│  │       │           │pass                                        │   │
│  │       │     ┌─────▼─────┐                                      │   │
│  │       │     │ Tool      │──▶ Built-in / MCP / Agent            │   │
│  │       │     │ Router    │                                      │   │
│  │       │     └─────┬─────┘                                      │   │
│  │       │           │                                             │   │
│  │       │     ┌─────▼─────┐                                      │   │
│  │       │     │PostToolUse│                                      │   │
│  │       │     │ Hooks     │                                      │   │
│  │       │     └─────┬─────┘                                      │   │
│  │       │           │                                             │   │
│  │       └───────────┘ (loop until Stop)                          │   │
│  │                                                                │   │
│  │  On context ~80% ──▶ COMPACTION                                │   │
│  │  On Stop event  ──▶ Stop Hooks ──▶ Done / Continue             │   │
│  └────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────┐     │
│  │                    MEMORY LAYER                                 │     │
│  │  Auto-memory │ CLAUDE.md │ Todo List │ Compacted History       │     │
│  └────────────────────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Что категорически НЕ копировать

| # | Антипаттерн | Почему |
|---|---|---|
| 1 | Закрытый бинарник как core | Нужен open-source runtime |
| 2 | Всё в одном npm-пакете | Нужна модульная архитектура |
| 3 | WASM-зависимость для парсинга | Fragile, leaks |
| 4 | Неограниченный prompt stack | Нужен token budget manager |
| 5 | Settings merge 4 уровней | Достаточно 2-3 |

---

*Анализ выполнен на основе https://github.com/anthropics/claude-code (shallow clone, open artifacts only)*
*Runtime код закрыт — архитектура реконструирована по плагинам, CHANGELOG, examples и документации*
