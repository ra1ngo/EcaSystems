# EcaSystems — ToDo

> Текущая точка фиксации: базовый ECA-слой и слой RuleRuntime реализованы, Unity smoke-test проходит с результатом **40 PASS / 0 FAIL**.
>
> Этот файл фиксирует текущие архитектурные решения и задачи, которые намеренно оставлены на будущее.

## Текущая стабильная база

- [x] Сохраняем структуру пакета:
  - `Runtime/Core`
  - `Runtime/Unity`
- [x] Внутри `Runtime/Core` используем:
  - `Base`
  - `RuleRuntime`
  - `Helpers`
- [x] `DelegateEcaCondition` находится в `Runtime/Core/Helpers`.
- [x] Используем `EcaEventContextEmpty` для событий без payload, вместо перегрузок и дублирования API.
- [x] Пока используем `using EcaRuleId = System.String` только как alias для удобства чтения.
- [x] Текущие проверки аргументов и config оставляем без изменений.
- [x] Базовый слой полностью работоспособен без RuleRuntime.
- [x] Базовый pipeline:
  - `EcaRuleRegistry`
  - `EcaRuleChecker`
  - `EcaRuleRunner`
  - `EcaEngine`
- [x] `EcaRuleRegistry` отвечает за:
  - `Register`
  - `Unregister`
  - `GetRulesForEvent`
- [x] `EcaRuleChecker` ничего не хранит и только проверяет Conditions.
- [x] Для одного `Fire` сначала проверяются все Conditions, и только после этого запускаются Actions.
- [x] `EcaRuleRunner` запускает один уже отфильтрованный Rule и ничего не хранит и не проверяет.
- [x] RuleRuntime-слой использует те же `Registry / Checker / Runner`, что и Base.
- [x] `EcaRuleRuntimeRegistry` хранит runtime-сущности Rules.
- [x] `EcaRuleRuntime` отвечает за:
  - активные `EcaRuleExecution`
  - lifecycle execution
  - overlap-поведение
  - будущую очередь
- [x] `EcaRuleRuntimeState` живёт между executions одного Rule.
- [x] Сейчас `EcaRuleRuntimeState` содержит:
  - `EcaRuleExecutionTotalStarted`
  - `EcaRuleExecutionTotalFinished`
- [x] Runtime state, передаваемый через context, является живым состоянием, а не snapshot.
- [x] Condition может видеть счётчики до запуска текущей execution.
- [x] Action может видеть тот же живой state уже после увеличения `Started` текущей execution.
- [x] `EcaRuleExecutionStatus` сейчас содержит:
  - `Pending`
  - `Running`
  - `Completed`
  - `Cancelled`
  - `Failed`
- [x] Реализован и проверен `Overlap.Ignore`.
- [x] Реализован и проверен `Overlap.Allow`.
- [x] Завершённые executions удаляются из активной коллекции.
- [x] Failed Action корректно завершает lifecycle execution и увеличивает `Finished`.
- [x] Unity smoke-test проходит: **40 PASS / 0 FAIL**.

## Следующие архитектурные задачи

- [ ] Вернуться к временному названию `IEcaRuleOverlapped`.
  - Найти название, которое описывает capability/policy и не привязывает весь RuleRuntime только к overlap.
- [ ] Вернуться к названию второго Engine (`EcaRuleRuntimeEngine`) после стабилизации RuleRuntime API.
- [ ] Определить точный публичный API для просмотра `EcaRuleRuntime` и активных executions.
- [ ] Решить, должен ли `EcaRuleRuntimeRegistry` предоставлять только lookup или также runtime-management операции.
- [ ] Определить semantics `Unregister`, если у Rule есть активные executions:
  - продолжить выполнение
  - отменить
  - сразу удалить runtime
  - оставить runtime до завершения активных executions
- [ ] Решить, что происходит с `EcaRuleRuntimeState` после `Unregister`.
- [ ] Явно определить cancellation-поведение:
  - отмена одной execution
  - отмена всех executions одного Rule
  - status/error semantics cancellation
- [ ] Решить, нужны ли публичные hooks/events для ошибок execution помимо статуса `Failed`.
- [ ] Вернуться ко второй глобальной архитектурной проблеме, которую пока намеренно отложили.

