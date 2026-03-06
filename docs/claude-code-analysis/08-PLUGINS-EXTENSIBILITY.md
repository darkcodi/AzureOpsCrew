# H) Система плагинов и расширяемость — Claude Code

## Архитектура плагинов

```
┌──────────────────────────────────────────────────────────────────┐
│                     PLUGIN ARCHITECTURE                          │
│                                                                  │
│  Claude Code Runtime                                            │
│  ├── Plugin Loader                                               │
│  │   ├── Discover plugins (.claude-plugin/marketplace.json)      │
│  │   ├── Validate plugin.json manifests                          │
│  │   ├── Register commands (slash commands)                      │
│  │   ├── Register agents                                         │
│  │   ├── Register skills                                         │
│  │   ├── Register hooks                                          │
│  │   └── Register MCP servers                                    │
│  │                                                               │
│  └── Plugin Isolation                                            │
│      ├── Separate config namespaces                              │
│      ├── ${CLAUDE_PLUGIN_ROOT} variable                          │
│      └── Settings hierarchy merging                              │
└──────────────────────────────────────────────────────────────────┘
```

## Структура плагина

```
my-plugin/
├── .claude-plugin/
│   └── plugin.json              ← Манифест (обязательный)
├── agents/                       ← Определения агентов
│   ├── agent-one.md
│   └── agent-two.md
├── commands/                     ← Slash-команды
│   ├── command-one.md
│   └── subgroup/
│       └── command-two.md       ← /command-two (my-plugin:subgroup)
├── skills/                       ← Контекстные знания
│   └── skill-name/
│       ├── SKILL.md             ← Главный файл навыка
│       └── references/          ← Дополнительные материалы
│           ├── example.md
│           └── api-reference.md
├── hooks/                        ← Hooks
│   └── hooks.json
├── scripts/                      ← Вспомогательные скрипты
│   └── validator.py
├── .mcp.json                     ← MCP серверы плагина
├── settings.json                 ← Settings для плагина
└── README.md
```

## Манифест (plugin.json)

```json
{
  "name": "my-awesome-plugin",
  "version": "1.0.0",
  "description": "Does amazing things with code",
  "author": "developer@example.com",
  "homepage": "https://github.com/dev/plugin",
  "repository": "https://github.com/dev/plugin",
  "license": "MIT",
  "tags": ["code-quality", "review", "security"],
  "claude_min_version": "2.1.49"
}
```

### Обязательные поля
| Поле | Описание |
|---|---|
| `name` | Уникальный идентификатор плагина |
| `version` | Семантическое версионирование |
| `description` | Описание назначения |

### Опциональные поля
| Поле | Описание |
|---|---|
| `author` | Автор |
| `homepage` | URL документации |
| `repository` | URL репозитория |
| `license` | Лицензия |
| `tags` | Теги для поиска |
| `claude_min_version` | Минимальная версия Claude Code |

## Компоненты плагина

### 1. Commands (Slash-команды)

**Файл**: `commands/command-name.md`

```yaml
---
description: Review code for security issues
allowed-tools: Read, Grep, Bash(git:*), Agent
model: sonnet
argument-hint: [pr-number]
---

Review the code changes in PR $ARGUMENTS for security vulnerabilities.

Focus on:
1. SQL injection
2. XSS
3. Authentication bypass
4. Sensitive data exposure

Use `!`git diff origin/main..HEAD`` to see changes.
Read each changed file using @$1 if a specific file is mentioned.
```

**Специальные переменные**:
| Переменная | Описание |
|---|---|
| `$ARGUMENTS` | Все аргументы после команды |
| `$1`, `$2`, `$3` | Позиционные аргументы |
| `@$1` | Включить содержимое файла (file reference) |
| `` !`command` `` | Выполнить bash inline, вставить результат |
| `${CLAUDE_PLUGIN_ROOT}` | Путь к корню плагина |

**Frontmatter поля**:
| Поле | Описание |
|---|---|
| `description` | Описание для help |
| `allowed-tools` | Разрешённые инструменты (comma-separated) |
| `model` | Модель для выполнения |
| `argument-hint` | Подсказка аргументов в UI |

**Namespacing**: Поддиректории → пространства имён
```
commands/review.md        → /review (plugin-name)
commands/ci/build.md      → /build (plugin-name:ci)
commands/ci/deploy.md     → /deploy (plugin-name:ci)
```

### 2. Agents

**Файл**: `agents/agent-name.md` (см. детали в [04-AGENTS-SUBAGENTS-TEAMS.md](04-AGENTS-SUBAGENTS-TEAMS.md))

### 3. Skills

**Файл**: `skills/skill-name/SKILL.md`

```yaml
---
name: React Best Practices
description: |
  This skill should be used when the user asks about React component design,
  hooks patterns, or performance optimization in React applications.
version: 0.1.0
---

# React Best Practices

## Component Design
...

## References
See ${CLAUDE_SKILL_DIR}/references/hooks-patterns.md for detailed hook examples.
```

**Авто-загрузка**: Skills загружаются автоматически когда user intent matches description.

**Переменные**:
| Переменная | Описание |
|---|---|
| `${CLAUDE_SKILL_DIR}` | Путь к директории навыка |
| `${CLAUDE_PLUGIN_ROOT}` | Путь к корню плагина |

