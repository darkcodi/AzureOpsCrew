# AzureOpsCrew — Руководство по запуску

## Предварительные требования

| Компонент | Где установлен | Проверить |
|-----------|---------------|-----------|
| .NET 10 SDK | `/tmp/dotnet10/` | `/tmp/dotnet10/dotnet --version` → `10.0.103` |
| Node.js | стандартный | `node -v` |
| pnpm | стандартный | `pnpm -v` |

> **Важно:** .NET 10 установлен во временную папку `/tmp/dotnet10/`. После перезагрузки Mac она может быть стёрта. Если `dotnet` пропал — см. раздел «Переустановка .NET 10» внизу.

---

## Быстрый старт (копируй и вставляй)

### 1. Открой терминал и перейди в папку проекта

```bash
cd /Users/Ilia/ilia-tiushniakov/AzureOpsCrew
```

### 2. Запусти Backend

```bash
# Удалить старую БД (чистый старт с seed-данными)
rm -f backend/src/Api/azureopscrew.db

# Загрузить переменные окружения из .env (безопасный парсинг — не ломает ~ и спецсимволы)
while IFS='=' read -r key value; do
  [[ "$key" =~ ^[[:space:]]*# ]] && continue
  [[ -z "$key" ]] && continue
  key=$(echo "$key" | xargs)
  [ -z "$key" ] && continue
  export "$key=$value"
done < .env

# Прокинуть переменные в формате .NET
export Jwt__SigningKey="$JWT_SIGNING_KEY"
export OpenAI__ApiKey="$OPENAI_API_KEY"
export Mcp__Azure__ServerUrl="$MCP_AZURE_URL"
export Mcp__Azure__TenantId="$MCP_AZURE_TENANT_ID"
export Mcp__Azure__ClientId="$MCP_AZURE_CLIENT_ID"
export Mcp__Azure__ClientSecret="$MCP_AZURE_CLIENT_SECRET"
export Mcp__Azure__TokenUrl="$MCP_AZURE_TOKEN_URL"
export Mcp__Azure__Scope="$MCP_AZURE_SCOPE"
export Mcp__AzureDevOps__ServerUrl="$MCP_ADO_URL"
export Mcp__AzureDevOps__TenantId="$MCP_ADO_TENANT_ID"
export Mcp__AzureDevOps__ClientId="$MCP_ADO_CLIENT_ID"
export Mcp__AzureDevOps__ClientSecret="$MCP_ADO_CLIENT_SECRET"
export Mcp__AzureDevOps__TokenUrl="$MCP_ADO_TOKEN_URL"
export Mcp__AzureDevOps__Scope="$MCP_ADO_SCOPE"
export Mcp__Platform__ServerUrl="$MCP_PLATFORM_URL"
export Mcp__Platform__TenantId="$MCP_PLATFORM_TENANT_ID"
export Mcp__Platform__ClientId="$MCP_PLATFORM_CLIENT_ID"
export Mcp__Platform__ClientSecret="$MCP_PLATFORM_CLIENT_SECRET"
export Mcp__Platform__TokenUrl="$MCP_PLATFORM_TOKEN_URL"
export Mcp__Platform__Scope="$MCP_PLATFORM_SCOPE"
export Mcp__GitOps__ServerUrl="$MCP_GITOPS_URL"
export Mcp__GitOps__TenantId="$MCP_GITOPS_TENANT_ID"
export Mcp__GitOps__ClientId="$MCP_GITOPS_CLIENT_ID"
export Mcp__GitOps__ClientSecret="$MCP_GITOPS_CLIENT_SECRET"
export Mcp__GitOps__TokenUrl="$MCP_GITOPS_TOKEN_URL"
export Mcp__GitOps__Scope="$MCP_GITOPS_SCOPE"
export Seeding__IsEnabled=true

# Запустить backend на порту 42100
DOTNET_ROOT=/tmp/dotnet10 /tmp/dotnet10/dotnet run --project backend/src/Api --urls="http://localhost:42100"
```

Backend выведет логи в этот терминал. **Не закрывай его** — если закроешь, backend остановится.

### 3. Запусти Frontend (в НОВОМ терминале)

```bash
cd /Users/Ilia/ilia-tiushniakov/AzureOpsCrew/frontend
pnpm dev -p 3001
```

