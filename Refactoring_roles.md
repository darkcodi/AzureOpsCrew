# Техническое задание

## Настройка ролей, прав, MCP-доступов и orchestration policy для 3 агентов

Нужно переработать текущую мультиагентную систему так, чтобы она работала как управляемая инженерная команда, а не как roleplay-чат.

В системе должно остаться **ровно 3 агента**:

* **Manager**
* **DevOps**
* **Developer**

Использовать уже существующие 4 MCP сервера:

1. **Azure MCP (Official)**
2. **Azure Platform MCP (Custom)**
3. **Azure DevOps MCP (Official)**
4. **Azure DevOps GitOps MCP (Custom)**

Креды и параметры доступа брать из файла с доступами.
**Не выводить секреты, токены, client secrets и sensitive config в логи, ответы модели и UI.**

---

## 1. Главная цель

Нужно настроить систему так, чтобы:

* каждый агент имел **свою чёткую специализацию**;
* один агент **не делал всё подряд**;
* роли дополняли друг друга;
* все dangerous actions проходили через policy/approval;
* система была максимально близка по поведению к хорошим task agents:

  * планирование,
  * декомпозиция,
  * evidence-driven investigation,
  * handoff,
  * controlled execution,
  * verification.

---

## 2. Оценка текущего MCP набора

На текущем этапе функционала **достаточно** для следующего:

### Infra / runtime / Azure

Через Azure MCP + Platform MCP можно:

* находить ресурсы;
* читать ARM/ARG inventory;
* смотреть Container Apps / Key Vault / Networking / SQL / VM / SWA;
* диагностировать runtime и config issues;
* делать controlled remediation.

### Dev workflow

Через Azure DevOps MCP + GitOps MCP можно:

* искать репозитории и код;
* читать файлы;
* создавать ветки;
* изменять файлы;
* commit/push;
* создавать PR;
* запускать pipelines;
* читать статусы build/run.

### Вывод

Новые MCP сейчас **не требуются**.
Задача — не добавлять ещё тулзы, а **правильно ограничить и распределить имеющиеся**.

---

## 3. Роли и обязанности

### 3.1. Manager

Роль: **оркестратор / incident commander / planner**

Отвечает за:

* приём задачи пользователя;
* построение initial plan;
* декомпозицию на шаги/подзадачи;
* определение: infra / code / mixed;
* назначение задач DevOps и Developer;
* контроль прогресса;
* контроль наличия evidence;
* остановку на approval checkpoints;
* финальный summary.

Manager **не должен**:

* сам исправлять инфраструктуру;
* сам писать код;
* сам выполнять commit/push;
* сам запускать dangerous write actions.

---

### 3.2. DevOps

Роль: **инфраструктура / расследование / remediation / verification**

Отвечает за:

* Azure/runtime investigation;
* логи, метрики, конфиги, секреты, сеть, Container Apps, SQL, Key Vault, VM;
* инцидентные диагностические сценарии;
* выявление infra/config/secret/network/root-cause;
* подготовку evidence package для Developer, если проблема кодовая;
* выполнение infra remediation;
* verification после деплоя или исправления.

DevOps **не должен**:

* редактировать код;
* делать commit/push;
* использовать GitOps MCP write tools.

---

### 3.3. Developer

Роль: **код / branch / commit / PR / pipeline / application deploy flow**

Отвечает за:

* анализ кода;
* чтение файлов репозитория;
* изменение кода;
* создание branch;
* commit/push;
* PR;
* запуск pipeline;
* сопровождение code-fix flow;
* подготовку release/deploy context;
* summary по изменениям.

Developer **не должен**:

* работать с Azure MCP;
* работать с Platform MCP;
* расследовать Azure infra напрямую;
* менять NSG / secrets / infra config;
* выполнять инфраструктурные remediation actions.

---

## 4. Матрица доступов

### Manager

Разрешить:

* Azure MCP → **read only**
* Platform MCP → **read only**
* Azure DevOps MCP → **read only**
* Azure DevOps GitOps MCP → **no access**
  (опционально read-only только если без этого нельзя, но по умолчанию отключить)

### DevOps

Разрешить:

* Azure MCP → **read + controlled write**
* Platform MCP → **read + controlled write**
* Azure DevOps MCP → **read only**
* Azure DevOps GitOps MCP → **no access**

### Developer

Разрешить:

* Azure DevOps MCP → **read + operational use**
* Azure DevOps GitOps MCP → **read + write**
* Azure MCP → **no access**
* Platform MCP → **no access**

---

## 5. Tool policy и запреты

Нужно реализовать policy layer не только на уровне prompt, но и на уровне routing / tool authorization.

### Обязательные запреты

* Manager не может вызывать write tools
* DevOps не может использовать GitOps MCP write tools
* Developer не может использовать Azure MCP
* Developer не может использовать Platform MCP
* никто не может выполнять запрещённый tool call только потому, что “модель так решила”

### Классы операций

Разделить операции минимум на:

* `read_safe`
* `read_sensitive`
* `write_controlled`
* `write_dangerous`

### Правила

* `read_safe` — разрешено в рамках role scope
* `read_sensitive` — отдельно контролировать (секреты, чувствительные значения)
* `write_controlled` — разрешать только тем агентам, кому это положено, и при нужной policy
* `write_dangerous` — только через approval / env policy / explicit gate

---

## 6. Routing логика

### Если задача infra/runtime/config/network/secrets

Manager → DevOps

### Если задача code-only

Manager → Developer

### Если задача mixed

Manager:

1. сначала отправляет задачу DevOps;
2. DevOps собирает evidence и делает structured handoff;
3. затем Manager отправляет пакет Developer;
4. после code fix Developer возвращает результаты;
5. DevOps участвует в verification / deploy validation.

---

## 7. Handoff protocol

Нужно реализовать structured handoff.

### DevOps → Developer

Передавать не абстрактный текст, а пакет:

* service / environment
* symptom summary
* logs/errors
* what was checked
* suspected root cause
* suspected component/file/module
* deployment context
* expected outcome from Developer

### Developer → Manager / DevOps

Передавать:

* что изменено
* какие файлы
* branch
* commit(s)
* PR
* pipeline/deploy context
* risk summary
* verification notes

---

## 8. Deploy / execution policy

### Developer

Может:

* создавать branch
* commit/push
* открывать PR
* запускать pipeline
* выполнять app deploy flow **только если это разрешено policy**

### DevOps

Может:

* делать infra remediation через Azure/Platform MCP
* перезапускать app/revision/VM
* менять secret/config/network rules
* **только если это разрешено policy**

### Approval

Для production / risky environments обязательно реализовать checkpoint/approve.

Минимум:

* prod deploy → approve required
* prod secret change → approve required
* prod NSG/network change → approve required
* prod dangerous restart/remediation → approve required

Для incubator/dev можно сделать более мягкую политику, но это должно быть конфигурируемо.

---

## 9. Что нужно реализовать

1. Найти текущие role configs, tool bindings и router logic
2. Удалить/отключить старые роли, если они ещё есть
3. Внедрить новую role matrix для 3 агентов
4. Внедрить tool authorization matrix
5. Ограничить MCP доступы по агентам
6. Реализовать structured handoff
7. Обновить system prompts / role prompts
8. Обновить execution policy:

   * Manager планирует и координирует
   * DevOps расследует Azure/infra
   * Developer работает с кодом и delivery flow
9. Добавить проверки, что forbidden tool calls реально блокируются

---

## 10. Обязательные тестовые сценарии

1. **Infra issue**
   Manager → DevOps
   DevOps использует Azure MCP / Platform MCP
   Developer не вызывается без необходимости

2. **Code issue**
   Manager → Developer
   Developer использует Azure DevOps MCP + GitOps MCP
   DevOps не пишет код

3. **Mixed issue**
   DevOps делает investigation
   DevOps → Developer handoff
   Developer фиксит код
   затем verification / deploy flow

4. **Developer tries Azure tool**
   Должно быть заблокировано

5. **DevOps tries GitOps write**
   Должно быть заблокировано

6. **Manager tries write action**
   Должно быть заблокировано

7. **Prod risky action**
   Должен сработать approval gate

---

## 11. Definition of Done

Задача считается выполненной только если:

* в системе осталось ровно 3 агента;
* роли не дублируют друг друга;
* Manager действительно оркестрирует, а не делает всё сам;
* DevOps работает только с infra/runtime;
* Developer работает только с кодом / PR / pipeline / deploy flow;
* MCP доступы жёстко разграничены;
* forbidden actions реально блокируются;
* handoff работает;
* тестовые сценарии проходят;
* система выглядит как инженерная агентная команда, а не как roleplay-чат.
