Цель

Привести текущую мультиагентную систему управления инфраструктурой к рабочему, предсказуемому и управляемому виду, чтобы она перестала быть “чатом с болтовнёй” и стала системой, которая:

принимает задачу от пользователя в чате;

превращает её в рабочий run / incident / ticket;

распределяет работу между Manager / DevOps / Developer;

использует MCP-инструменты как обязательный источник фактов;

собирает evidence перед выводами;

умеет диагностировать проблемы, предлагать исправления, готовить PR и изменения;

не выполняет deploy без явного approve от пользователя;

логирует весь ход работы и позволяет отлаживать поведение агентов.

Контекст проекта

Текущая система — это чат-интерфейс в стиле Discord/Slack с каналами и участниками:

User

Manager

DevOps

Developer

Сейчас это реализовано на базе Microsoft Azure AI Framework / GroupChat-подобной оркестрации.
Агенты подключены к MCP через HTTP. MCP сервер поднят в облаке. Клиент подключается к нему по HTTP.

На начальном этапе считать, что:

используется Azure MCP со всеми доступными инструментами;

если уже существует отдельный Azure DevOps MCP или аналогичный доступ к Azure DevOps через текущую инфраструктуру — использовать его и интегрировать в общую политику;

модели — обычные актуальные OpenAI-модели, без кастомных локальных LLM;

все агенты технически могут иметь read-write доступ к инструментам, но политика использования прав должна быть разграничена логикой системы.

Бизнес-цель

Пользователь должен иметь возможность написать в чат что-то вроде:

“Сайт не работает”

“Проверь, почему упал deploy”

“Посмотри, почему API отвечает 500”

“Найди причину деградации”

“Если проблема в коде — подготовь исправление”

и система должна:

собрать факты через MCP;

выдвинуть гипотезы на основе evidence;

при необходимости передать задачу Developer;

подготовить изменение кода и PR;

запросить у пользователя approve перед deploy;

после approve выполнить deploy;

верифицировать результат;

дать понятный итог.

Известные проблемы текущей реализации

Текущая система ведёт себя как “цирк”, а не как команда:

агенты отвечают общими фразами;

агенты “ждут”, “передают”, “сообщают”, но не выполняют рабочий цикл;

tool usage не является обязательным;

нет чёткого определения задачи;

нет критериев завершения;

нет протокола handoff;

нет разделения evidence / hypothesis / action;

нет понятного approval gate на deploy;

нет нормального логирования выполнения мультиагентного run;

нет service map / карты того, где лежат ресурсы, логи, пайплайны, репозитории.

Главная задача

Проведи полный аудит текущей реализации и внедри изменения, которые переведут систему из “чатовой симуляции команды” в рабочую мультиагентную систему диагностики и remediation.

Обязательные принципы, которые нужно внедрить
1. Протокол работы задачи

Система не должна отвечать в свободной болтовне.
Каждое пользовательское обращение, относящееся к работе с инфраструктурой / кодом / деплоем / инцидентом, должно быть преобразовано в структурированный run/ticket.

Ввести объект задачи с минимум следующими полями:

run_id

channel_id

title

user_request

goal

service

environment

severity

constraints

status

owner

assignees

plan

evidence

hypotheses

proposed_actions

requires_approval

approval_status

execution_log

verification_steps

final_summary

Нужно определить допустимые состояния задачи, например:

new

triaged

investigating

waiting_for_tool_result

waiting_for_user_input

waiting_for_approval

implementing_fix

ready_for_pr

ready_for_deploy

deploying

verifying

resolved

failed

2. Manager должен быть оркестратором, а не говорящей головой

Manager обязан:

формализовать задачу;

определить план;

назначить роли;

контролировать, чтобы DevOps и Developer возвращали не общие слова, а evidence;

блокировать бессмысленные циклы общения;

требовать tool calls там, где без них нельзя делать выводы;

требовать approval перед deploy;

завершать задачу только после verification.

Manager не должен:

делать вид, что “ожидает ответа” без фактического запуска действий;

пересказывать то, что уже и так известно;

закрывать задачу без evidence.

3. DevOps должен работать через evidence-first подход

DevOps обязан:

сначала собрать факты через MCP;

