# Порядок реализации (Implementation Order)

Пошаговый план: что делать первым, что вторым и т.д.

---

## Фаза 0: Подготовка (30 мин)

### Шаг 0.1: Настройка окружения
- [ ] Убедиться что .NET 10 SDK установлен (`dotnet --version`)
- [ ] Убедиться что Node.js ≥ 18 и pnpm установлены
- [ ] Убедиться что Docker Desktop запущен
- [ ] Создать `.env` файл в корне проекта

### Шаг 0.2: Получить ключи
- [ ] OpenAI API key → в `.env` как `OPENAI_API_KEY`
- [ ] Azure MCP Server URL и ключ (когда будут готовы)

---

## Фаза 1: Упрощение бэкенда (2-3 часа)

### Шаг 1.1: Очистка от ненужного кода
**Удалить файлы/папки:**
```
backend/src/Api/Email/               ← вся папка
backend/src/Api/Auth/                ← заменим на простую авто-логин
backend/src/Api/Endpoints/ProvidersEndpoints.cs  ← не нужен CRUD
backend/src/Api/Endpoints/UsersEndpoints.cs      ← упрощаем
```

**Удалить NuGet пакеты из Api.csproj:**
```xml
<!-- Удалить: -->
Brevo.API
MailKit
MimeKit
<!-- Они для email верификации, не нужны -->
```

### Шаг 1.2: Новые Settings классы
**Создать:**
- `backend/src/Api/Settings/OpenAISettings.cs` — для OpenAI ключа и модели
- `backend/src/Api/Settings/McpSettings.cs` — для MCP сервера

### Шаг 1.3: Упрощение Program.cs
- Убрать регистрацию email сервисов
- Убрать сложную JWT конфигурацию → оставить простой JWT
- Добавить binding для OpenAISettings и McpSettings из конфигурации
- Обновить appsettings.json / appsettings.Development.json

### Шаг 1.4: Авто-логин
**Переписать `AuthEndpoints.cs`:**
- Один endpoint: `POST /api/auth/auto-login`
- Находит/создаёт пользователя "Demo User"
- Возвращает JWT токен сразу
- Нет пароля, нет email верификации

### Шаг 1.5: Обновить Seeder.cs
- Seed одного Provider (OpenAI, gpt-4o-mini)
- Seed трёх агентов с детальными prompts:
  - **Manager** (жёлтый) — оркестратор
  - **DevOps** (зелёный) — мониторинг, пайплайны
  - **Developer** (синий) — код, PR, дебаг  
- Seed одного канала "Ops Room" со всеми тремя агентами
- Seed демо-пользователя

### Шаг 1.6: Протестировать бэкенд
```bash
cd backend/src/Api
dotnet run
# Проверить: GET http://localhost:5063/api/agents
# Должен вернуть 3 агента
```

---

## Фаза 2: MCP Tools (1-2 часа)

### Шаг 2.1: McpToolProvider
**Создать `backend/src/Api/Extensions/McpExtensions.cs`:**
- Класс `McpToolProvider`
- Метод `GetToolsForAgent(string role)` → возвращает список `AITool`
- Mock-реализация для начала (реальный MCP подключим позже)
- Каждый tool — это `AIFunction` с описанием и параметрами

### Шаг 2.2: Интегрировать tools в агентов
**Обновить `AiAgentFactory.cs`:**
- При создании агента, брать tools из `McpToolProvider`
- Передавать в `ChatOptions.Tools`
- DevOps получает tools мониторинга
- Developer получает tools для кода
- Manager не получает MCP tools

### Шаг 2.3: Протестировать tool calling
```bash
# POST /api/channels/{id}/agui
# Тело: { messages: [{ content: "Какие ошибки в App Insights?" }] }
# DevOps агент должен вызвать tool и вернуть результат
```

---

## Фаза 3: Новый Chat Endpoint (1-2 часа)

### Шаг 3.1: CrewChatEndpoint (или обновить существующий AgUiEndpoints)
Ключевое изменение: **GroupChat должен работать как дискуссия**:
1. User пишет сообщение
2. Manager видит сообщение и решает кому делегировать
3. DevOps/Developer вызывают tools и отвечают
4. Manager суммирует и спрашивает user confirmation
5. User подтверждает → агенты действуют

**Обновить `AgUiEndpoints.cs`:**
- Канальный endpoint уже использует `RoundRobinGroupChatManager`
- Заменить на smarter selection (или оставить RoundRobin для простоты)
- Увеличить `MaximumIterationCount` для полной дискуссии
- Добавить human-in-the-loop через специальные сообщения

### Шаг 3.2: Протестировать multi-agent chat
```
User: "Проверь здоровье продакшена"
→ Manager: "Ок, попрошу DevOps проверить. @DevOps, проверь статус ресурсов."
→ DevOps: [calls resource_health tool] "Вижу проблему с db-prod, статус Degraded."
→ Manager: "Похоже проблема с БД. @Developer, можешь проверить логи?"
→ Developer: [calls appinsights_query tool] "Нашёл TimeoutException в PaymentService."
→ Manager: "Итого: БД деградирована, в коде таймауты. Предлагаю рестартнуть app service. Подтверди?"
→ [Human-in-the-loop card: кнопка "Approve restart"]
```

---

