# F) Память, Контекст и Компакция — Claude Code

## Модель управления контекстом

```
┌──────────────────────────────────────────────────────────────────┐
│                    CONTEXT MANAGEMENT MODEL                       │
│                                                                  │
│  ┌──────────────────────┐                                        │
│  │   Session Context    │  200K / 1M tokens (configurable)       │
│  │   ┌──────────────┐   │                                        │
│  │   │ System Prompt │   │  ~3-5K tokens                         │
│  │   ├──────────────┤   │                                        │
│  │   │ CLAUDE.md     │   │  variable                             │
│  │   ├──────────────┤   │                                        │
│  │   │ Memory        │   │  variable, grows                      │
│  │   ├──────────────┤   │                                        │
│  │   │ Conversation  │   │  BULK — сообщения + tool results      │
│  │   ├──────────────┤   │                                        │
│  │   │ Pinned Items  │   │  todo list, active task context       │
│  │   └──────────────┘   │                                        │
│  └──────────────────────┘                                        │
│                                                                  │
│  При ~80% заполнения ──▶ COMPACTION                              │
│                                                                  │
│  ┌──────────────────────┐                                        │
│  │  Compacted Context   │                                        │
│  │  ┌──────────────┐   │                                        │
│  │  │ System Prompt │   │  ← preserved                           │
│  │  ├──────────────┤   │                                        │
│  │  │ CLAUDE.md     │   │  ← preserved                           │
│  │  ├──────────────┤   │                                        │
│  │  │ Memory        │   │  ← preserved                           │
│  │  ├──────────────┤   │                                        │
│  │  │ Summary       │   │  ← AI-generated summary                │
│  │  ├──────────────┤   │                                        │
│  │  │ Pinned Items  │   │  ← preserved                           │
│  │  ├──────────────┤   │                                        │
│  │  │ Last N turns  │   │  ← recent context preserved            │
│  │  └──────────────┘   │                                        │
│  └──────────────────────┘                                        │
└──────────────────────────────────────────────────────────────────┘
```

## 1M Context Window

- **Default**: 200K tokens
- **Extended**: 1M tokens (для соответствующих моделей)
- **Отключение**: `CLAUDE_CODE_DISABLE_1M_CONTEXT=true`
- **Влияние**: Меньше компакций → лучше continuity, но дороже

## Типы памяти

### 1. Session Memory (In-Context)
- **Что**: Вся переписка текущей сессии
- **Где**: В контексте LLM
- **Lifetime**: До конца сессии или до компакции
- **Persistence**: Нет (теряется при закрытии)

### 2. Auto-Memory (Persistent, v2.1.59+)
- **Что**: Автоматически сохранённые "learnings" от Claude
- **Где**: Session store / disk
- **Lifetime**: Между сессиями
- **Управление**: Команда `/memory`
- **Формат**: Key-value pairs с описанием
- **Пример**:
  ```
  [learning] This project uses pnpm, not npm
  [learning] Tests must pass before commit (pre-commit hook)
  [learning] API endpoints follow /api/v2/{resource} convention
  ```

### 3. Project Memory (CLAUDE.md)
- **Что**: Ручные инструкции от разработчика
- **Где**: `/project/CLAUDE.md`, `.claude/rules/*.md`
- **Lifetime**: Постоянно (в repo)
- **Формат**: Markdown

### 4. Todo List State
- **Что**: Текущий список задач
- **Где**: В контексте (pinned)
- **Lifetime**: До конца сессии
- **Persistence**: Сохраняется при компакции
- **Tool**: `TodoWrite`

### 5. Session Persistence (--resume)
- **Что**: Полное восстановление сессии
- **Где**: На диске
- **Lifetime**: Между запусками
- **Команда**: `claude --resume <session-id>`
- **Что сохраняется**: История, tool state, todo list

## CLAUDE.md: Система проектных инструкций

### Иерархия загрузки

```
1. ~/.claude/CLAUDE.md                   ← Глобальные (user-level)
2. /project/CLAUDE.md                    ← Project root
3. /project/subdir/CLAUDE.md             ← Subdirectory (conditional)
4. /project/.claude/rules/*.md           ← Rules files
5. @-imported files                       ← Из любого CLAUDE.md
```

### Импорт файлов
```markdown
# CLAUDE.md
Используй TypeScript strict mode.

@docs/api-conventions.md
@.claude/rules/testing.md
```
- **Синтаксис**: `@path/to/file.md`
- **Рекурсивный**: Imported файл может импортировать другие
- **Пути**: Относительно CLAUDE.md файла

