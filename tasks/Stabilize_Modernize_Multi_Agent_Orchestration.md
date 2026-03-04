ТЗ: Stabilize & Modernize Multi-Agent Orchestration (AzureOpsCrew)
0) Контекст и проблема

Сейчас система ломается из-за того, что критические вещи завязаны на “свободный текст”:

Делегирование хрупкое (Manager обязан буквальным текстом упомянуть имя агента).

Worker может “болтать” без tool calls (нет enforcement).

“Используй оба MCP” — пожелание, не контракт.

Tool output режется (MaxToolResultChars=8000) и нет пагинации → инвентарь всегда неполный.

Терминация (MaxConsecutiveNonToolTurns) завершает run, но не чинит причину (нет recovery).

1) Цели
Основные

Надёжная оркестрация: если задача требует tools — worker не может ответить без tool calls.

Надёжная инвентаризация Azure: “List all resources” возвращает “all”, без потерь от truncation.

Структурированное делегирование: больше никаких Contains("DevOps") для запуска worker’а.

Direct addressing: пользователь может писать @DevOps/@Developer напрямую, без Manager.

Подзадачи: агенты могут создавать subtask другим агентам по протоколу (после стабилизации).

Метрики успеха (SLO)

inventory_tasks_completed_with_both_sources ≥ 95%

runs_terminated_due_to_non_tool_turns → стремится к 0 (после ввода recovery)

avg_retries_due_to_missing_tools < 1 (в норме)

“List all resources” всегда возвращает полный count + artifactId (если список большой)

2) Non-Goals (пока не делаем)

Автодеплой/автозалив без approval.

Автоматическое исправление infra без gating.

Полный RAG/долговременная память (можно позже).

3) Архитектурные изменения
3.1 Вводим “контракт задач” (ExecutionTask Contract)

Каждая задача, выдаваемая воркеру, должна иметь:

intent (строка/enum): azure_inventory, ado_pipeline_status, code_fix, …

requires_tools (bool)

required_tools (string[])

definition_of_done (строка)

evidence_requirements (например: “artifact required if payload > threshold”)

Это можно хранить:

либо в DB как часть ExecutionTask / RunContext,

либо хотя бы в памяти на run + логировать.

3.2 Структурированное делегирование вместо text parsing
Новое правило

Manager делегирует задачи только через структурированное действие, а не через упоминание имён.

Реализация (рекомендуется)

Ввести внутренний “оркестраторский tool” (не MCP):

orchestrator_delegate_tasks

Payload:

{
  "tasks": [
    {
      "assignee": "DevOps",
      "intent": "azure_inventory",
      "goal": "List ALL Azure resources across subscription",
      "requires_tools": true,
      "required_tools": ["platform_arg_query_resources", "azure_list_resources"],
      "definition_of_done": "Merged inventory; counts; no missing pages; if large -> stored as artifact"
    }
  ]
}

Оркестратор:

принимает этот function-call,

кладёт задачи в очередь (delegationQueue) уже не по тексту, а по payload.

Fallback на время миграции:

если Manager не сделал delegate_tasks, можно 1 раз retry Manager’а системой: “Ты обязан делегировать через delegate_tasks”.

только если и это не сработало — старый текстовый fallback (временно, под feature flag).

3.3 Enforce: “no tools → response rejected → retry”

Если task requires_tools=true:

после ответа worker’а проверяем, что в turn есть FunctionCallContent (и/или были tool calls с нужными именами).

если нет — ответ не засчитываем, увеличиваем счётчик missingToolAttempts, добавляем системное сообщение и повторяем ход worker’а.

после MaxMissingToolRetries:

помечаем task как failed,

возвращаем Manager’у structured failure artifact.

Важно: это должно работать на уровне кода, а не промпта.

3.4 Композитный “инвентарь” как tool (fan-out на два MCP)

Ввести внутренний tool:

inventory_list_all_resources

Он сам:

вызывает Platform MCP (platform_arg_query_resources) с пагинацией

вызывает Azure MCP (azure_list_resources) (минимум для cross-check)

мерджит по resourceId (или другому canonical key)

сохраняет полный JSON в Artifact (если большой)

возвращает:

summary (counts by type/rg/subscription)

artifactId

sourceCoverage: какие источники были использованы

Так вы убираете “надежду на промпт” и делаете поведение системным.