## Фаза 4: Фронтенд (2-3 часа)

### Шаг 4.1: Удалить лишнее из фронтенда
**Удалить файлы:**
```
frontend/components/settings/         ← вся папка
frontend/components/direct-messages-* ← все DM компоненты
frontend/components/dm-messages.tsx
frontend/app/login/                   ← вся папка  
frontend/app/signup/                  ← вся папка
frontend/app/api/settings/            ← вся папка
```

### Шаг 4.2: Авто-логин
**Создать/обновить `frontend/app/api/auth/auto/route.ts`:**
- При загрузке страницы → вызвать auto-login endpoint
- Сохранить JWT в cookie
- Redirect в основной интерфейс
- Без формы логина, без страницы регистрации

### Шаг 4.3: Упростить layout
**Обновить `home-page-client.tsx`:**
- Убрать DM секцию
- Убрать Settings
- Оставить: ChannelSidebar (слева) + ChannelArea (центр) + MemberList (справа)
- По умолчанию открывать канал "Ops Room"

### Шаг 4.4: Обновить MemberList
**Обновить/создать `member-list.tsx`:**
- Показывать 3 агентов с их цветами и статусами
- Показывать текущего пользователя
- Индикатор "typing..." когда агент думает

### Шаг 4.5: Обновить MessageList
**`message-list.tsx`** уже хорошо работает — возможно минимальные правки:
- Убедиться что аватары агентов показывают правильные цвета
- Tool calls отображаются как inline блоки

### Шаг 4.6: Human-in-the-loop карточки
**`copilot-actions.tsx`** уже имеет 5 типов карточек — проверить:
- Pipeline Status → кнопки Approve/Reject
- Resource Health → кнопки с действиями
- Deployment → кнопки Deploy/Rollback
- Убедиться что нажатие кнопки отправляет сообщение в чат

### Шаг 4.7: Протестировать фронтенд
```bash
cd frontend
pnpm dev
# Открыть http://localhost:3000
# Должен авто-залогиниться и показать Ops Room
```

---

## Фаза 5: Docker Compose (30 мин)

### Шаг 5.1: Обновить docker-compose.yml
- API контейнер (порт 5063)
- Frontend контейнер (порт 3000)
- Neo4j контейнер (порт 7474, 7687)
- Общая сеть

### Шаг 5.2: Проверить
```bash
docker-compose up --build
# Всё должно подняться и работать
```

---

## Фаза 6: Подключение реального MCP (когда будут данные)

### Шаг 6.1: Заменить mock на реальный MCP
- Обновить `.env` с реальным MCP_SERVER_URL
- Обновить `McpToolProvider` для HTTP вызовов к MCP серверу
- Протестировать каждый tool отдельно

### Шаг 6.2: Подключить реальный OpenAI
- Обновить `.env` с реальным OPENAI_API_KEY
- Протестировать что агенты отвечают адекватно
- Настроить temperature, max_tokens если нужно

---

## Фаза 7: Полировка для демо (1-2 часа)

### Шаг 7.1: Подготовить демо-сценарий
Написать скрипт демо:
1. Открыть приложение → авто-логин
2. Написать "Check production health"
3. Показать как агенты обсуждают
4. Показать tool calls в реальном времени
5. Показать interactive card с кнопкой Approve
6. Нажать Approve → показать результат

### Шаг 7.2: UI/UX мелочи
- Добавить лого "Azure Ops Crew" в хедер
- Убедиться что тёмная тема выглядит как Discord
- Проверить мобильную отзывчивость (не критично для демо)

### Шаг 7.3: Fallback на случай проблем
- Mock MCP ответы всегда доступны
- Заготовленные сообщения если OpenAI недоступен
- Демо пользователь всегда создаётся автоматически

---

## Общая оценка времени

| Фаза | Время | Зависимости |
|------|-------|-------------|
| 0: Подготовка | 30 мин | — |
| 1: Бэкенд | 2-3 часа | Фаза 0 |
| 2: MCP Tools | 1-2 часа | Фаза 1 |
| 3: Chat Endpoint | 1-2 часа | Фаза 2 |
| 4: Фронтенд | 2-3 часа | Фаза 1 (бэкенд API) |
| 5: Docker | 30 мин | Фазы 1-4 |
| 6: Реальный MCP | 1 час | Данные от тебя |
| 7: Полировка | 1-2 часа | Всё остальное |
| **Итого** | **~10-14 часов** | |

> **Важно:** Фазы 1-3 (бэкенд) и Фаза 4 (фронтенд) можно делать параллельно если два человека.
> Я буду помогать по каждому шагу — просто скажи "давай делать фазу X" и я начну писать код.

---

## Чеклист готовности к демо

- [ ] Приложение запускается одной командой (`docker-compose up`)
- [ ] Авто-логин работает без регистрации
- [ ] Канал "Ops Room" открывается по умолчанию
- [ ] 3 агента видны в панели справа
- [ ] User может написать сообщение и получить ответ от агентов
- [ ] Агенты обсуждают между собой (multi-turn)
- [ ] Tool calls видны в чате (DevOps вызывает App Insights и т.д.)
- [ ] Interactive cards появляются для подтверждения действий
- [ ] Кнопки на карточках работают и отправляют сообщения
- [ ] Всё работает минимум 5 минут без падений