**Структура references/**:
```
skills/my-skill/
├── SKILL.md                    ← Главный файл (загружается первым)
└── references/
    ├── api-reference.md         ← Детальная документация
    ├── examples.md              ← Примеры кода
    └── system-prompt.md         ← Шаблон промпта для создания
```

### 4. Hooks

**Файл**: `hooks/hooks.json` (см. детали в [05-TOOLS-MCP-HOOKS.md](05-TOOLS-MCP-HOOKS.md))

### 5. MCP Servers

**Файл**: `.mcp.json` в корне плагина (см. раздел MCP в [05-TOOLS-MCP-HOOKS.md](05-TOOLS-MCP-HOOKS.md))

### 6. Settings

**Файл**: `settings.json` в корне плагина

```json
{
  "permissions": {
    "allow": ["Bash(npm test:*)"]
  },
  "model": "sonnet"
}
```

## Marketplace

### Файл: `.claude-plugin/marketplace.json`

```json
{
  "plugins": [
    {
      "name": "feature-dev",
      "description": "Full-stack feature development with 7-phase workflow",
      "repository": "https://github.com/anthropics/claude-code",
      "path": "plugins/feature-dev"
    },
    {
      "name": "code-review",
      "description": "Comprehensive code review with 9 specialized agents",
      "repository": "https://github.com/anthropics/claude-code",
      "path": "plugins/code-review"
    }
  ]
}
```

### 13 плагинов Claude Code (registry)

| # | Plugin | Описание | Компоненты |
|---|---|---|---|
| 1 | **plugin-dev** | Инструменты разработки плагинов | 7 skills |
| 2 | **feature-dev** | 7-фазная разработка фич | 3 agents + 1 command |
| 3 | **code-review** | 9-агентный code review | 9 agents + 1 command |
| 4 | **security-guidance** | Security hooks и рекомендации | hooks + skills |
| 5 | **hookify** | Python-фреймворк hooks | hooks + scripts |
| 6 | **mcp-integration** | MCP server примеры | .mcp.json + skills |
| 7 | **prompt-patterns** | Паттерны prompt engineering | skills |
| 8 | **testing** | Тестирование и QA | agents + commands |
| 9 | **documentation** | Авто-документирование | commands |
| 10 | **refactoring** | Рефакторинг кода | agents + commands |
| 11 | **debugging** | Отладка и диагностика | agents + hooks |
| 12 | **git-workflows** | Git workflow автоматизация | commands |
| 13 | **ci-integration** | CI/CD интеграция | hooks + commands |

## Жизненный цикл плагина

```
1. INSTALL
   claude /install-plugin <url-or-path>
   → Клонирует/копирует в ~/.claude/plugins/
   → Валидирует plugin.json

2. LOAD
   При старте сессии (или /reload-plugins):
   → Читает plugin.json
   → Регистрирует commands в slash-command router
   → Регистрирует agents в agent registry
   → Загружает skills descriptions (lazy loading)
   → Регистрирует hooks
   → Запускает MCP серверы (если есть)

3. USE
   Пользователь использует через:
   → /command-name — slash commands
   → Automatic agent selection — по description match
   → Automatic skill loading — по description match
   → Background hooks — автоматически на events

4. UPDATE
   /update-plugin <name>
   → Git pull или re-download
   → Reload

5. REMOVE
   /remove-plugin <name>
   → Удаление из ~/.claude/plugins/
```

## Переменная ${CLAUDE_PLUGIN_ROOT}

### Назначение
Абсолютный путь к корневой директории текущего плагина. Используется для:
- Ссылок на скрипты в hooks
- Путей к reference файлам в skills
- Конфигурации MCP серверов

### Использование
```json
// hooks/hooks.json
{
  "hooks": [{
    "event": "PreToolUse",
    "hooks": [{
      "type": "command",
      "command": "python3 ${CLAUDE_PLUGIN_ROOT}/scripts/validate.py"
    }]
  }]
}
```

```markdown
<!-- skills/my-skill/SKILL.md -->
See ${CLAUDE_SKILL_DIR}/references/api.md for details.
```

## Blueprint для создания плагина в AzureOpsCrew

### Минимальный плагин

```
azure-ops-plugin/
├── .claude-plugin/
│   └── plugin.json
├── commands/
│   └── deploy-check.md
└── README.md
```

### Полноценный плагин

```
azure-ops-plugin/
├── .claude-plugin/
│   └── plugin.json
├── agents/
│   ├── infrastructure-analyst.md
│   ├── cost-optimizer.md
│   └── security-auditor.md
├── commands/
│   ├── analyze.md
│   ├── optimize.md
│   └── audit/
│       ├── security.md
│       └── compliance.md
├── skills/
│   └── azure-best-practices/
│       ├── SKILL.md
│       └── references/
│           ├── naming-conventions.md
│           └── resource-limits.md
├── hooks/
│   └── hooks.json
├── scripts/
│   ├── validate_resource.py
│   └── check_costs.py
├── .mcp.json
├── settings.json
└── README.md
```

### Ключевые паттерны для AzureOpsCrew

1. **Plugin = модуль расширения** — изолированный, version-controlled
2. **Markdown = конфигурация** — agents, commands, skills — всё в MD
3. **Skills = lazy knowledge** — загружаются по необходимости
4. **Hooks = policy enforcement** — автоматическая валидация
5. **MCP = external integration** — внешние сервисы через стандартный протокол