### 4. Открой в браузере

```
http://localhost:3001
```

---

## Остановка

### Остановить Backend
В терминале где он работает нажми **Ctrl+C**.

### Остановить Frontend
В терминале где он работает нажми **Ctrl+C**.

### Если терминал уже закрыт (процессы висят в фоне)

```bash
# Убить backend
lsof -ti:42100 | xargs kill -9 2>/dev/null

# Убить frontend
pkill -f "next dev" 2>/dev/null

# Проверить что порты свободны
lsof -nP -iTCP:42100 -iTCP:3001 | grep LISTEN
# (пустой вывод = всё чисто)
```

---

## Перезапуск

```bash
# 1. Остановить всё
lsof -ti:42100 | xargs kill -9 2>/dev/null
pkill -f "next dev" 2>/dev/null
sleep 2

# 2. Запустить заново (см. шаги 2 и 3 выше)
```

### Чистый перезапуск (со сбросом БД)

Добавь перед запуском backend:
```bash
rm -f backend/src/Api/azureopscrew.db
```
Это удалит SQLite-базу. При старте она будет создана заново с seed-данными (3 агента, 1 канал).

---

## Проверка что всё работает

```bash
# Backend жив?
curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:42100/api/auth/auto-login
# Ожидаемый ответ: 200

# Frontend жив?
curl -s -o /dev/null -w "%{http_code}" http://localhost:3001
# Ожидаемый ответ: 200

# Проверить агентов и каналы
TOKEN=$(curl -s -X POST http://localhost:42100/api/auth/auto-login | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")

curl -s -H "Authorization: Bearer $TOKEN" http://localhost:42100/api/agents | python3 -m json.tool
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:42100/api/channels | python3 -m json.tool
```

---

## Сборка (без запуска)

```bash
cd /Users/Ilia/ilia-tiushniakov/AzureOpsCrew

# Собрать backend
DOTNET_ROOT=/tmp/dotnet10 /tmp/dotnet10/dotnet build backend/src/Api

# Собрать frontend
cd frontend && pnpm build
```

---

## Тесты

```bash
cd /Users/Ilia/ilia-tiushniakov/AzureOpsCrew
DOTNET_ROOT=/tmp/dotnet10 /tmp/dotnet10/dotnet test backend/AzureOpsCrew.slnx
# Ожидаемый результат: 39 passed, 0 failed
```

---

## Переустановка .NET 10

Если после перезагрузки Mac `/tmp/dotnet10/dotnet` пропал:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /tmp/dotnet10
/tmp/dotnet10/dotnet --version
# Должно показать 10.0.103 или новее
```

---

## Структура проекта (кратко)

```
AzureOpsCrew/
├── .env                  ← все секреты и ключи (НЕ коммитить!)
├── backend/
│   └── src/Api/          ← .NET backend (порт 42100)
├── frontend/             ← Next.js frontend (порт 3001)
└── RUN_GUIDE.md          ← этот файл
```

## Порты

| Сервис | Порт | URL |
|--------|------|-----|
| Backend API | 42100 | http://localhost:42100 |
| Frontend | 3001 | http://localhost:3001 |

## Агенты

| Агент | ID | Роль |
|-------|----|------|
| Manager | manager | Координатор — принимает задачи, маршрутизирует, утверждает |
| DevOps | devops | Инфраструктура — Azure, Platform, мониторинг |
| Developer | developer | Код — ADO репозитории, пайплайны, GitOps |

## Скрипт для ленивых (всё одной командой)

В корне проекта уже есть готовые скрипты `start.sh` и `stop.sh`:

```bash
chmod +x start.sh stop.sh
./start.sh        # запуск всего
./stop.sh         # остановка всего
```

Что делает `start.sh`:
1. Убивает старые процессы на портах 42100 и 3001
2. Проверяет/устанавливает .NET 10 SDK
3. Безопасно загружает `.env` (не ломает значения с `~`)
4. Маппит переменные в формат .NET (`Mcp__Azure__*` и т.д.)
5. Проверяет JWT ключ (≥32 символов)
6. Запускает backend, ждёт health-check
7. Запускает frontend
8. Ctrl+C останавливает оба процесса
