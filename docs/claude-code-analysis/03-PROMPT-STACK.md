# C) Prompt Stack & System Prompts — Claude Code

## Prompt Assembly Pipeline

```
┌──────────────────────────────────────────────────────────────────┐
│                    PROMPT ASSEMBLY PIPELINE                       │
│                                                                  │
│  Layer 1: Base System Prompt (закрытый, внутри бинарника)        │
│     ↓                                                            │
│  Layer 2: Git Instructions (опционально, --includeGitInstructions)│
│     ↓                                                            │
│  Layer 3: CLAUDE.md (project root + nested + imported files)     │
│     ↓                                                            │
│  Layer 4: .claude/rules/*.md (conditional rules with paths:)     │
│     ↓                                                            │
│  Layer 5: Auto-Memory (/memory, автоматические learnings)        │
│     ↓                                                            │
│  Layer 6: Skills (авто-загружаемые контекстные знания)           │
│     ↓                                                            │
│  Layer 7: Plugin Prompts (SessionStart hooks → system messages)  │
│     ↓                                                            │
│  Layer 8: Tool Schemas (built-in + MCP tools)                    │
│     ↓                                                            │
│  Layer 9: Runtime State (todo list, active tasks, agent context)  │
│     ↓                                                            │
│  Layer 10: --append-system-prompt / --system-prompt-file          │
│     ↓                                                            │
│  ═══════════════════════════════════════                          │
│  FINAL: system messages + developer messages + user messages      │
│  → отправляется в Claude API                                    │
└──────────────────────────────────────────────────────────────────┘
```

## Таблица всех промптов/инструкций

| # | Название | Расположение | Назначение | Когда применяется | Токен-импакт |
|---|---|---|---|---|---|
| 1 | **Base System Prompt** | Внутри бинарника (закрытый) | Основные инструкции Claude Code: role, capabilities, tool descriptions | При каждом запросе | ~3-5K токенов (est.) |
| 2 | **Git Instructions** | Внутри бинарника | Инструкции по commit, PR workflows | При запросе (отключается `CLAUDE_CODE_DISABLE_GIT_INSTRUCTIONS`) | ~500 токенов (est.) |
| 3 | **CLAUDE.md** | `/project/CLAUDE.md` + вложенные | Project-specific правила, конвенции, стиль | При старте сессии + InstructionsLoaded hook | Variable, 500-5000 |
| 4 | **Imported CLAUDE.md** | `@path/to/file.md` в CLAUDE.md | Дополнительные файлы инструкций | При загрузке CLAUDE.md | Variable |
| 5 | **Rules files** | `.claude/rules/*.md` | Условные правила с `paths:` frontmatter | При старте + при работе с matching files | Variable |
| 6 | **Auto-Memory** | Внутри session store | Автоматически сохранённые "learnings" | При старте сессии | Variable, растёт |
| 7 | **Project Memory** | `/memory` управление | Пользовательские заметки | При старте сессии | Variable |
| 8 | **Skills (SKILL.md)** | `skills/skill-name/SKILL.md` | Авто-загружаемые знания по теме | Триггерятся по описанию | 1-10K per skill |
| 9 | **Agent System Prompt** | `agents/agent-name.md` (body after frontmatter) | Инструкции для субагента | При вызове Agent tool | 500-3000 per agent |
| 10 | **Command Prompt** | `commands/command-name.md` (body) | Инструкция при вызове slash-команды | При вызове /command | Variable |
| 11 | **SessionStart Hook Output** | Hooks → stdout / systemMessage | Динамический контекст при старте | SessionStart event | Variable |
| 12 | **Tool Permissions Policy** | settings.json / managed settings | Allow/deny/ask правила для tools | При каждом tool call | Minimal |
| 13 | **--append-system-prompt** | CLI argument / env var | Дополнительный промпт (API users) | При каждом запросе | Variable |
| 14 | **--system-prompt-file** | CLI argument | Замена или дополнение system prompt | При каждом запросе | Variable |

## Детали каждого слоя

### Layer 1: Base System Prompt
- **Где**: Внутри бинарника `@anthropic-ai/claude-code` npm пакет
- **Формат**: Неизвестен (закрытый код)
- **Содержимое** (реконструкция по поведению):
  - Role: "You are Claude Code, an AI coding assistant..."
  - Tool descriptions и usage instructions
  - Safety guidelines
  - Output formatting rules
  - Git workflow instructions (отключаемые)
