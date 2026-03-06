# D) Агенты, Субагенты и Команды — Claude Code

## Архитектурная модель

```
┌─────────────────────────────────────────────────────────────┐
│                    MAIN AGENT (Claude Code)                  │
│                                                              │
│  ┌─────────────────┐   ┌─────────────────┐                  │
│  │   User Request   │──▶│  Agent Decision  │                 │
│  └─────────────────┘   └────────┬────────┘                  │
│                                 │                            │
│                    ┌────────────┼────────────┐               │
│                    ▼            ▼            ▼               │
│              ┌──────────┐ ┌──────────┐ ┌──────────┐         │
│              │ Субагент │ │ Субагент │ │ Субагент │         │
│              │ in-proc  │ │ worktree │ │background│         │
│              │          │ │          │ │          │         │
│              │ ReadOnly │ │ Full FS  │ │ Detached │         │
│              │ tools    │ │ isolation│ │ async    │         │
│              └──────────┘ └──────────┘ └──────────┘         │
│                    │            │            │               │
│                    └────────────┼────────────┘               │
│                                 ▼                            │
│                          Summary back                        │
│                          to Main Agent                       │
└─────────────────────────────────────────────────────────────┘
```

## Ключевой принцип: Single Agent + Tool

> Claude Code — это **НЕ** мульти-агентный фреймворк.
> Это **один агент** с инструментом `Agent`, который порождает субагентов.

### Почему важно:
1. **Один контекст** — главный агент владеет всей историей
2. **Субагенты изолированы** — получают только задачу, не видят историю
3. **Нет P2P коммуникации** — субагенты не общаются между собой
4. **Нет вложенности** — субагент не может вызвать Agent tool

## Типы агентов

### 1. In-Process субагенты (default)
```
Main Agent ──Agent tool──▶ Субагент ──result──▶ Main Agent
```
- **Execution**: В том же процессе
- **Контекст**: Получает только задачу (не историю parent)
- **Tools**: Ограничены `tools:` массивом из frontmatter
- **FS доступ**: Тот же рабочий каталог
- **Блокирующий**: Main agent ждёт результата

### 2. Background субагенты (v2.1.49+)
```yaml
---
name: background-watcher
background: true
---
```
- **Execution**: Отдельный thread/process
- **Не блокирует**: Main agent продолжает работу
- **TeammateIdle hook**: Срабатывает когда фоновый агент завершил
- **Use case**: Мониторинг, длинные задачи, параллельная работа

### 3. Worktree-isolated субагенты (v2.1.49+)
```yaml
---
name: feature-builder
isolation: worktree
---
```
- **Execution**: В отдельном git worktree
- **FS изоляция**: Полная — свой клон репозитория
- **Безопасность**: Не может испортить основной рабочий каталог
- **Use case**: Кодогенерация, эксперименты, параллельные ветки
- **Hooks**: `WorktreeCreate`, `WorktreeRemove` для setup/cleanup

### 4. Custom agents (из плагинов)
- Определяются в `agents/*.md` внутри плагина
- Могут комбинировать background + isolation
- Имеют ограниченный набор tools

## Формат определения агента

### Файл: `agents/agent-name.md`

```yaml
---
name: code-reviewer              # Уникальное имя
description: |                    # Когда использовать
  Use this agent when the user asks to review code quality.
  <example>
    Context: User wants code reviewed
    user: Review the authentication module
    assistant: [calls Agent tool with code-reviewer]
    <commentary>
      Triggered because user explicitly asked for code review
    </commentary>
  </example>
model: inherit                    # inherit | sonnet | opus | haiku
color: blue                       # Цвет в UI
tools:                            # Разрешённые инструменты
  - Read
  - Grep
  - Glob
  - LS
---

# Code Reviewer Agent System Prompt

You are an expert code quality reviewer. Your role is to...

## Responsibilities
- Identify bugs and anti-patterns
- Check for security issues
- Evaluate maintainability

## Output Format
Provide findings in this structure:
1. Critical issues
2. Warnings
3. Suggestions
```

### Обязательные и опциональные поля

| Поле | Обязательное | Описание |
|---|---|---|
| `name` | Да | Уникальный идентификатор агента |
| `description` | Да | Когда и зачем использовать, с `<example>` блоками |
| `model` | Нет | Модель (inherit = та же что у parent) |
| `color` | Нет | Цвет для визуализации |
| `tools` | Нет | Массив разрешённых инструментов |
| `background` | Нет | Фоновое выполнение (true/false) |
| `isolation` | Нет | Изоляция: `worktree` |

### Triggering Examples

Каждый агент должен иметь 2-4 `<example>` блока, показывающих когда он должен быть вызван:

```xml
<example>
  Context: User is working on a Node.js project with tests
  user: Can you check if there are any security vulnerabilities?
  assistant: [calls Agent tool with security-reviewer]
  <commentary>
    The user asked about security vulnerabilities, which matches
    the security-reviewer agent's expertise.
  </commentary>
</example>
```

**Зачем нужны examples**: Claude использует semantic matching для определения, какого агента вызвать. Examples — это training data для этого matching.

## Протокол коммуникации

### Вызов субагента (Agent tool)

