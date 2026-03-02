# AzureOpsCrew 2.0 — Архитектура и план реализации

## 📋 Что мы строим

**Discord-like чат** с мультиагентной системой для управления Azure-инфраструктурой.

- Один канал "Ops Room" с захардкоженными агентами
- Агенты общаются между собой и с пользователем
- Агенты подключаются к Azure через MCP-сервер
- Human-in-the-loop: подтверждение деплоев, пайплайнов, code-fix'ов
- Всё на .NET 10 + Microsoft Agents Framework + CopilotKit + AG-UI

---

## 🏗 Высокоуровневая архитектура

```
┌──────────────────────────────────────────────────────────┐
│  Frontend (Next.js 16 + CopilotKit + AG-UI)             │
│  ┌─────────────┐ ┌──────────────┐ ┌──────────────────┐  │
│  │ Chat UI     │ │ CopilotKit   │ │ Interactive      │  │
│  │ (Discord-   │ │ Provider     │ │ Cards            │  │
│  │  style)     │ │ + AG-UI SSE  │ │ (approve/reject) │  │
│  └──────┬──────┘ └──────┬───────┘ └────────┬─────────┘  │
│         └───────────────┼──────────────────┘             │
│                         │ SSE stream                     │
│                         ▼                                │
│  ┌─────────────────────────────────────────┐             │
│  │ Next.js API Routes (proxy to backend)   │             │
│  └─────────────────────┬───────────────────┘             │
└────────────────────────┼─────────────────────────────────┘
                         │ HTTP/SSE
┌────────────────────────┼─────────────────────────────────┐
│  Backend (.NET 10 API) │                                 │
│                        ▼                                 │
│  ┌─────────────────────────────────────────┐             │
│  │ AG-UI Endpoint: POST /api/crew/chat     │             │
│  │ (единственный endpoint для чата)         │             │
│  └─────────────────────┬───────────────────┘             │
│                        ▼                                 │
│  ┌─────────────────────────────────────────┐             │
│  │ Microsoft Agents Group Chat             │             │
│  │ ┌─────────┐ ┌────────┐ ┌────────────┐  │             │
│  │ │ Manager │ │ DevOps │ │ Developer  │  │             │
│  │ │ (орк.)  │ │ Agent  │ │ Agent      │  │             │
│  │ └────┬────┘ └───┬────┘ └─────┬──────┘  │             │
│  │      │          │            │          │             │
│  │      ▼          ▼            ▼          │             │
│  │  ┌──────────────────────────────────┐   │             │
│  │  │ MCP Client → Azure MCP Server   │   │             │
│  │  │ (App Insights, Repos, Pipelines) │   │             │
│  │  └──────────────────────────────────┘   │             │
│  └─────────────────────────────────────────┘             │
│                                                          │
│  ┌─────────────────────────────┐                         │
│  │ Long-Term Memory (Neo4j)    │                         │
│  │ (контекст каждого агента)   │                         │
│  └─────────────────────────────┘                         │
│                                                          │
│  ┌─────────────────────────────┐                         │
│  │ SQLite (users, agents, etc) │                         │
│  └─────────────────────────────┘                         │
└──────────────────────────────────────────────────────────┘
                         │
                         ▼
┌──────────────────────────────────────────────────────────┐
│  Azure MCP Server (отдельный)                            │
│  - Application Insights (логи, ошибки, метрики)          │
│  - Azure Repos / GitHub (код, PR, коммиты)               │
│  - Azure Pipelines (запуск, статус)                       │
│  - Azure Resource Manager (ресурсы, статус)               │
└──────────────────────────────────────────────────────────┘
```

---

## 🤖 Агенты (захардкожены)

### 1. **Manager** (Orchestrator)
- **Роль**: Получает задачу от пользователя, декомпозирует, раздаёт агентам, собирает результаты
- **Промпт**: Координация команды, приоритезация, отчёт пользователю
- **Tools**: Нет MCP tools (только оркестрация через GroupChat)
- **Цвет**: `#9b59b6` (фиолетовый)

### 2. **DevOps Agent**  
- **Роль**: Мониторинг, диагностика, пайплайны
- **Промпт**: Анализ Application Insights, запуск/проверка пайплайнов, проверка ресурсов
- **MCP Tools**: `appinsights_query`, `pipeline_status`, `pipeline_run`, `resource_health`
- **Цвет**: `#0078d4` (синий Azure)

