# Техническое задание

## Исправление двух критических дефектов в агентной системе

### Scope: MCP tool invocation reliability + context budget management

---

# 1. Цель

Исправить два независимых, но критических дефекта текущей мультиагентной системы:

## Дефект A — неверный вызов MCP tool для Azure Monitor / Log Analytics

Сейчас агент DevOps не может корректно выполнять запросы к Azure Monitor / Log Analytics через Azure MCP, хотя необходимый функционал существует.

## Дефект B — переполнение контекста модели

Сейчас система отправляет в модель слишком большой payload:

* полную историю сообщений,
* tool definitions,
* tool results,
* repeated orchestration messages,

что приводит к `context_length_exceeded`.

---

# 2. Подтверждённые симптомы

## 2.1. Симптомы дефекта A

Из логов видно:

* DevOps вызывает monitor tool;
* MCP сервер отвечает ошибкой:

  * `Missing Required options: --resource-group, --workspace, --table, --query`
  * `Wrap all command arguments into the root "parameters" argument`

Это означает:

* MCP tool существует;
* MCP endpoint доступен;
* проблема не в RBAC;
* проблема не в отсутствии функционала monitor;
* проблема в формате аргументов при tool invocation.

## 2.2. Симптомы дефекта B

Из логов видно:

* лимит модели: 128000 tokens
* фактически отправлено: 173352 tokens

  * 159811 in messages
  * 13541 in functions

Это означает:

* в систему отсутствует token budget management;
* сообщения не сжимаются;
* старые шаги не суммаризируются;
* tool results не обрезаются;
* набор доступных tools и их schema перегружают prompt.

---

# 3. Корневые причины

## 3.1. Root cause A — Tool argument contract failure

На стыке LLM → tool layer → MCP server отсутствует надёжная нормализация и валидация аргументов.

Наиболее вероятно:

* модель генерирует `params`,
* MCP сервер ожидает `parameters`,
* код передаёт аргументы “как есть” без schema-aware remapping.

Также отсутствует:

* pre-validation аргументов перед отправкой;
* auto-retry с исправлением типовых format errors;
* инструментально жёсткий wrapper для log query.

## 3.2. Root cause B — отсутствие context management

В системе отсутствуют:

* token counting;
* message pruning;
* sliding window;
* summarization;
* tool result truncation;
* task-aware tool filtering;
* session-based server-side state compaction.

---

# 4. Что нужно исправить

---

## 4.1. Исправление A — Надёжный вызов MCP tools

### Обязательная цель

Сделать так, чтобы вызовы MCP tools были:

* schema-aware,
* валидируемыми,
* автоматически восстанавливаемыми при типовых ошибках формата.

### Что нужно реализовать

#### 4.1.1. Tool argument normalization layer

Перед отправкой tool call в MCP нужно внедрить нормализацию аргументов.

Минимум:

* если schema ожидает `parameters`, а модель прислала `params`, автоматически remap;
* если обязательные command arguments вложены не туда, привести к ожидаемой структуре;
* если типы полей совместимы, но ключи названы не так, делать safe normalization.

#### 4.1.2. Schema-aware validation before MCP call

Перед HTTP POST в MCP:

* валидировать args против schema инструмента;
* проверять наличие обязательных полей;
* проверять корневой формат payload;
* при невалидности не слать сырой запрос в MCP без попытки исправления.

#### 4.1.3. Retry with auto-repair

Если MCP возвращает типовую ошибку вида:

* missing required options
* wrap arguments into root parameters
* invalid command payload shape

система должна:

1. распознать тип ошибки;
2. применить known repair strategy;
3. выполнить retry автоматически;
4. только после неуспеха отдавать ошибку агенту.

#### 4.1.4. Stronger tool description / schema presentation

Улучшить описание tools, которые используют generic command wrappers, так чтобы модель явно видела:

* required top-level fields;
* что нужно использовать `parameters`, а не `params`;
* примеры корректного payload shape.

#### 4.1.5. Optional: typed wrapper for high-frequency operations

Если архитектурно возможно без сильного refactor, добавить typed wrapper для самых частых monitor сценариев, например:

* query Log Analytics workspace
* get logs by workspace/query
* get recent errors

Цель:
снизить вероятность того, что модель будет собирать generic command payload вручную.

---

## 4.2. Исправление B — Token budget management

### Обязательная цель

Сделать так, чтобы даже длинные мультиагентные runs не выходили за лимит модели.

### Что нужно реализовать

#### 4.2.1. Token estimator before LLM call

Перед каждым вызовом модели внедрить оценку размера:

* messages
* tool schemas/functions
* expected response budget

Если текущий payload превышает целевой бюджет — применять compaction strategy.

#### 4.2.2. Sliding window for messages

Перестать слать всю историю целиком.
Нужно хранить:

* system messages,
* краткий summary старого контекста,
* последние релевантные сообщения,
* только нужные tool outcomes.

#### 4.2.3. Conversation summarization

Внедрить summary layer:

* после каждого раунда или при достижении budget threshold;
* заменять старые подробные сообщения кратким structured summary;
* summary должен содержать:

  * goal,
  * completed steps,
  * findings,
  * active hypotheses,
  * handoffs,
  * pending actions.