использовать доступные инструменты Azure/MCP для health, logs, metrics, deployments, ресурсов, конфигурации и состояния среды;

формировать гипотезы только после получения фактов;

явно разделять:

факт,

интерпретацию,

гипотезу,

предлагаемое действие.

DevOps не должен:

предлагать исправление без evidence;

утверждать, что проблема в коде или инфраструктуре без подтверждения;

выполнять deploy без approve пользователя.

4. Developer должен работать через кодовую доказательную базу

Developer обязан:

анализировать код, конфигурацию, пайплайны и связанные артефакты;

ссылаться на конкретные файлы, модули, конфиги, участки кода;

при выявлении проблемы в коде готовить минимальный безопасный фикс;

оформлять изменения как commit/branch/PR;

не выполнять deploy без approve пользователя.

Developer может:

создавать ветки;

вносить изменения;

готовить PR;

писать описание PR;

предлагать risk summary;

предлагать verification steps.

Developer не должен:

деплоить напрямую без approve;

“гадать” по коду без указания, где именно проблема;

переписывать большие куски системы без необходимости.

5. Approval gate на любые опасные write-операции

Нужно реализовать политику действий:

Разрешено без approve:

чтение логов;

чтение метрик;

чтение конфигурации;

чтение статуса pipeline/deploy/resources;

анализ кода;

подготовка плана;

создание гипотез;

создание draft PR, если это безопасно и допустимо текущей системой.

Только после явного approve пользователя:

deploy;

rollback;

restart production workload;

scale production resource;

изменение production-конфигурации;

merge PR;

любые действия, влияющие на production/runtime.

Если система технически имеет read-write доступ, это не значит, что она может писать всё сразу.
Нужно реализовать policy layer, который запрещает опасные действия до approve.

6. Источники правды

На первом этапе использовать имеющийся MCP-доступ как primary source of truth.

Но система должна явно знать, где искать правду по каждому сервису.
Для этого нужно внедрить service map / service registry.

Для каждого сервиса определить:

service name

environment

subscription

resource group

workload/resource names

monitoring source

logs source

metrics source

app/service identifiers

repo name

pipeline name

branch strategy

runbooks / docs / playbooks

Создать человекочитаемый и машиночитаемый формат, например YAML/JSON.

Пример структуры:

service

environment

azure.subscription

azure.resource_group

azure.resources

observability.logs_workspace

observability.app_insights

devops.project

devops.repo

devops.pipeline

docs.runbooks

owners

Если каких-то связей пока нет, создать placeholders и систему graceful degradation.

7. Минимальный RAG для v1

Нужно внедрить прагматичный минимальный retrieval layer, а не “AI-магическую абстракцию”.

Для v1 нужно сделать retrieval как минимум по:

service registry;

runbooks;

repo-level docs;

architecture notes;

known incidents / patterns;

deployment guides.

Важно:

логи и runtime metrics не нужно векторизовать как primary strategy;

живые operational данные должны запрашиваться через MCP в runtime;

retrieval нужен не вместо MCP, а для статических знаний и контекста.

Нужно спроектировать и внедрить:

ingestion pipeline для документов;

chunking strategy;

retrieval abstraction;

промпт-подмешивание найденных кусков в нужные роли;

лимиты на объём контекста;

fallback, если retrieval ничего не нашёл.

Если полноценный vector DB для v1 избыточен, допустим сначала hybrid/simple retrieval, но он должен быть реально встроен в рабочий цикл.

8. Принудительный workflow для инцидентов

Для запросов класса:

сайт не работает

API отвечает ошибкой

упал deploy

сервис деградирует

прод не поднимается

реализовать фиксированный incident workflow:

Шаг 1. Triage

выделить сервис и окружение;

определить severity;

выяснить, хватает ли информации для старта;

при нехватке информации задать точечный вопрос пользователю.

Шаг 2. Evidence collection

DevOps должен выполнить обязательный минимум диагностик, например:

health/state

недавние deploy/build events

logs/errors

metrics/resource pressure

конфигурационные аномалии

Шаг 3. Hypothesis building

Сформировать список гипотез с confidence и evidence links.

Шаг 4. Route decision

если проблема в инфраструктуре — DevOps готовит remediation plan;