## Расширение RuleRuntime

- [ ] Реализовать и протестировать `Overlap.Reset`.
  - Отменить активную execution / executions.
  - Создать новую execution.
  - Проверить semantics счётчиков.
- [ ] Спроектировать `Overlap.Queue` до реализации.
  - Решить, что именно попадает в очередь до появления execution.
  - Оставить ownership очереди внутри `EcaRuleRuntime`.
  - Определить FIFO semantics.
  - Определить поведение Queue при failure / cancellation / reset / unregister.
- [ ] Провести отдельные mental tests для Queue до написания кода.
- [ ] Решить, нужна ли queued-работе отдельная небольшая data-сущность или можно оставить это внутренней реализацией.
- [ ] Проверить recursive `Fire` для `Ignore`, `Allow`, будущих `Reset` и `Queue`.
- [ ] Проверить, что все выбранные executions переходят в `Pending` до запуска любых соответствующих Actions.
- [ ] Добавить явные smoke-tests для cancellation.
- [ ] При необходимости добавить проверки `Failed / Cancelled` до удаления завершённой execution из runtime.

## Context / runtime state

- [ ] Сохранять `EventContext` и Rule runtime state как отдельные части ECA context.
- [ ] Добавлять новые поля context только при появлении конкретного требования.
- [ ] Не вводить `EcaEventState`.
- [ ] Не вводить отдельные runtime-версии Checker/Runner.
- [ ] Вернуться к вопросу, нужно ли Conditions и Actions получать дополнительные RuleRuntime-данные помимо текущих счётчиков.
- [ ] Решить, нужно ли в будущем добавить в runtime state:
  - количество активных executions
  - количество элементов в pending queue
  - статус последней execution
  - последнюю ошибку
  - другие значения — только при наличии реального use case

## Validation / IDs

- [ ] Текущую runtime-validation пока оставляем.
- [ ] Позже вернуться к вопросу, должен ли `Name` быть обязательным.
- [ ] Позже рассмотреть замену `using EcaRuleId = System.String` на strongly typed ID, только если смешивание ID станет реальной проблемой.
- [ ] Сохранить тесты на collision `EventId` / несовместимый тип context.
- [ ] Сохранить тесты на duplicate `RuleId`.

## Тестирование

- [ ] Сохранить текущий `EcaSystemsSmokeTest` как быстрый Unity integration test.
- [ ] Базовое ожидаемое состояние: `FAIL 0`.
- [ ] Добавлять тесты при появлении каждой новой overlap strategy или lifecycle semantics.
- [ ] Добавить тесты для:
  - `Unregister` при активной execution
  - явной cancellation
  - recursive `Fire`
  - нескольких Rules с разными runtime policies
  - наблюдения за execution failure
  - будущего `Queue`
  - будущего `Reset`
- [ ] Позже перенести стабильные проверки из smoke-test MonoBehaviour в формальные тесты Unity Test Framework.

## Отложено — пока не реализовывать

- [ ] Commands layer.
- [ ] Modules layer.
- [ ] Visual programming layer.
- [ ] EventBlock.
- [ ] Templates.
- [ ] Gates как отдельный framework primitive.
- [ ] Signals / WaitSignal.
- [ ] FSM / Event Pages.
- [ ] EventGroup.
- [ ] JSON / data-authored Rules.
- [ ] Persistent save/load для RuleRuntime.
- [ ] Сохранение и восстановление игры посреди активной Action.
- [ ] Execution middleware / policies без конкретного требования.
- [ ] Thread-safe / background-thread `Fire`; Unity integration пока считаем main-thread oriented до появления реального use case.

## Документация / cleanup

- [ ] Поддерживать `Documentation~/Architecture/mental-tests.md` в актуальном состоянии.
- [ ] Обновить mental tests после фиксации semantics `Reset / Queue / Unregister`.
- [ ] Добавить короткий architecture overview после стабилизации RuleRuntime API.
- [ ] Удалить устаревшие экспериментальные файлы и классы предыдущих ECA-реализаций, если они ещё остались в репозитории.