#### 4.2.4. Tool result truncation / compaction

Не сохранять в chat history большие сырые tool results целиком.

Вместо этого:

* сохранять краткий summary;
* сохранять artifact reference / internal storage ref;
* в messages включать только короткую полезную выдержку.

#### 4.2.5. Tool filtering by agent and task

Не отправлять всем агентам все доступные tools.
Нужно фильтровать tools:

* по роли агента;
* по типу текущей задачи;
* по текущей фазе run.

Примеры:

* Manager получает только supervisory/read subset;
* DevOps получает только infra-relevant tools;
* Developer получает только code/ADO tools;
* если задача про Log Analytics, не надо тащить лишние GitOps tools.

#### 4.2.6. Reduce redundant orchestration chatter

Manager не должен повторно публиковать один и тот же triage/plan без новых данных.
Нужно:

* сокращать повторяющиеся manager messages;
* не дублировать одинаковые делегации;
* использовать state update вместо verbose restatement.

#### 4.2.7. Prefer server-side conversation state

Если текущая архитектура это позволяет, не отправлять с фронтенда полный `messages[]` массив на каждый шаг.
Предпочтительно:

* session/thread state хранить на backend;
* с фронта передавать только новый user input + session id;
* backend сам восстанавливает компактный state.

---

# 5. Что НЕ нужно делать

* Не переписывать всю систему с нуля.
* Не менять MCP серверы без необходимости.
* Не чинить сейчас все prompt/role проблемы мира — только то, что влияет на эти два дефекта.
* Не добавлять новые MCP сервера в рамках этой задачи.
* Не расширять scope на общую переработку всей orchestration architecture.

---

# 6. Конкретные deliverables

Нужно выдать:

1. исправленный MCP tool invocation layer;
2. нормализацию args для generic MCP commands;
3. retry-with-repair для типовых format errors;
4. token budget manager;
5. summarization / compaction strategy;
6. tool filtering policy;
7. защиту от повторяющегося orchestration spam;
8. тесты;
9. краткую документацию:

   * как теперь обрабатываются tool args,
   * как теперь работает context compaction.

---

# 7. Обязательные тестовые сценарии

## 7.1. MCP formatting scenario

Сценарий:
агент вызывает monitor/log query tool с неверной формой аргументов (`params` вместо `parameters`).

Ожидается:

* система автоматически нормализует вызов;
* запрос успешно выполняется;
* агент получает результат без ручного вмешательства пользователя.

## 7.2. Missing required options scenario

Сценарий:
MCP возвращает типовую ошибку про missing required options.

Ожидается:

* система распознаёт known format failure;
* делает retry после repair;
* не просит пользователя то, что уже известно из контекста.

## 7.3. Long conversation scenario

Сценарий:
длинная мультиагентная сессия с несколькими tool calls и handoffs.

Ожидается:

* payload не превышает model limit;
* старый контекст компактно суммаризируется;
* run продолжается без `context_length_exceeded`.

## 7.4. Large tool result scenario

Сценарий:
MCP tool возвращает большой JSON/result.

Ожидается:

* в chat history сохраняется compact summary;
* полный результат уходит в artifact/internal storage;
* контекст не раздувается.

## 7.5. Tool filtering scenario

Сценарий:
Developer агент получает task по коду.

Ожидается:

* ему не подсовываются лишние Azure/infra tools;
* tool schema budget уменьшается.

---

# 8. Definition of Done

Задача считается выполненной только если:

1. monitor/log query tool больше не ломается на `params` vs `parameters`;
2. типовые MCP format errors автоматически чинятся или корректно переформатируются;
3. агент может реально выполнить запрос в Log Analytics без ручной передачи уже известных аргументов;
4. система перестаёт выходить за context limit на типовых длинных runs;
5. внедрены token budgeting, summarization и truncation;
6. tool schemas и message history больше не раздувают prompt неконтролируемо;
7. повторяющиеся manager/delegation loops сокращены;
8. тестовые сценарии проходят.

---

# 9. Порядок работы агента

Работай поэтапно.

## Этап 1 — Audit

Найди и проанализируй:

* место формирования tool schemas;
* место сериализации tool args;
* место MCP HTTP invocation;
* retry/error handling;
* место сборки messages для LLM;
* место добавления tool results в историю;
* место выбора tools по агентам.

## Этап 2 — Fix MCP invocation reliability

Реализуй:

* normalization
* validation
* retry-with-repair
* improved diagnostics

## Этап 3 — Fix context management

Реализуй:

* token estimator
* sliding window
* summarization
* tool result compaction
* tool filtering

## Этап 4 — Validation

Прогони сценарии и покажи:

* до/после;
* что именно исправлено;
* какой лимит по токенам теперь удерживается;
* что теперь происходит при format failures MCP tools.

---

# 10. Формат отчёта после каждого этапа

После каждого этапа верни:

* что найдено;
* какие файлы изменены;
* что именно исправлено;
* как это протестировано;
* какие риски/ограничения остались.