если проблема вероятно в коде — Manager эскалирует Developer;

если проблема смешанная — ведётся совместная работа.

Шаг 5. Code fix path

Developer:

находит конкретную причину;

делает branch;

вносит фикс;

создаёт PR;

возвращает summary, risk и deploy plan.

Шаг 6. Approval gate

Перед deploy система обязана вывести:

proposed action

why this is the right action

risk

rollback plan

verification plan

и запросить approve.

Шаг 7. Execution

После approve выполнить deploy/изменение.

Шаг 8. Verification

Проверить:

health restored

errors reduced

deployment succeeded

key functionality works

Шаг 9. Final summary

Дать короткий итог:

root cause

what changed

status

next recommendations

9. Обязательные ограничения на поведение агентов

Нужно встроить ограничения в system prompts, orchestration logic и runtime policy.

Агентам запрещено:

отвечать шаблонным “я передал”, “жду”, “сообщу позже” без реального действия;

делать выводы без проверки через инструменты, если инструмент доступен;

спорить друг с другом ради “симуляции роли”;

повторять уже известное без добавления ценности;

выполнять опасные действия без approve;

бесконечно продолжать чат без прогресса.

Агентам обязательно:

ссылаться на evidence;

работать по структуре;

явно писать, когда это факт, а когда гипотеза;

останавливать run при отсутствии доступа/данных и запрашивать конкретную недостающую информацию;

завершать задачу только при выполненных done criteria.

10. Системные промпты ролей

Нужно создать или переработать system prompts для:

Manager

DevOps

Developer

Промпты должны быть:

короткими, строгими и операционными;

без лишней “ролевой театральности”;

заточенными под поведение, а не под стиль общения;

заставлять использовать evidence и tools;

содержать ограничения на dangerous actions;

содержать правила handoff.

Нужно убрать “личностный RP”, оставить только функциональную роль.

11. Параметры моделей и execution policy

Нужно определить и внедрить рекомендуемые runtime defaults.

Рекомендуемые базовые настройки:

Manager

low temperature

high structure

prioritise planning and coordination

no speculative diagnostics without agent evidence

DevOps

very low temperature

tool-first behaviour

must gather evidence before hypotheses

concise operational summaries

Developer

low-to-medium temperature

precise code modifications

minimal patch strategy

must reference files/components/changes

Также нужно внедрить на уровне оркестрации:

max turns per run;

max consecutive non-tool turns;

retry policy for MCP over HTTP;

timeout policy;

degraded-mode handling for unavailable tools;

explicit stop conditions;

no silent endless loops.

Подбери и зафиксируй значения в конфиге, а не хардкодь в разных местах.

12. Надёжность MCP over HTTP

Учитывать, что Azure MCP подключён по HTTP к облачному MCP серверу.

Нужно проверить и внедрить:

retries;

backoff;

timeout handling;

auth/session handling;

structured error propagation;

partial failure handling;

circuit breaker or equivalent graceful fallback;

tool availability checks at startup and optionally per run.

Если MCP недоступен:

система не должна делать вид, что она что-то проверила;

она должна честно сказать, что именно недоступно и что из-за этого нельзя проверить.

13. Трассировка, логирование и дебаг

Внедрить подробную трассировку исполнения каждого run.

Нужно сохранять минимум:

run id

user request

selected service/environment

agent messages

tool calls

tool results summary

decision points

approvals requested / granted / denied

model used

token usage if available

latency per step

retries/errors

final status

Нужен удобный формат для отладки:

JSON logs / structured logs

возможность воспроизвести run

возможность увидеть, на каком шаге всё сломалось

14. UI/чатовое поведение

Чат должен остаться удобным, но стать рабочим.

Нужно добавить/улучшить в интерфейсе:

статус run;

кто сейчас активен;

что делает агент;

какие шаги уже выполнены;

где ждём approve;

финальный summary;

при необходимости скрытый/сворачиваемый technical trace.

Сообщения должны перестать быть “диалогом персонажей” и стать рабочей коммуникацией.

15. Реализация поэтапно, а не одним коммитом

Работу выполнить по фазам.

Phase 0 — Audit

проанализировать текущую архитектуру;

найти точки входа, orchestration layer, prompts, tool adapters, state management, UI message flow;