### Условные Rules
```yaml
---
paths:
  - "src/**/*.ts"
  - "lib/**/*.ts"
---
Always use strict TypeScript.
No `any` type allowed.
```
- **Frontmatter**: `paths:` с glob-паттернами
- **Применение**: Только когда Claude работает с файлами, matching паттерну
- **Расположение**: `.claude/rules/*.md`

### Worktree Sharing
- CLAUDE.md **общий** для всех git worktrees одного репо
- Не дублируется при создании worktree
- Субагенты с worktree isolation тоже видят main CLAUDE.md

## Компакция (Compaction)

### Когда происходит

1. **Auto-compact**: При достижении ~80% context window
2. **Manual**: Команда `/compact` (или shift+cmd+c)
3. **Pre-compact hook**: `PreCompact` event для подготовки
4. **Large conversations**: Автоматически при длинных сессиях

### Что сохраняется при компакции

| Компонент | Сохраняется? | Как |
|---|---|---|
| System prompt | ✅ Полностью | Не трогается |
| CLAUDE.md | ✅ Полностью | Не трогается |
| Memory (auto) | ✅ Полностью | Не трогается |
| Todo list | ✅ Полностью | Pinned |
| Conversation | ⚠️ Summary | AI-резюме |
| Tool results | ⚠️ Summary | Сжатие |
| Last N turns | ✅ Полностью | Preserved |
| File contents read | ❌ Удаляются | В summary |

### Алгоритм компакции (реконструкция)

```
1. Определить threshold (80% context window)
2. Разделить контекст:
   a. PINNED: system prompt, CLAUDE.md, memory, todo → не трогать
   b. RECENT: последние N сообщений → сохранить
   c. OLD: всё остальное → сжать
3. Для OLD контекста:
   a. Вызвать LLM (или heuristic) → создать summary
   b. Summary включает: key decisions, file changes, test results
4. Заменить OLD → Summary
5. Записать на диск ("compacted to disk" в logs, v2.1.39+)
6. Теперь в контексте: PINNED + Summary + RECENT
```

### Оптимизации компакции (из CHANGELOG)

| Версия | Улучшение |
|---|---|
| v2.1.11 | Компакция при невозможности отправить → auto-compact |
| v2.1.20 | Фикс потери context при autocompact |
| v2.1.27 | Предупреждение при приближении к context limit |
| v2.1.39 | Компакция на диск (compacted to disk) |
| v2.1.49 | PreCompact hook для подготовки |
| v2.1.56 | Сохранение чеклиста при компакции |
| v2.1.59 | Auto-memory: learnings persist across compactions |
| v2.1.62 | Фикс разрушительной компакции через shift+cmd+c |
| v2.1.66 | Фикс бесконечного цикла компакции |

### Проблемы компакции (из CHANGELOG)

1. **Infinite compaction loop** (v2.1.66 fix) — компакция триггерила себя
2. **Lost context** (v2.1.20 fix) — summary не включал критическую инфу
3. **Destructive compaction** (v2.1.62) — shift+cmd+c ломал state
4. **Checklist loss** (v2.1.56) — todo list терялся при компакции

## Context Budget Management

### Token Accounting

```
Total budget: 200,000 tokens (or 1,000,000)
─────────────────────────────────────────
System prompt:     ~3,000-5,000
CLAUDE.md:         ~500-5,000
Memory:            ~200-2,000
Skills (loaded):   ~1,000-10,000 per skill
Tool schemas:      ~200 per tool × N tools
─────────────────────────────────────────
Available for conversation: ~170,000-190,000
```

### Стратегии экономии

1. **Lazy skill loading** — Skills загружаются только при match
2. **Tool filtering** — Субагенты получают subset tools
3. **Summary, не raw** — Tool results суммируются
4. **Line ranges** — Read tool читает только нужные строки
5. **Compaction** — Автоматическое сжатие старой истории
6. **1M window** — Расширенное окно для больших проектов
7. **Disable features** — CLAUDE_CODE_SIMPLE, CLAUDE_CODE_DISABLE_1M_CONTEXT

## Восстановление сессий

### --resume
```bash
claude --resume                    # Последняя сессия
claude --resume <session-id>       # Конкретная сессия
claude --resume --latest           # Самая свежая
```

### Что восстанавливается
- Полная история (или compacted summary)
- Todo list state
- Memory (auto)
- Working directory
- MCP connections (переподключение)

### Что НЕ восстанавливается
- Background процессы (терминалы)
- Worktree субагенты
- Открытые файлы в editor
- Running MCP server processes (перезапуск)
