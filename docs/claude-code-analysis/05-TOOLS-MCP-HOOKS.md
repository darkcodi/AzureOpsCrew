# E) Tools, MCP Servers & Hooks — Claude Code

## Каталог встроенных инструментов

| Tool | Назначение | Категория | Доступен субагентам |
|---|---|---|---|
| **Read** | Чтение файлов (с line ranges) | File I/O | ✅ |
| **Write** | Создание новых файлов | File I/O | ✅ |
| **Edit** | Редактирование существующих файлов | File I/O | ✅ |
| **Bash** | Выполнение shell-команд | Execution | ✅ (ограничения) |
| **Grep** | Текстовый поиск (regex) | Search | ✅ |
| **Glob** | Поиск файлов по паттерну | Search | ✅ |
| **LS** | Листинг директорий | Search | ✅ |
| **Agent** | Вызов субагента | Orchestration | ❌ (не для субагентов) |
| **WebFetch** | HTTP-запросы к URL | Network | ✅ |
| **WebSearch** | Поиск в интернете | Network | ✅ |
| **NotebookRead** | Чтение Jupyter ноутбуков | File I/O | ✅ |
| **TodoWrite** | Управление todo-списком | State | ✅ |
| **AskUserQuestion** | Вопрос пользователю | Interaction | ❌ (не для субагентов) |
| **KillShell** | Остановка процесса в терминале | Execution | ✅ |
| **BashOutput** | Получение вывода фонового процесса | Execution | ✅ |

### Детали важных инструментов

#### Bash
```json
{
  "tool": "Bash",
  "input": {
    "command": "npm test -- --coverage",
    "timeout": 30000,
    "description": "Run unit tests with coverage"
  }
}
```
- **Permissions**: Контролируется через allow/deny паттерны
- **Timeout**: Настраиваемый, default ~120s
- **Pattern matching**: `Bash(npm:*)`, `Bash(git:*)` для точного контроля
- **Safety**: PreToolUse hooks для валидации команд

#### Agent
```json
{
  "tool": "Agent",
  "input": {
    "agent": "security-reviewer",
    "task": "Review auth module for vulnerabilities"
  }
}
```
- **Нет вложенности**: Субагент не может вызвать Agent tool
- **Task = единственный вход**: Субагент не получает историю parent

#### Read
- Поддерживает line ranges: `startLine`, `endLine`
- Поддерживает offset-based чтение
- Интеграция с tree-sitter для syntax-aware чтения
- Max read size configurable

## MCP (Model Context Protocol) серверы

### Конфигурация (.mcp.json)
```json
{
  "mcpServers": {
    "filesystem": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/path/to/allowed"],
      "env": {
        "NODE_ENV": "production"
      }
    },
    "remote-api": {
      "type": "sse",
      "url": "https://api.example.com/mcp/sse",
      "headers": {
        "Authorization": "Bearer ${MCP_API_KEY}"
      }
    },
    "http-server": {
      "type": "http",
      "url": "https://api.example.com/mcp",
      "headers": {}
    },
    "websocket-server": {
      "type": "ws",
      "url": "wss://ws.example.com/mcp"
    }
  }
}
```

### Типы MCP транспорта

| Тип | Протокол | Use Case |
|---|---|---|
| `stdio` | stdin/stdout процесса | Локальные серверы, CLI-обёртки |
| `sse` | Server-Sent Events over HTTP | Remote servers (legacy) |
| `http` | HTTP Streamable | Remote servers (modern, v2.1.52+) |
| `ws` | WebSocket | Real-time bidirectional |

### Расположение .mcp.json

```
~/.claude/.mcp.json          ← Глобальные (для пользователя)
/project/.claude/.mcp.json   ← Для проекта
/plugin/.mcp.json             ← Для плагина
```

### Переменные окружения
```json
{
  "env": {
    "API_KEY": "${env:MY_API_KEY}",
    "HOME": "${HOME}"
  }
}
```
- Поддержка `${env:VAR}` синтаксиса
- Envvar substitution в args и headers
- Подробный обработчик ошибок для неподдерживаемого формата

### MCP в плагинах
- Каждый плагин может иметь свой `.mcp.json`
- MCP серверы из плагинов регистрируются при загрузке плагина
- Переменная `${CLAUDE_PLUGIN_ROOT}` для путей

## Система Hooks

### Архитектура Hooks

```
┌──────────────────────────────────────────────────────────────┐
│                     HOOKS PIPELINE                            │
│                                                              │
│  Event ──▶ Matcher ──▶ Hook Scripts ──▶ Output ──▶ Action    │
│            (filter)    (parallel!)     (JSON)     (process)  │
│                                                              │
│  Types:                                                      │
│    • command hooks (external process)                         │
│    • prompt hooks (LLM evaluation)                            │
└──────────────────────────────────────────────────────────────┘
```

### Все Hook Events

