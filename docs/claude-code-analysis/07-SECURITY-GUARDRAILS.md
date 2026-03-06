# G) Безопасность, Guardrails и Песочница — Claude Code

## Многоуровневая модель безопасности

```
┌──────────────────────────────────────────────────────────────────┐
│                    SECURITY LAYERS                                │
│                                                                  │
│  Layer 1: MANAGED SETTINGS (enterprise)                          │
│     ↓  Жёсткие ограничения от организации                        │
│                                                                  │
│  Layer 2: PERMISSIONS MODEL (allow/deny/ask)                     │
│     ↓  Per-tool правила                                          │
│                                                                  │
│  Layer 3: SANDBOX (network isolation)                             │
│     ↓  Ограничение сети и файловой системы                       │
│                                                                  │
│  Layer 4: HOOKS (PreToolUse validation)                           │
│     ↓  Программная валидация перед действиями                    │
│                                                                  │
│  Layer 5: TOOL RESTRICTIONS (per agent)                           │
│     ↓  Субагенты имеют минимальный набор инструментов            │
│                                                                  │
│  Layer 6: PROMPT INJECTION DEFENSE                                │
│     ↓  Защита от манипуляций                                     │
│                                                                  │
│  Layer 7: LLM SAFETY (built-in model guardrails)                  │
│     Anthropic safety training                                    │
└──────────────────────────────────────────────────────────────────┘
```

## Permissions Model

### Три режима для каждого tool

| Режим | Поведение | Флаг |
|---|---|---|
| **allow** | Выполнять без вопросов | Зелёный |
| **deny** | Блокировать без вопросов | Красный |
| **ask** | Спрашивать пользователя каждый раз | Жёлтый |

### Конфигурация permissions

#### settings.json
```json
{
  "permissions": {
    "allow": [
      "Read",
      "Grep",
      "Glob",
      "LS",
      "Bash(git:*)",
      "Bash(npm test:*)",
      "mcp__filesystem__*"
    ],
    "deny": [
      "Bash(rm -rf:*)",
      "Bash(curl:*)",
      "Bash(wget:*)"
    ]
  }
}
```

#### Pattern синтаксис
```
ToolName                    → все вызовы этого tool
ToolName(pattern:*)         → с аргументом matching pattern
Bash(npm:*)                 → все npm команды
Bash(git log:*)             → конкретная git команда
mcp__servername__toolname   → MCP tool
mcp__*                      → все MCP tools
```

### Иерархия settings

```
HIGHEST PRIORITY:
┌─────────────────────────────────────────────┐
│ 1. Managed Settings (enterprise)            │ ← НЕ переопределяется
│    ~/.claude/managed-settings.json          │
├─────────────────────────────────────────────┤
│ 2. User Settings                            │
│    ~/.claude/settings.json                  │
├─────────────────────────────────────────────┤
│ 3. Project Settings                         │
│    .claude/settings.json                    │
├─────────────────────────────────────────────┤
│ 4. Local Project Settings                   │
│    .claude/settings.local.json              │ ← git-ignored
└─────────────────────────────────────────────┘
LOWEST PRIORITY
```

### Managed Settings (Enterprise)

```json
{
  "permissions": {
    "deny": ["Bash(curl:*)", "Bash(wget:*)"]
  },
  "disableBypassPermissionsMode": true,
  "allowManagedPermissionRulesOnly": true,
  "disableMcpServers": false,
  "allowedMcpServers": ["@modelcontextprotocol/server-filesystem"],
  "disablePlugins": false,
  "allowedPlugins": ["code-review", "security-guidance"]
}
```

- **disableBypassPermissionsMode**: Запрещает `--dangerously-skip-permissions`
- **allowManagedPermissionRulesOnly**: Только managed rules, user не может добавлять allow
- **allowedMcpServers**: Whitelist MCP серверов
- **allowedPlugins**: Whitelist плагинов

## Sandbox (Network Isolation)

### macOS Sandbox
- **Механизм**: macOS App Sandbox (sandbox-exec)
- **Сеть**: По умолчанию ограничена
- **allowedDomains**: 
  ```json
  {
    "sandbox": {
      "allowedDomains": [
        "api.anthropic.com",
        "api.github.com",
        "registry.npmjs.org"
      ]
    }
  }
  ```
- **httpProxyPort**: Локальный прокси для контроля HTTP-трафика

### Linux Container Sandbox
- **Механизм**: Container-based isolation
- **Limitations**: Ограниченная файловая система, сеть

### Sandbox vs Tools

| Аспект | Без sandbox | С sandbox |
|---|---|---|
| Network | Полный доступ | Только allowedDomains |
| Filesystem | Полный | Working directory + allowed paths |
| Processes | Полный | Ограничены |
| Env vars | Полный | Filtered |

## Tool Restrictions для субагентов

### Principle of Least Privilege

