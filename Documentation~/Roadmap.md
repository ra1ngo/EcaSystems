# EcaSystems — Roadmap

> Здесь находятся задачи, которые **не блокируют первое практическое использование EcaSystems в играх**.
>
> Всё, что нужно довести до начала реального использования framework — Scope, entity-scoped execution, Queue/Reset, Systems, Commands, Global State, TimeSystem и обязательный lifecycle — должно оставаться в `ToDo.md`, а не здесь.

## Execution inspection / history

- [ ] Добавить read-only API для просмотра executions через `EcaRuleExecutionGroupState`.
- [ ] Рассмотреть доступ к:
  - текущим executions группы;
  - status каждой execution;
  - previous / last execution;
  - active count;
  - pending count;
  - last failure.
- [ ] Не отдавать наружу mutable внутренние коллекции.
- [ ] Решить, нужен ли отдельный `ExecutionHistory`, или достаточно `LastExecution` / observation events.

## Debugging / observation

- [ ] Добавить механизм наблюдения за lifecycle execution:
  - started;
  - completed;
  - cancelled;
  - failed.
- [ ] Добавить удобный доступ к exception/failure information.
- [ ] Добавить diagnostic API для просмотра ExecutionGroups.
- [ ] Добавить Debug UI / Editor tooling для active executions, queues и statuses.
- [ ] Рассмотреть визуализацию цепочек `Event → Rule → Execution`.

## Reentrant Fire / cyclic events

- [ ] Добавить диагностику циклических ECA-цепочек:
  - `Event A → Action → Event A`;
  - `Event A → Event B → Event A`;
  - более длинные циклы.
- [ ] Рассмотреть tracking event-chain / call-chain.
- [ ] Рассмотреть configurable depth limit.
- [ ] Сначала диагностировать циклы, а не запрещать сложные event chains без необходимости.

## Advanced Execution features

После практической стабилизации базового Execution-слоя провести mental experiments и затем при необходимости реализовать:

- [ ] Priority.
- [ ] Retry.
- [ ] Timeout.
- [ ] MaxConcurrency.
- [ ] Pause / Resume execution.
- [ ] ExecutionHistory.
- [ ] Dependencies.
- [ ] Sequences.
- [ ] Parallel execution.
- [ ] Execution middleware / policy pipeline.

Для каждой возможности определить правильную ответственность:

```text
ExecutionGroup
Executor
Scheduler
Policy
Middleware
System
```

Цель — не превращать `EcaRuleExecutionGroup` или `EcaExecutionExecutor` в монолит.

## Advanced overlap extensibility

После стабилизации стандартных `Ignore / Allow / Reset / Queue`:

- [ ] Рассмотреть пользовательские overlap policies.
- [ ] Сохранить `EcaOverlap` как понятный список стандартных вариантов; он не обязан оставаться enum.
- [ ] Рассмотреть Strategy API.
- [ ] Рассмотреть `Policy → Plan` модель:
  - Policy принимает решение;
  - Group остаётся владельцем execution lifecycle и применяет plan.
- [ ] Рассмотреть custom policies без прямого mutable доступа к внутренностям Group.

## Visual programming / Editor

- [ ] Visual Rule Editor.
- [ ] Node/graph representation.
- [ ] UI для Events / Conditions / Actions / Commands.
- [ ] UI для Systems.
- [ ] UI для Variables / Global State.
- [ ] UI для Execution policies.
- [ ] Debug visualization active executions.
- [ ] Поддержать стабильные ID для editing/serialization.

## Data / serialization

- [ ] JSON/data-authored Rules.
- [ ] ScriptableObject authoring при необходимости.
- [ ] Stable serialization format.
- [ ] Migration/versioning формата.
- [ ] Не привязывать Core runtime к конкретному data format.

## Persistence

- [ ] Save/load Global State.
- [ ] Save/load scopes/rules при реальной необходимости.
- [ ] Save/load timer state.
- [ ] Mid-execution save/resume рассматривать отдельно и только при реальном use case.
- [ ] Не обещать resume произвольной async Action без отдельной continuation/state-machine модели.

## Testing / diagnostics maturity

- [ ] Расширенные stress tests для большого количества scopes/executions.
- [ ] Property tests для custom execution policies.
- [ ] Tests для cyclic event diagnostics.
- [ ] Performance profiling и benchmark tooling.
- [ ] Developer diagnostics для утечек lifecycle / забытых registrations.