- **Когда обновляется**: При обновлении версии Claude Code

### Layer 2: CLAUDE.md
- **Где**: Корень проекта `/CLAUDE.md`
- **Формат**: Markdown
- **Поддержка вложенности**: 
  - Вложенные `CLAUDE.md` в поддиректориях (условные, по `paths:`)
  - Импорт через `@path/to/file.md`
- **Загрузка**: 
  - При старте сессии
  - Не в print mode (`claude -p`) — fixed в 2.1.47
  - Общая для git worktrees одного репо
- **Hook**: `InstructionsLoaded` срабатывает после загрузки
- **Token counting**: Есть подсчёт (CLAUDE_CODE_SIMPLE отключает)

### Layer 3: Rules (.claude/rules/*.md)
```yaml
---
paths:
  - "src/**/*.ts"    # Применяется только для TypeScript файлов
---
Используй strict TypeScript, без any.
```
- **Условные**: Применяются только когда работаем с matching files
- **Frontmatter**: `paths:` для glob-паттернов

### Layer 4: Auto-Memory
- **Управление**: Команда `/memory`
- **Автосохранение**: Claude автоматически сохраняет полезный контекст (v2.1.59+)
- **Хранение**: Session store (shared across worktrees)
- **Формат**: Key-value learnings

### Layer 5: Skills (SKILL.md)
```yaml
---
name: Hook Development
description: This skill should be used when the user asks to "create a hook"...
version: 0.1.0
---
# Content of the skill
```
- **Авто-discovery**: Загружаются из plugins/ и project skills/
- **Триггер**: По описанию + user intent matching
- **Формат**: YAML frontmatter + Markdown body
- **Переменная**: `${CLAUDE_SKILL_DIR}` для ссылок на файлы внутри навыка

### Layer 6: Agent System Prompt
```yaml
---
name: code-reviewer
description: Use this agent when...
model: inherit
color: blue
tools: ["Read", "Grep", "Glob"]
---
You are an expert code quality reviewer...
```
- **Файлы**: `agents/*.md` в плагинах или проекте
- **Frontmatter fields**:
  - `name` — идентификатор
  - `description` — когда использовать (с `<example>` блоками для triggering)
  - `model` — inherit/sonnet/opus/haiku
  - `color` — цвет в UI
  - `tools` — ограничение доступных инструментов
  - `background` — фоновое выполнение (v2.1.49+)
  - `isolation` — worktree изоляция (v2.1.49+)

### Layer 7: Command Prompt
```yaml
---
description: Review code for security issues  
allowed-tools: Read, Grep, Bash(git:*)
model: sonnet
argument-hint: [pr-number]
---
Review this code for security vulnerabilities...
```
- **Файлы**: `commands/*.md`
- **Динамические аргументы**: `$ARGUMENTS`, `$1`, `$2`, `$3`
- **File references**: `@$1` → включает содержимое файла
- **Bash execution**: `!`git status`` → выполняет bash inline
- **Namespacing**: Поддиректории → namespace (e.g., `ci/build.md` → `/build (project:ci)`)

## Prompt для создания агентов (из plugin-dev docs)

Файл: `plugins/plugin-dev/skills/agent-development/references/agent-creation-system-prompt.md`

Описывает точный формат, который Claude Code использует внутри для создания agent definitions. Ключевые правила:

1. У каждого агента **2-4 triggering examples** в `<example>` блоках
2. Examples содержат `Context:`, `user:`, `assistant:`, `<commentary>`
3. System prompt следует шаблону: Role → Responsibilities → Process → Quality Standards → Output Format → Edge Cases
4. Агенты **должны быть автономными** — не требовать дополнительных вопросов

## Как промпты влияют на контекстный бюджет

| Компонент | Оценка токенов | Persistence |
|---|---|---|
| Base system | ~3-5K | Всегда |
| CLAUDE.md | 500-5K | Всегда |
| Active skills | 1-10K each | По необходимости |
| Agent prompt (subagent) | 500-3K | Только для субагента |
| Tool schemas | ~200 per tool | Всегда |
| Memory | Growing | Всегда |
| **Total overhead** | **~10-30K** | — |

При 200K context window это ~5-15% бюджета. При 1M context — ~1-3%.