| Event | Когда | Input | Может блокировать |
|---|---|---|---|
| **PreToolUse** | Перед вызовом инструмента | tool name, input params | ✅ (exit 2 = block) |
| **PostToolUse** | После выполнения инструмента | tool name, result | ❌ |
| **Stop** | Когда Claude готов остановиться | final response | ✅ (exit 2 = continue) |
| **SubagentStop** | Когда субагент завершился | agent result | ✅ |
| **SessionStart** | При старте сессии | session info | ❌ |
| **SessionEnd** | При завершении сессии | session summary | ❌ |
| **PreCompact** | Перед компакцией контекста | current context | ❌ |
| **UserPromptSubmit** | При отправке сообщения пользователем | user message | ✅ |
| **Notification** | При уведомлении от Claude | notification text | ❌ |
| **InstructionsLoaded** | После загрузки CLAUDE.md | instructions text | ❌ |
| **TeammateIdle** | Когда фоновый агент простаивает | agent status | ❌ |
| **TaskCompleted** | При завершении задачи | task result | ❌ |
| **ConfigChange** | При изменении конфигурации | config diff | ❌ |
| **WorktreeCreate** | При создании worktree | worktree path | ❌ |
| **WorktreeRemove** | При удалении worktree | worktree path | ❌ |

### Формат hooks.json

```json
{
  "hooks": [
    {
      "event": "PreToolUse",
      "matcher": "Bash",
      "hooks": [
        {
          "type": "command",
          "command": "python3 ${CLAUDE_PLUGIN_ROOT}/scripts/validate_bash.py"
        }
      ]
    },
    {
      "event": "Stop",
      "hooks": [
        {
          "type": "prompt",
          "prompt": "Before stopping, verify all tests pass. If not, continue working."
        }
      ]
    }
  ]
}
```

### Matcher Patterns

| Pattern | Описание | Пример |
|---|---|---|
| `"Bash"` | Точное совпадение с tool name | PreToolUse для Bash |
| `"Bash(npm:*)"` | Tool + argument pattern | Bash с npm командами |
| `"mcp__*"` | Wildcard для MCP tools | Все MCP инструменты |
| `null` / отсутствует | Все tool calls | Универсальный hook |

### Command Hook I/O

**Input (stdin)**:
```json
{
  "sessionId": "abc123",
  "toolName": "Bash",
  "toolInput": {
    "command": "rm -rf /important"
  }
}
```

**Output (stdout)**:
```json
{
  "decision": "block",
  "reason": "Dangerous command detected: rm -rf on root path"
}
```

### Exit Codes (Command Hooks)

| Exit Code | Значение |
|---|---|
| 0 | ОК, продолжить выполнение |
| 2 | Блокировать действие (для PreToolUse); продолжить работу (для Stop) |
| Другие | Ошибка, логировать |

### Prompt Hooks
```json
{
  "type": "prompt",
  "prompt": "Check if the code being written follows our style guide. If not, suggest corrections."
}
```
- **Execution**: LLM оценивает ситуацию по промпту
- **Use case**: Валидация, гардрейлы, стилевые проверки
- **Cost**: Дополнительные API-вызовы

### Параллельность

- Все hooks для одного event выполняются **параллельно**
- Если ЛЮБОЙ hook возвращает block → действие блокируется
- Hooks из разных плагинов комбинируются

## Практический пример: Security Hooks

Из `plugins/security-guidance/hooks/hooks.json`:

```json
{
  "hooks": [
    {
      "event": "PreToolUse",
      "matcher": "Bash",
      "hooks": [
        {
          "type": "command",
          "command": "python3 validate_command.py"
        }
      ]
    },
    {
      "event": "PreToolUse", 
      "matcher": "Write",
      "hooks": [
        {
          "type": "command",
          "command": "python3 check_sensitive_files.py"
        }
      ]
    }
  ]
}
```

## Практический пример: Hookify Plugin

Из `plugins/hookify/`:
- **Language**: Python
- **Purpose**: Полноценный фреймворк для написания hooks
- **Features**:
  - Bash command validator (dangerous commands blacklist)
  - File write guardian (protected paths)
  - Session logger
  - Custom validation pipeline
- **Config**: `hookify.yaml` для декларативной настройки

```python
# Пример: hookify validate_bash.py
import json, sys

data = json.load(sys.stdin)
command = data.get("toolInput", {}).get("command", "")

DANGEROUS = ["rm -rf /", "mkfs", "dd if=", ":(){:|:&};:"]
for pattern in DANGEROUS:
    if pattern in command:
        result = {"decision": "block", "reason": f"Blocked: {pattern}"}
        json.dump(result, sys.stdout)
        sys.exit(2)

sys.exit(0)
```

## Взаимодействие Tools ↔ MCP ↔ Hooks

```
User request
    │
    ▼
Claude decides to use tool
    │
    ▼
┌────────────────┐     ┌────────────────┐
│ Permission     │────▶│ PreToolUse     │
│ Check          │     │ Hooks          │
│ (allow/deny)   │     │ (parallel)     │
└────────────────┘     └───────┬────────┘
                               │
                    ┌──────────┴──────────┐
                    │ Any hook blocked?    │
                    │  Yes → abort tool   │
                    │  No → proceed       │
                    └──────────┬──────────┘
                               │
                    ┌──────────┴──────────┐
                    │ Built-in tool?       │
                    │  Yes → execute       │
                    │  No → MCP call       │
                    └──────────┬──────────┘
                               │
                    ┌──────────┴──────────┐
                    │ PostToolUse Hooks    │
                    │ (parallel, no block) │
                    └──────────┬──────────┘
                               │
                               ▼
                    Tool result → Claude
```
