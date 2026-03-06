# A) Repo Mapping — Claude Code

## Важная оговорка

Исходный код Claude Code (runtime, CLI, orchestration engine) **НЕ является open-source**. Репозиторий `anthropics/claude-code` содержит:

- Плагины (примеры + справочные материалы по архитектуре)
- CHANGELOG с глубокими архитектурными сигналами
- Примеры настроек и хуков
- Документацию по plugin-dev с описанием всех внутренних API

Тем не менее, из plugin-dev документации, CHANGELOG и структуры плагинов мы можем **полностью восстановить архитектуру** системы.

---

## Структура репозитория (верхний уровень)

```
claude-code/
├── .claude/                     # Встроенные команды для самого проекта
│   └── commands/
│       ├── commit-push-pr.md    # Git workflow команда
│       ├── dedupe.md            # Дедупликация issues
│       └── triage-issue.md      # Триаж GitHub issues
├── .claude-plugin/              # Marketplace manifest
│   └── marketplace.json         # Реестр всех плагинов
├── plugins/                     # ★ Главное сокровище - 13 плагинов
├── examples/                    # Примеры настроек и хуков
│   ├── hooks/
│   │   └── bash_command_validator_example.py
│   └── settings/
│       ├── settings-strict.json
│       ├── settings-lax.json
│       └── README.md
├── scripts/                     # GitHub Actions скрипты
├── CHANGELOG.md                 # 2069 строк архитектурных сигналов
├── README.md
└── SECURITY.md
```

## Карта модулей (реконструкция из evidence)

| Модуль (внутренний) | Что делает | Evidence |
|---|---|---|
| **CLI / REPL Engine** | Ink-based терминальный UI, обработка ввода | CHANGELOG: Yoga WASM, React Compiler, Ink, spinner |
| **Orchestration Runtime** | Главный цикл agent: plan→act→observe | CHANGELOG: "forked agents", "autocompact", "summarization" |
| **Tool Router** | Маршрутизация tool_use → execution | Плагины: `matcher` в hooks, tool names (Read/Write/Edit/Bash/Grep/Glob/LS) |
| **Agent/Subagent/Team Engine** | Spawn субагентов, фоновые задачи | CHANGELOG: "teammates", "subagents", "background agents", "worktree isolation" |
| **Memory Store** | Auto-memory, project memory, session persistence | CHANGELOG: "auto-memory", "/memory", "session upload" |
| **Compaction Engine** | Сжатие контекста при переполнении | CHANGELOG: "autocompact", "compaction", "50K tool results to disk" |
| **Plugin Loader** | Discovery плагинов, команд, агентов, навыков | Plugin-dev SKILL.md, marketplace.json |
| **MCP Client** | Подключение к MCP серверам (stdio/SSE/HTTP/ws) | MCP integration SKILL.md |
| **Hooks Engine** | Event-driven pre/post обработка | Hook development SKILL.md |
| **Permissions/Sandbox** | Trust model, sandboxing, approvals | settings-strict.json, CHANGELOG: sandbox references |
| **Config Loader** | Settings hierarchy (managed → user → project) | examples/settings/, CHANGELOG |
| **Session Manager** | Persist/resume/continue разговоры | CHANGELOG: --resume, --continue, transcript files |
| **CLAUDE.md Loader** | Загрузка project-level instructions | CHANGELOG: CLAUDE.md, rules/*.md, InstructionsLoaded hook |

## Ключевые папки плагинов

| Плагин | Содержимое | Архитектурная ценность |
|---|---|---|
| `plugin-dev/` | 7 skills с полной документацией по API | ★★★★★ Главный источник |
| `feature-dev/` | 3 агента + 1 команда (7-phase workflow) | ★★★★ Паттерн оркестрации |
| `code-review/` | Мульти-агентный pipeline с 5 параллельными агентами | ★★★★ Паттерн teams |
| `hookify/` | Python хуки для всех событий | ★★★ Паттерн hooks |
| `pr-review-toolkit/` | 6 специализированных review-агентов | ★★★ Паттерн субагентов |
| `security-guidance/` | PreToolUse security hook | ★★ Паттерн guardrails |
| `ralph-wiggum/` | Iterative loop (Stop hook → continue) | ★★ Паттерн long-running |

## Входы/Выходы модулей

```
User Input → CLI/REPL
  ↓
Config Loader (settings, env vars, .claude/*.md)
  ↓
Plugin Loader (commands, agents, skills, hooks, MCP servers)
  ↓
Orchestration Runtime (main agent loop)
  ├── Tool Router → Built-in Tools (Read/Write/Edit/Bash/Grep/Glob/LS)
  │                → MCP Tools (external servers)
  │                → Agent Tool (spawn subagents)
  ├── Hooks Engine → PreToolUse/PostToolUse/Stop/SessionStart/...
  ├── Memory Store → Auto-memory, project memory
  ├── Compaction Engine → Context summarization when window fills
  └── Permissions Engine → Allow/Deny/Ask decisions
  ↓
API Call → Claude Model (system + developer + user messages)
  ↓
Response → Parse tool_use blocks → Execute → Loop
  ↓
Final Response → Terminal UI
```