```yaml
---
name: code-reviewer
tools:
  - Read       # ✅ Может читать
  - Grep       # ✅ Может искать
  - Glob       # ✅ Может находить файлы
  - LS         # ✅ Может листать директории
  # ❌ НЕТ Write, Edit, Bash, Agent
---
```

Каждый субагент получает **только перечисленные tools**:
- **Read-only агенты**: Read, Grep, Glob, LS
- **Full-access агенты**: Read, Write, Edit, Bash, Grep, Glob, LS
- **Специализированные**: Конкретный набор под задачу

### Нет Agent tool у субагентов

> Субагент НИКОГДА не может вызвать Agent tool → нет бесконечной рекурсии

## Hooks как Guardrails

### Блокирующие hooks (PreToolUse)

```python
#!/usr/bin/env python3
# validate_bash_command.py

import json, sys

BLOCKED_PATTERNS = [
    "rm -rf /",
    "rm -rf ~",
    ":(){:|:&};:",     # fork bomb
    "mkfs",
    "dd if=/dev",
    "> /dev/sda",
    "chmod 777",
    "curl | sh",       # pipe to shell
    "wget | bash",
]

BLOCKED_COMMANDS = [
    "shutdown",
    "reboot",
    "poweroff",
    "halt",
]

data = json.load(sys.stdin)
command = data.get("toolInput", {}).get("command", "")

# Check patterns
for pattern in BLOCKED_PATTERNS:
    if pattern in command:
        json.dump({
            "decision": "block",
            "reason": f"Blocked dangerous pattern: {pattern}"
        }, sys.stdout)
        sys.exit(2)

# Check commands
first_word = command.strip().split()[0] if command.strip() else ""
if first_word in BLOCKED_COMMANDS:
    json.dump({
        "decision": "block",
        "reason": f"Blocked dangerous command: {first_word}"
    }, sys.stdout)
    sys.exit(2)

sys.exit(0)
```

### Защита файлов (Write hook)

```python
#!/usr/bin/env python3
# protect_files.py

import json, sys

PROTECTED_PATHS = [
    ".env",
    ".env.local",
    "*.pem",
    "*.key",
    "id_rsa",
    "credentials.json",
    "secrets.yaml",
]

data = json.load(sys.stdin)
file_path = data.get("toolInput", {}).get("file_path", "")

import fnmatch
for pattern in PROTECTED_PATHS:
    if fnmatch.fnmatch(file_path, pattern) or file_path.endswith(pattern):
        json.dump({
            "decision": "block",
            "reason": f"Protected file: {pattern}"
        }, sys.stdout)
        sys.exit(2)

sys.exit(0)
```

## Защита от Prompt Injection

### Проблемы (из CHANGELOG)

| Версия | Проблема / Фикс |
|---|---|
| v2.1.27 | Symlink bypass — обход через символические ссылки |
| v2.1.39 | Permission fix для symlinks |
| v2.1.52 | MCP tool permission inheritance fix |
| v2.1.62 | Managed settings enforcement strengthening |

### Стратегии защиты

1. **Tool result isolation**: Результаты tools маркируются как data, не instructions
2. **Permission boundaries**: Каждый tool call проверяется
3. **No nested agents**: Нет возможности создать цепочку эскалации привилегий
4. **Hook validation**: External scripts проверяют каждое действие
5. **Managed settings**: Организация может ограничить capabilities жёстко
6. **Symlink resolution**: Проверка реальных путей, не символических

## Два профиля безопасности (из examples/)

### Strict Profile (settings-strict.json)
```json
{
  "permissions": {
    "allow": ["Read", "Grep", "Glob", "LS"],
    "deny": [
      "Bash(*)",
      "Write(*)",
      "Edit(*)",
      "WebFetch(*)",
      "mcp__*"
    ]
  },
  "disableBypassPermissionsMode": true,
  "sandbox": {
    "allowedDomains": []
  }
}
```
- ❌ Нет bash
- ❌ Нет записи файлов
- ❌ Нет сети
- ✅ Только чтение и поиск

### Lax Profile (settings-lax.json)
```json
{
  "permissions": {
    "allow": [
      "Read", "Write", "Edit",
      "Bash(*)",
      "Grep", "Glob", "LS",
      "WebFetch(*)",
      "mcp__*"
    ]
  }
}
```
- ✅ Всё разрешено
- ⚠️ Для доверенных окружений (sandbox containers)

## Рекомендации для AzureOpsCrew

### Обязательно реализовать:
1. **Permission model** (allow/deny/ask) — базовая безопасность
2. **Tool restrictions для субагентов** — principle of least privilege
3. **PreToolUse hooks** — валидация перед execution
4. **Managed settings** — enterprise-level control

### Архитектурные принципы:
1. **Defense in depth** — несколько слоёв, каждый добавляет защиту
2. **Fail-closed** — при сомнении → block
3. **Audit trail** — логировать все tool calls
4. **No escalation** — субагенты не могут получить больше прав чем parent
5. **Deterministic validation** — hooks выполняют конкретные проверки, не LLM