3.5 Pagination + artifact-first для больших ответов
Pagination

Добавить слой, который умеет:

распознавать nextLink, skipToken, continuationToken, @odata.nextLink, hasMore, …

повторять вызов tool’а с нужными args

агрегировать результаты в единый массив

Artifact-first

Если tool_result_chars > ToolInlineThresholdChars:

весь результат сохраняем в Artifact.Content

в чат кладём:

краткий summary

artifactId

“первые N строк/первые M элементов” (опционально)

Ввести tool:

artifact_fetch(artifactId, offset, limit, format)
чтобы пользователь/агент мог постранично доставать данные без переполнения контекста.

3.6 Verification step (Copilot-like pattern)

Каждый task должен иметь финальный шаг verify:

для azure_inventory: сверить, что:

использованы оба источника,

пагинация завершена,

merged count >= max(count(sourceA), count(sourceB)) (или чёткая логика),

нет truncation-only результата без artifact.

Только после verify task считается “complete”.

3.7 Direct addressing: @DevOps, @Developer, @Manager
Требование

Если пользователь пишет:

@DevOps ... → выполнить direct run с DevOps агентом, без участия Manager.

@Developer ... → direct run с Developer.

@Manager ... → direct run с Manager (координация).

Роутинг

В AgUiEndpoints (или входном middleware):

распознать ^@(\w+) или ^/(devops|developer|manager)

снять tag из текста, записать AddressedTo

выбрать workflow:

direct single-agent mode

или groupchat mode

История остаётся в том же channel, но сообщения помечаются метаданными AddressedTo.

3.8 Subtasks: agents can create tasks for each other

Вводим внутренний tool:

orchestrator_create_subtask

Payload:

{
  "assignee": "Developer",
  "intent": "code_fix",
  "goal": "Implement pagination in inventory tool",
  "requires_tools": true,
  "required_tools": ["ado_create_branch", "gitops_create_pr"],
  "inputs": { "artifactIds": ["..."], "paths": ["..."] },
  "definition_of_done": "PR opened with tests updated"
}

Оркестратор:

валидирует, что вызывающий агент имеет право создавать такой subtask (политика).

кладёт subtask в очередь текущего run (или создаёт отдельный run, если так проще).

4) Конкретные изменения по коду (минимально необходимый набор)
4.1 Новые/изменённые настройки

OrchestrationSettings:

MaxMissingToolRetries (default 2)

ToolInlineThresholdChars (например 4000–8000)

EnableStructuredDelegation (feature flag)

EnableDirectAddressing (feature flag)

EnableCompositeInventoryTool (feature flag)

4.2 MultiRoundGroupChatManager

Заменить:

ParseDelegation() (text-based)
на:

обработчик function-call orchestrator_delegate_tasks и очередь задач

Добавить:

проверку requires_tools после worker-turn

retry worker-turn с system message

фиксацию task status: queued → running → tool_called → verified → completed/failed

4.3 McpToolProvider

Оставить MaxToolResultChars как ограничение для inline сообщений, но:

сохранять полный результат в artifact при превышении threshold

добавить pagination helper (если MCP tools поддерживают page tokens)

Важно: увеличение MaxToolResultChars до 16000 допустимо, но это не замена пагинации и artifact-first.

4.4 Artifact pipeline

Дополнить Artifact:

Metadata (json/string)

sourceTool, serverType, isTruncatedInline, totalItems, pageInfo

ContentFormat (json, text, table)

Добавить tool:

artifact_fetch

4.5 AgUiEndpoints / routing

Добавить:

распознавание @Agent и выбор direct workflow

запись AddressedTo в message metadata

4.6 Seeder.cs (prompts)

Обновить тексты Manager/DevOps/Developer:

Manager обязан делегировать через delegate_tasks

DevOps для inventory использует inventory_list_all_resources

Developer получает subtask/handoff через structured inputs и работает как “Copilot coding agent” (PR + tests)

5) Acceptance Criteria (обязательные сценарии)
AC-1 Structured Delegation

Given: пользователь просит “List all Azure resources”
When: Manager отвечает
Then:

Manager вызывает orchestrator_delegate_tasks

worker получает task payload (intent, required_tools, DoD)

AC-2 Enforce Tools

Given: DevOps получает azure_inventory
When: DevOps пытается ответить без tool calls
Then:

оркестратор отклоняет ответ и делает retry
And:

после 2 retries task marked failed и Manager получает failure evidence

AC-3 Full Inventory (no loss)

Given: в подписке 200+ ресурсов
When: DevOps выполняет inventory
Then:

возвращён итоговый count

сохранён artifactId с полным списком

в чат не вставляется “обрезанный” список как финальный ответ

AC-4 Both sources used

Then:

execution log содержит вызовы Platform MCP + Azure MCP

в ответе есть sourceCoverage: ["Platform(ARG)", "Azure"]

AC-5 Direct addressing

Given: пользователь пишет @DevOps list resources
Then:

Manager не участвует

DevOps делает tool calls и возвращает результат (summary + artifact при необходимости)

AC-6 Subtasks

Given: DevOps обнаружил, что нужен код-фикс
When: DevOps вызывает orchestrator_create_subtask для Developer
Then:

Developer получает task и создаёт PR (или хотя бы готовит ветку/изменения)

DevOps получает artifact/handoff обратно

6) План внедрения (по фазам)
Фаза 1 (stabilize): 1–2 недели

Structured delegation (delegate_tasks)

Enforce tools + retry

Composite inventory tool (без сложного UI)

Artifact-first + artifact_fetch

Фаза 2 (modern UX): 1 неделя

Direct addressing @Agent

Улучшенный verify step + статусы

Фаза 3 (agentic task graph): 1–2 недели

Subtasks creation

Handoff artifacts between agents

Ограничения и safety policies

7) Обновлённые runtime-prompts (для Seeder.cs)

Ниже тексты, которые должны заменить старые prompts. Они короче, контрактнее и “под новую механику”.

7.1 Manager Prompt (runtime)

Роль: планер и координатор

Ключ: делегируй ТОЛЬКО через orchestrator_delegate_tasks

YOU ARE: Manager (Incident Commander / Planner).
TOOLS: NO direct MCP tools. You can ONLY coordinate by delegating structured tasks.

CRITICAL OUTPUT CONTRACT:
1) Your FIRST response MUST include:
   - [TRIAGE]
   - [PLAN]
   - A FUNCTION CALL: orchestrator_delegate_tasks with at least one task.
2) Do NOT delegate by mentioning names in plain text. ALWAYS use orchestrator_delegate_tasks.
3) If a task requires data gathering, you MUST delegate to DevOps or Developer with:
   - intent
   - required_tools
   - definition_of_done

TRIAGE FORMAT:
[TRIAGE]
Service: <...>
Environment: <...>
Severity: <low|medium|high>
Goal: <...>

PLAN FORMAT:
[PLAN]
1) <task summary> → delegate

TERMINATION:
- Only output [RESOLVED] when you have evidence-backed completion.
- If action is dangerous, output [APPROVAL REQUIRED] with a clear approval package.
7.2 DevOps Prompt (runtime)
YOU ARE: DevOps Worker.
TOOLS: Azure MCP + Platform MCP + ADO (read-only). No GitOps.

NON-NEGOTIABLE RULE:
If your assigned task has requires_tools=true, your FIRST action MUST be tool calls.
Text-only responses are rejected by the orchestrator.

INVENTORY STANDARD (azure_inventory intent):
- ALWAYS use the internal tool inventory_list_all_resources.
- Do NOT attempt to list everything inline. Return summary + artifactId.
- Evidence must include: counts, sourceCoverage, and whether pagination completed.

RESPONSE FORMAT:
[EVIDENCE]
- <tool calls summary + key outputs>
[INTERPRETATION]
- <what it means>
[RECOMMENDED ACTION]
- <next steps / handoff if needed>
7.3 Developer Prompt (runtime)
YOU ARE: Developer Worker (Coding Agent).
TOOLS: ADO + GitOps (rw). No Azure/Platform.

NON-NEGOTIABLE RULE:
If task requires_tools=true, your FIRST action MUST be tool calls (repo inspection, branch, PR etc.)

DELIVERABLE STANDARD:
- Implement changes in code
- Add/Update tests
- Open PR with a concise description and verification steps
- Provide a rollback note if feature flags are involved

RESPONSE FORMAT:
[EVIDENCE]
- <what files changed, PR link/id, tests run>
[ROOT CAUSE]
- <why it was broken>
[FIX PROPOSAL]
- <what you changed>
[VERIFICATION PLAN]
- <how to verify in runtime>