зафиксировать текущие проблемы;

подготовить gap analysis.

Phase 1 — Architecture and protocol

спроектировать run protocol;

определить states;

определить action classes;

определить approval policy;

определить service map format;

определить retrieval design for v1.

Phase 2 — Role prompts and orchestration

внедрить новые prompts;

внедрить manager-led orchestration;

убрать idle/looping behaviour;

внедрить handoff rules;

внедрить evidence-first routing.

Phase 3 — Tool policy and MCP reliability

внедрить tool wrappers / execution policy;

добавить retries/timeouts/error handling;

разделить safe vs approval-required actions.

Phase 4 — Service registry and retrieval

внедрить registry;

подключить runbooks/docs;

внедрить retrieval into prompts/workflows.

Phase 5 — Incident workflow

реализовать обязательный инцидентный пайплайн;

интегрировать его в run lifecycle.

Phase 6 — Code fix and PR workflow

реализовать workflow, в котором Developer может:

подготовить branch,

внести change,

создать PR,

вернуть summary;

deploy остаётся только по approve.

Phase 7 — Logging, tracing, UI polish

внедрить execution trace;

улучшить chat UX;

показать progress, status, approval points.

Phase 8 — Testing and validation

прогнать сценарии;

исправить обнаруженные дефекты;

подготовить финальное summary и список дальнейших улучшений.

16. Обязательные сценарии тестирования

Нужно реализовать и прогнать хотя бы следующие end-to-end сценарии:

Сценарий 1

Пользователь: “Сайт не работает”
Ожидается:

triage;

health/logs/deploy evidence;

гипотеза;

remediation proposal;

verification plan.

Сценарий 2

Пользователь: “После последнего deploy API отвечает 500”
Ожидается:

DevOps связывает ошибку с deploy/change window;

Developer находит кодовую причину;

создаётся PR;

deploy only after approve.

Сценарий 3

Пользователь: “Проверь, почему pipeline падает”
Ожидается:

анализ pipeline/build/logs;

route to Developer only if code/config issue identified.

Сценарий 4

MCP временно недоступен
Ожидается:

честная деградация;

нормальная ошибка;

без fake analysis.

Сценарий 5

Нет достаточно данных для определения сервиса
Ожидается:

короткий, точечный вопрос пользователю;

не начинать хаотичный поиск по всему подряд.

17. Definition of Done

Работа считается завершённой только если:

система больше не ведёт себя как roleplay/chat-simulation;

Manager реально управляет run и задачами;

DevOps делает tool-driven диагностику;

Developer может готовить кодовый фикс и PR;

deploy без approve невозможен;

есть service registry;

retrieval для статического контекста реально встроен;

есть structured logging / tracing;

есть тестовые сценарии и подтверждение, что они проходят;

есть документация по новой архитектуре и точкам настройки.

18. Требуемые deliverables

В результате работы предоставить:

архитектурное описание новой схемы;

список изменённых файлов;

новые/обновлённые system prompts;

run protocol / state machine;

action policy / approval policy;

service registry format + examples;

retrieval design + what is indexed;

logging/tracing design;

тестовые сценарии;

summary: что было не так, что исправлено, что осталось улучшить.

19. Правила выполнения

Не переписывай всё с нуля без причины.

Сначала разберись в текущей кодовой базе.

Предпочитай минимально достаточные изменения с максимальным эффектом.

Все архитектурные решения объясняй через проблему, которую они решают.

Если в коде уже есть полезные механизмы, доиспользуй их.

Не придумывай отсутствующие инструменты — используй реально существующие MCP/integration points.

Если какая-то часть недоступна или не реализуема в текущем коде, зафиксируй это явно и предложи ближайший рабочий путь.

20. Формат работы агента

Работай не в режиме “сразу писать код вслепую”, а так:

Шаг A

Сделай аудит и выдай:

текущую архитектуру;

список проблем;

план реализации по фазам.

Шаг B

После этого начни реализацию по фазам и после каждой фазы давай:

что сделано;

какие файлы изменены;

что осталось;

риски/заметки.

Шаг C

В конце дай:

итоговую сводку;

как теперь работает система;

какие следующие улучшения стоит сделать.