```json
{
  "tool": "Agent",
  "input": {
    "agent": "code-reviewer",
    "task": "Review the file src/auth/login.ts for security issues. Focus on input validation and SQL injection risks."
  }
}
```

### Что получает субагент

1. **System prompt**: Body из agent-name.md (после frontmatter)
2. **User message**: Содержимое `task` из вызова Agent tool
3. **Available tools**: Только те, что указаны в `tools:` frontmatter
4. **No parent context**: Не видит историю переписки parent agent

### Что возвращает субагент

- **Text result**: Финальный ответ субагента (summary)
- **Status**: success / failure
- Main agent получает это как tool result

### Ограничения

- ❌ Субагент **не может** вызвать Agent tool (нет вложенности)
- ❌ Субагенты **не могут** общаться друг с другом напрямую
- ❌ Субагент **не получает** историю parent agent
- ✅ Субагент **может** использовать MCP tools (если разрешены)
- ✅ Main agent **может** запускать несколько субагентов параллельно

## Пример: Команда агентов (Code Review Plugin)

```
plugins/code-review/commands/code-review.md:

/code-review → запуск 9+ агентов ПАРАЛЛЕЛЬНО:

┌──────────────────────────────────────────────────────────┐
│                    Main Agent                             │
│                 (code-review command)                     │
│                                                          │
│   ┌────────────┐  ┌────────────┐  ┌────────────┐        │
│   │ architecture│  │  security  │  │performance │        │
│   │  reviewer   │  │  reviewer  │  │  reviewer  │        │
│   └─────┬──────┘  └─────┬──────┘  └─────┬──────┘        │
│         │               │               │                │
│   ┌─────┴──────┐  ┌─────┴──────┐  ┌─────┴──────┐       │
│   │error-handler│  │ logging    │  │   testing  │        │
│   │  reviewer   │  │  reviewer  │  │  reviewer  │        │
│   └─────┬──────┘  └─────┬──────┘  └─────┬──────┘        │
│         │               │               │                │
│   ┌─────┴──────┐  ┌─────┴──────┐  ┌─────┴──────┐       │
│   │ complexity  │  │documentation│  │  style    │        │
│   │  reviewer   │  │  reviewer   │  │ reviewer  │        │
│   └─────┬──────┘  └─────┴──────┘  └─────┬──────┘       │
│         │               │               │                │
│         └───────────────┼───────────────┘                │
│                         ▼                                │
│              ┌──────────────────┐                        │
│              │  Report Compiler │                        │
│              │    (main agent)  │                        │
│              └──────────────────┘                        │
│                         │                                │
│                         ▼                                │
│            Final Markdown Report                         │
└──────────────────────────────────────────────────────────┘
```

### Детали code-review pipeline:

1. **Parse**: Определяет файлы для ревью (staging, branch diff, PR)
2. **Distribute**: Распределяет файлы по 9 специализированным агентам
3. **Parallel execution**: Все 9 агентов работают параллельно
4. **Merge**: Main agent собирает результаты в единый отчёт
5. **Format**: Создаёт Markdown-таблицу с severity levels

## Пример: Feature Development Plugin (7-фазная оркестрация)

```
/feature-dev → 7-фазный workflow:

Phase 1: Technical Lead Agent
    ↓ (планирование, анализ scope)
Phase 2: Backend Developer Agent  
    ↓ (реализация серверной части)
Phase 3: Frontend Developer Agent
    ↓ (визуальная реализация)
Phase 4: QA Engineer Agent
    ↓ (написание тестов)
Phase 5: DevOps Agent
    ↓ (конфигурация, deployment)
Phase 6: Code Reviewer Agent
    ↓ (ревью, замечания)
Phase 7: Documentation Agent
    ↓ (документирование)
= Результат: Готовая фича с тестами и документацией
```

В этом workflow **каждый агент** получает summary предыдущего этапа, НО не полную историю:
- Technical Lead → output → задача Backend Dev
- Backend Dev → output → задача Frontend Dev
- etc.

Это **цепочечная (chain) оркестрация**, а не hub-and-spoke.

## Сравнение моделей оркестрации

| Модель | Описание | Пример в Claude Code |
|---|---|---|
| **Hub-and-spoke** | Main agent координирует N субагентов | Code Review (параллельный) |
| **Chain** | Последовательная передача между агентами | Feature Dev (7 фаз) |
| **Single** | Один агент без субагентов | Простые задачи |
| **Background** | Асинхронные агенты + TeammateIdle | Мониторинг, CI watch |

## Рекомендации для AzureOpsCrew

### Что копировать:
1. **Agent = Markdown файл** — простота определения
2. **Triggering examples** — semantic matching для выбора агента
3. **Tool ограничения** — principle of least privilege
4. **Isolation modes** — worktree для безопасной кодогенерации
5. **Chain и Hub-and-spoke** — оба паттерна нужны

### Чего НЕ делать:
1. ❌ Полноценный peer-to-peer mesh — нестабильно
2. ❌ Неограниченная вложенность субагентов — context explosion
3. ❌ Shared state между субагентами — race conditions
4. ❌ Субагенты с полным контекстом parent — wasteful