### 3. **Developer Agent**
- **Роль**: Анализ кода, исправление багов, code review
- **Промпт**: Чтение кода из репозитория, анализ стэктрейсов, предложение фиксов
- **MCP Tools**: `repo_file_read`, `repo_search`, `repo_pr_create`
- **Цвет**: `#43b581` (зелёный)

---

## 🔄 Поток данных (сценарий)

```
User → "Ребята, у нас 500 ошибки на API, разберитесь"
  │
  ▼
Manager: "Принял. DevOps, проверь Application Insights на 500-ки"
  │
  ▼
DevOps: [вызывает MCP: appinsights_query] 
        "Нашёл 43 ошибки за последний час. NullReferenceException в OrderController.cs:142"
        [показывает карточку с метриками через showMetrics]
  │
  ▼
Manager: "Developer, посмотри код OrderController.cs, строка 142"
  │
  ▼
Developer: [вызывает MCP: repo_file_read]
           "Нашёл проблему — отсутствует null-check для order.Customer"
           "Предлагаю фикс: добавить проверку..."
           [показывает код в чате]
  │
  ▼  
Manager: "User, подтверди — создать PR с этим фиксом?"
  │
  ▼ (Human-in-the-loop)
User: "Да, создавайте"
  │
  ▼
Developer: [вызывает MCP: repo_pr_create]
           "PR #247 создан: 'Fix NullReferenceException in OrderController'"
  │
  ▼
Manager: "DevOps, запусти CI/CD для этого PR"
  │
  ▼
DevOps: [вызывает MCP: pipeline_run]
        "Pipeline запущен"
        [показывает карточку PipelineStatus]
```

---

## 📁 Структура файлов (что менять)

Подробный план по файлам — в отдельных документах:

1. [BACKEND_CHANGES.md](./BACKEND_CHANGES.md) — все изменения на бэкенде
2. [FRONTEND_CHANGES.md](./FRONTEND_CHANGES.md) — все изменения на фронтенде  
3. [DOCKER_AND_CONFIG.md](./DOCKER_AND_CONFIG.md) — Docker, .env, конфигурация
4. [MCP_INTEGRATION.md](./MCP_INTEGRATION.md) — подключение Azure MCP сервера
5. [IMPLEMENTATION_ORDER.md](./IMPLEMENTATION_ORDER.md) — порядок реализации по шагам

---

## ⚠️ Что убираем (лишнее для хакатона)

1. **Email verification (Brevo)** — регистрация не нужна, делаем auto-login или простой hardcoded user
2. **Provider CRUD** — провайдер один (OpenAI), захардкожен в конфиге
3. **Agent CRUD** — агенты захардкожены (Manager, DevOps, Developer)
4. **Channel CRUD** — канал один "Ops Room", захардкожен
5. **Settings UI** — не нужен для демо
6. **Direct Messages** — не нужны для демо
7. **Multiple providers** (Anthropic, Ollama, OpenRouter) — только OpenAI

## ✅ Что оставляем и дорабатываем

1. **API на .NET 10** — основа, переписываем endpoint'ы
2. **AG-UI SSE streaming** — уже работает, расширяем для multi-agent
3. **Microsoft Agents GroupChat** — уже подключен (RoundRobin), переделываем на нормальную оркестрацию
4. **Long-Term Memory (Neo4j)** — даёт каждому агенту свой контекст/память
5. **Frontend chat UI** — оставляем Discord-стиль, упрощаем
6. **CopilotKit + AG-UI** — для интерактивных карточек (approve/reject)
7. **Docker Compose** — обновляем для нового стека

## 🔑 Ключевые технические решения

| Решение | Выбор | Почему |
|---------|-------|--------|
| LLM | OpenAI `gpt-4o-mini` | дёшево, быстро, достаточно умно |
| Orchestration | Microsoft Agents GroupChat + Manager Agent | нативно из фреймворка |
| MCP | Azure MCP Server (внешний) | уже есть, даёт доступ к Azure |
| Frontend-Backend | AG-UI SSE stream | уже реализовано, стабильно |
| Interactive cards | CopilotKit Actions | approve/reject кнопки в чате |
| Memory | Neo4j (Cypher) | уже подключен, per-agent контекст |
| Auth | Упрощённый JWT (auto-seed user) | для демо не нужна регистрация |
| DB | SQLite | достаточно для демо |
