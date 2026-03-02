# Docker, .env и конфигурация

## 1. `.env` файл (НОВЫЙ — корень проекта)

Создать `/AzureOpsCrew/.env`:

```env
# ============================================
# AzureOpsCrew 2.0 — Configuration
# ============================================

# ─── OpenAI ───
OPENAI_API_KEY=sk-...your-key...
OPENAI_MODEL=gpt-4o-mini

# ─── Azure MCP Server ───
MCP_SERVER_URL=https://your-mcp-server.azurewebsites.net
MCP_API_KEY=

# ─── JWT (для demo — можно оставить дефолтные) ───
JWT_SIGNING_KEY=AzureOpsCrewHackathonDemoKey2026!!
JWT_ISSUER=AzureOpsCrew
JWT_AUDIENCE=AzureOpsCrewFrontend

# ─── Neo4j (Long-Term Memory) ───
NEO4J_PASSWORD=azureopscrew

# ─── Seeding ───
SEEDING_ENABLED=true
```

## 2. `.env.example` (для коммита в git)

```env
OPENAI_API_KEY=
OPENAI_MODEL=gpt-4o-mini
MCP_SERVER_URL=
MCP_API_KEY=
JWT_SIGNING_KEY=AzureOpsCrewHackathonDemoKey2026!!
JWT_ISSUER=AzureOpsCrew
JWT_AUDIENCE=AzureOpsCrewFrontend
NEO4J_PASSWORD=azureopscrew
SEEDING_ENABLED=true
```

## 3. `docker-compose.yml` (ОБНОВЛЁННЫЙ)

```yaml
services:
  # ─── .NET Backend API ───
  api:
    image: ${DOCKER_REGISTRY-}azureopscrew-api
    build:
      context: ./backend
      dockerfile: src/Api/Dockerfile
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_HTTP_PORTS=80
      - ASPNETCORE_URLS=http://*:80
      # DB
      - DatabaseProvider=Sqlite
      - Sqlite__DataSource=Data Source=/app/data/azureopscrew.db
      # JWT
      - Jwt__Issuer=${JWT_ISSUER:-AzureOpsCrew}
      - Jwt__Audience=${JWT_AUDIENCE:-AzureOpsCrewFrontend}
      - Jwt__SigningKey=${JWT_SIGNING_KEY:-AzureOpsCrewHackathonDemoKey2026!!}
      # OpenAI
      - OpenAI__ApiKey=${OPENAI_API_KEY}
      - OpenAI__Model=${OPENAI_MODEL:-gpt-4o-mini}
      # MCP
      - Mcp__ServerUrl=${MCP_SERVER_URL:-}
      - Mcp__ApiKey=${MCP_API_KEY:-}
      # Long-Term Memory
      - LongTermMemory__Type=Cypher
      - LongTermMemory__Neo4j__Uri=bolt://neo4j:7687
      - LongTermMemory__Neo4j__Username=neo4j
      - LongTermMemory__Neo4j__Password=${NEO4J_PASSWORD:-azureopscrew}
      # Seeding
      - Seeding__IsEnabled=${SEEDING_ENABLED:-true}
    volumes:
      - ./backend/data:/app/data
    ports:
      - "42100:80"
    restart: on-failure
    depends_on:
      neo4j:
        condition: service_healthy

  # ─── Next.js Frontend ───
  frontend:
    image: ${DOCKER_REGISTRY-}azureopscrew-frontend
    build:
      context: ./frontend
      dockerfile: Dockerfile
    environment:
      - BACKEND_API_URL=http://api:80
      - NODE_ENV=production
    ports:
      - "3000:3000"
    restart: on-failure
    depends_on:
      - api

  # ─── Neo4j (Long-Term Memory) ───
  neo4j:
    image: neo4j:5
    container_name: neo4j
    environment:
      - NEO4J_AUTH=neo4j/${NEO4J_PASSWORD:-azureopscrew}
    ports:
      - "7474:7474"   # Web UI
      - "7687:7687"   # Bolt
    volumes:
      - neo4j_data:/data
    healthcheck:
      test: wget -qO - http://localhost:7474 || exit 1
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s
    restart: unless-stopped

volumes:
  neo4j_data:
```

### Что поменялось:
1. **Убрали** все Brevo/Email переменные
2. **Убрали** Azure Foundry seed variables
3. **Добавили** `OpenAI__ApiKey`, `OpenAI__Model`
4. **Добавили** `Mcp__ServerUrl`, `Mcp__ApiKey`
5. **Добавили** `frontend` сервис
6. **Упростили** JWT (фиксированный ключ для демо)

## 4. Frontend Dockerfile (проверить)

Текущий Dockerfile фронтенда должен работать. Убедиться что:
- `BACKEND_API_URL` передаётся как env variable
- Next.js использует его в runtime (не только build time)

```dockerfile
FROM node:22-alpine AS base
WORKDIR /app

# Install pnpm
RUN corepack enable && corepack prepare pnpm@10.9.0 --activate

# Install dependencies
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

# Build
COPY . .
RUN pnpm build

# Production
FROM node:22-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production

COPY --from=base /app/.next/standalone ./
COPY --from=base /app/.next/static ./.next/static
COPY --from=base /app/public ./public

EXPOSE 3000
CMD ["node", "server.js"]
```

> **Важно**: `BACKEND_API_URL` используется в API routes (server-side), поэтому должен быть доступен через env variable в runtime, не через `NEXT_PUBLIC_`.

## 5. Запуск для разработки (без Docker)

### Backend:
```bash
cd backend/src/Api
# Создать .env или user-secrets
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "Jwt:SigningKey" "AzureOpsCrewHackathonDemoKey2026!!"
dotnet user-secrets set "Mcp:ServerUrl" "https://..."
dotnet run
```

### Frontend:
```bash
cd frontend
# Создать .env.local
echo "BACKEND_API_URL=http://localhost:42100" > .env.local
pnpm dev
```

### Neo4j (если нужен):
```bash
docker run -d \
  --name neo4j \
  -p 7474:7474 -p 7687:7687 \
  -e NEO4J_AUTH=neo4j/azureopscrew \
  neo4j:5
```

Или для InMemory памяти (без Neo4j):
```bash
# В appsettings.json / env:
LongTermMemory__Type=InMemory
```

## 6. Порты

| Сервис | Порт | URL |
|--------|------|-----|
| Backend API | 42100 | http://localhost:42100 |
| Frontend | 3000 | http://localhost:3000 |
| Neo4j Web | 7474 | http://localhost:7474 |
| Neo4j Bolt | 7687 | bolt://localhost:7687 |
