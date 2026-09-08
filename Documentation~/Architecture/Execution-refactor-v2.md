# Base + Execution: рефакторинг v2

Эта запись описывает текущий код после применения `EcaSystems-Work-Refactor-Instructions-v2.md`. Старые ToDo и mental tests остаются историческими записями; при расхождении действует инструкция v2 и актуальный код.

## Реализовано

- Base Action и RuleRunner принимают только context, без cancellation token.
- Non-generic `IEcaRule` позволяет Registry хранить `List<IEcaRule>` с read-only представлением. Регистрация и проверки типов остаются в Registry; выборка по EventId находится в Selector. Несовместимые типы при выборке вызывают исключение.
- Checker проверяет одно правило. Оба Engine сначала проверяют все Conditions одного Fire, затем запускают прошедшие правила.
- ContextFactory соединяет общий, логически immutable EventContext с live `EcaRuleExecutionGroupState`. Deep copy отсутствует; состояния разных групп раздельны. Имена двух счётчиков сохранены.
- Generic Group хранит Rule, создаёт executions, применяет Ignore/Allow и передаёт исполнение общему Executor. Generic execution хранит Rule, Context, status, exception и свой token.
- Executor использует собственный Base RuleRunner. Cancellation вызывает отдельный `IEcaCancellableAction.Cancel(context)`, если Action его реализует. Асинхронный hook удерживает статус Cancelling до завершения; обычная ошибка Run не вызывает hook и даёт Failed.
- Повторные запросы Cancel одной execution ожидают одну задачу cleanup. Завершение Run во время cleanup не завершает учёт execution преждевременно. Без capability framework не останавливает пользовательский Run; группа сохраняет его до завершения возвращённой задачи.
- ExecutionRegistry хранит разные типы групп по RuleId, проверяет типы Get/TryGet. Ошибка регистрации группы откатывает регистрацию правила. Повторная регистрация того же экземпляра правила сохраняет группу; другой Rule с тем же Id не может молча подменить Rule существующей группы.
- Unregister по-прежнему удаляет только правило из RuleRegistry. Remove/Clear ExecutionRegistry являются операциями хранения и не вводят lifecycle policy.

## Проверка

Пакет скомпилирован Unity 6000.5.6f1 в отдельном временном проекте с локальной package dependency. Ошибок и предупреждений C# не обнаружено. Smoke tests в Play Mode: **65 PASS / 0 FAIL** (два запуска). Сохранены исходные 40 проверок, добавлены 25 проверок нового API: порядок Conditions в Execution, раздельный GroupState, отмена с асинхронным hook, Failed без cancellation, проверки типов и rollback регистрации.

Ограничение окружения: после успешных тестов оба запуска редактора записали `ArgumentOutOfRangeException` внутри `UnityEditor.Search.SearchDatabase` при стартовой индексации. Повторный процесс Unity завершился с кодом 1 из-за этой ошибки редактора; это не FAIL smoke tests и не ошибка компиляции пакета. Сам модуль Unity Search в рамках рефакторинга не изменялся.

## Нерешённые TODO — не реализованы

- Канал передачи CancellationToken в пользовательскую Action. Токен остаётся внутри execution; в Base и ExecutionContext его нет.
- Расширенный read-only просмотр executions через GroupState: статусы, previous execution, active/pending counts.
- Полный lifecycle/Dispose для registries, groups и engine **до Scope**. Registry удерживает сильные ссылки на Rules/Conditions/Actions. Требуется отдельное решение для Unregister, повторной регистрации другого Rule с тем же Id и освобождения активных групп. Close/Disposed API сейчас не вводится.
- Commands и полный cancellation contract: каждая асинхронная Command должна иметь execution-specific отмену и пользовательский cleanup; пользователь определяет остановку последовательности и ожидание cleanup.
- Scope, global/scene/local scopes, entity-scoped overlap; Systems, Global State, TimeSystem, PauseSystem.
- Queue, Reset и custom overlap strategies.
- Диагностика reentrant/cyclic Fire; detector и depth limit отсутствуют.
- Отдельные mental experiments для Priority, Retry, Timeout, MaxConcurrency, Pause, ExecutionHistory, Dependencies, Sequences, Parallel до реализации этих возможностей.
- Visual programming и save/load, а также остальные будущие функции из инструкции v2.
