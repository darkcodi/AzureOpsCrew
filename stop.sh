#!/bin/bash
cd "$(dirname "$0")"

echo "🛑 Останавливаю все процессы..."

# Backend (dotnet на порту 42100)
lsof -ti:42100 | xargs kill -9 2>/dev/null || true

# Frontend (next dev на порту 3001)
lsof -ti:3001 | xargs kill -9 2>/dev/null || true
pkill -f "next dev" 2>/dev/null || true

sleep 2

# Проверка
REMAINING=$(lsof -nP -iTCP:42100 -iTCP:3001 2>/dev/null | grep LISTEN | wc -l | xargs)
if [ "$REMAINING" = "0" ]; then
  echo "✅ Всё остановлено. Порты 42100 и 3001 свободны."
else
  echo "⚠️  Некоторые процессы ещё работают:"
  lsof -nP -iTCP:42100 -iTCP:3001 | grep LISTEN
fi
