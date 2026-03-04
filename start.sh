#!/bin/bash
cd "$(dirname "$0")"

# ─── 1. Остановить старое ───────────────────────────────────
echo "🛑 Останавливаю старые процессы..."
lsof -ti:42100 | xargs kill -9 2>/dev/null || true
pkill -f "next dev" 2>/dev/null || true
sleep 2

# ─── 2. Проверить .NET 10 ───────────────────────────────────
if [ ! -x /tmp/dotnet10/dotnet ]; then
  echo "📦 .NET 10 не найден, устанавливаю..."
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /tmp/dotnet10
fi
DOTNET_VER=$(/tmp/dotnet10/dotnet --version 2>/dev/null || echo "")
if [ -z "$DOTNET_VER" ]; then
  echo "❌ Не удалось запустить dotnet. Попробуй: curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /tmp/dotnet10"
  exit 1
fi
echo "✅ .NET SDK: $DOTNET_VER"

# ─── 3. Проверить .env ──────────────────────────────────────
if [ ! -f .env ]; then
  echo "❌ Файл .env не найден в $(pwd)"
  exit 1
fi

# ─── 4. Загрузить .env (безопасно, без раскрытия ~) ─────────
while IFS='=' read -r key value; do
  # Пропустить комментарии и пустые строки
  [[ "$key" =~ ^[[:space:]]*# ]] && continue
  [[ -z "$key" ]] && continue
  # Убрать пробелы вокруг ключа
  key=$(echo "$key" | xargs)
  [ -z "$key" ] && continue
  export "$key=$value"
done < .env

# ─── 5. Маппинг в .NET формат (Секция__Ключ) ────────────────
export Jwt__SigningKey="$JWT_SIGNING_KEY"
export OpenAI__ApiKey="$OPENAI_API_KEY"
export Anthropic__ApiKey="$ANTHROPIC_API_KEY"

export Mcp__Azure__ServerUrl="$MCP_AZURE_URL"
export Mcp__Azure__TenantId="$MCP_AZURE_TENANT_ID"
export Mcp__Azure__ClientId="$MCP_AZURE_CLIENT_ID"
export Mcp__Azure__ClientSecret="$MCP_AZURE_CLIENT_SECRET"
export Mcp__Azure__TokenUrl="$MCP_AZURE_TOKEN_URL"
export Mcp__Azure__Scope="$MCP_AZURE_SCOPE"
export Mcp__Azure__SubscriptionId="$MCP_AZURE_SUBSCRIPTION_ID"

# MCP tool runtime parameters (for InferToolParameters)
export MCP_AZURE_SUBSCRIPTION_ID="$MCP_AZURE_SUBSCRIPTION_ID"
export MCP_ADO_ORGANIZATION="$MCP_ADO_ORGANIZATION"
export MCP_ADO_PROJECT="$MCP_ADO_PROJECT"
export MCP_GITOPS_REPO="$MCP_GITOPS_REPO"

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

if [ -n "$SEEDING_ENABLED" ]; then
  export Seeding__IsEnabled="$SEEDING_ENABLED"
else
  export Seeding__IsEnabled=true
fi
export DOTNET_ROOT=/tmp/dotnet10

# Проверить что JWT ключ загрузился
if [ ${#Jwt__SigningKey} -lt 32 ]; then
  echo "❌ JWT_SIGNING_KEY пуст или короче 32 символов. Проверь .env"
  exit 1
fi
echo "✅ .env загружен (JWT: ${#Jwt__SigningKey} символов)"

# ─── 6. Чистый старт БД ─────────────────────────────────────
rm -f backend/src/Api/azureopscrew.db

# ─── 7. Запуск backend ──────────────────────────────────────
echo "🚀 Запускаю backend..."
/tmp/dotnet10/dotnet run --project backend/src/Api --urls="http://localhost:42100" &
BACKEND_PID=$!

echo "⏳ Жду backend..."
READY=0
for i in $(seq 1 30); do
  sleep 2
  if curl -s -o /dev/null -w "" -X POST http://localhost:42100/api/auth/auto-login 2>/dev/null; then
    READY=1
    break
  fi
  # Проверить что процесс ещё живой
  if ! kill -0 $BACKEND_PID 2>/dev/null; then
    echo "❌ Backend упал при запуске. Смотри логи выше."
    exit 1
  fi
done

if [ $READY -eq 0 ]; then
  echo "❌ Backend не ответил за 60 секунд"
  kill $BACKEND_PID 2>/dev/null
  exit 1
fi
echo "✅ Backend запущен (PID: $BACKEND_PID)"

# ─── 8. Запуск frontend ─────────────────────────────────────
echo "🚀 Запускаю frontend..."
cd frontend
pnpm dev -p 3001 &
FRONTEND_PID=$!
cd ..

sleep 5
echo "✅ Frontend запущен (PID: $FRONTEND_PID)"
echo ""
echo "=========================================="
echo "  🌐 Открой: http://localhost:3001"
echo "=========================================="
echo ""
echo "Для остановки нажми Ctrl+C"

# ─── Ctrl+C handler ─────────────────────────────────────────
trap "echo ''; echo '🛑 Останавливаю...'; kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit 0" INT TERM

wait
