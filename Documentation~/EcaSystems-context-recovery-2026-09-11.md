# EcaSystems — контекст для продолжения работы

Дата актуализации: 2026-09-11, architecture checkpoint.

Этот recovery-файл обновлён после checkpoint. Актуальные решения определяет [Context.md](Context.md), ближайшие этапы — [ToDo.md](ToDo.md), будущие возможности — [Roadmap.md](Roadmap.md). Прежний порядок «Systems → EventRegistry → Fire Event Command» больше не является согласованным планом реализации.

[Полный исторический recovery snapshot](Architecture/EcaSystems-context-recovery-full-2026-09-11.md) сохранён отдельно из base/main; он может содержать устаревшие решения и не переопределяет этот краткий срез и более новые решения.

## Проект и текущий код

EcaSystems — Unity-first UPM framework для связи независимых игровых систем через ECA. Core не зависит от Unity. Репозиторий: [ra1ngo/EcaSystems](https://github.com/ra1ngo/EcaSystems). Корень репозитория одновременно является корнем package.

- EcaBaseEngine — новое имя прежнего минимального EcaEngine. Это самостоятельный Base pipeline и reference implementation, а не главный/root engine. Execution/Scope используют собственную инфраструктуру; дублирование orchestration требует review.
- Rule сохраняет три явных типа: IEcaRule<TEventContext, TConditionContext, TActionContext>. Event — декларация с ID и payload type; Condition.Check возвращает bool, Action.Run(context) — Task.
- Selector проверяет Event.Id, точный payload и точную пару role-context types. В пределах одного Fire одного engine все Conditions проверяются до любых Actions.
- Role-context interfaces invariant; read-only IEcaContext<out TEventContext> covariant.
- Общий invariant IEcaExecutionContext<TEventContext> объявляет RuleExecutionGroupState. Execution Condition/Action interfaces наследуют его и свою Base/Commands роль. Единый concrete EcaExecutionContext и старая фабрика не возвращены.
- Commands v1: string ID, heterogeneous registry, runner.Bind(actionContext), bound API доступен Action. Нет результатов/cancellation в command API. Resolve выполняется при каждом Run.
- Порядок Execution: все Conditions → допуск Limit/Overlap → new ActionContext → Bind Commands вне конструктора → создание Execution → Action. Отклонённый Fire не создаёт ActionContext и не вызывает Bind.
- Group принадлежит регистрации Rule; GroupState живой, содержит Started/Finished. Overlap — Ignore/Allow, Limit — -1/0/N. Pending → Running → Completed/Failed; исключение, включая cancellation exception, считается Failed.
- Unregister немедленно удаляет Rule/Group, но не отменяет уже запущенные Actions. Повторная регистрация того же ID получает независимую Group и Limit.
- Каждый Scope имеет отдельные engines/registries/groups, общий только CommandRunner. Общую Rule можно регистрировать по ссылке, state независим; вложенные изменяемые Condition/Action при этом остаются общими объектами.
- Scope hierarchy — ownership/lifetime. Fire локален; parent Dispose каскаден; Actions завершаются естественно. ScopeId уникален среди активных scopes, после Dispose переиспользуется с проверкой object identity.

## ScopeState: что действительно сделано

EcaScope.State хранит отдельный EcaScopeState только с get-only ScopeId; Scope.ScopeId читает его. Новое время жизни Scope создаёт новый State даже при том же ID. State не содержит engine, EcaScope или сервисов.

IEcaScopeContext<TEventContext> наследует IEcaExecutionContext<TEventContext> и добавляет ScopeState. Это подготовленный контракт, **не runtime wiring**. Scope.Fire по-прежнему поддерживает точную пару Execution role types; ExecutionEngine и Group создают конкретные контексты через new. Добавлять Scope-зависимость вниз в Execution или временный factory/service environment нельзя. Создание/выбор расширенных контекстов оставлены следующей архитектурной итерации. Универсальная ContextFactory/hydration не выбрана.

Механизм создания/расширения контекстов необходимо определить до полноценного Systems runtime: Scope уже требует ScopeState, Systems позже добавит SystemState. Универсальная фабрика не обязательна; временный service locator или скрытый runtime service внутри data context недопустимы.

## Ближайшая архитектурная работа перед Systems

Вертикальная ось — features/layers: Base, Commands, Execution, Scope, Systems, возможный Inspection/Debug. Это не строгая линейная Clean Architecture; понятную структуру папок желательно сохранить до MVP.

Горизонтальная ось — самостоятельные Events, Conditions, Actions, Commands, State, Rules, Context, Execution. Текущий runtime слишком Rule-centric. Нужно сохранить Rule как удобную декларативную/authoring композицию и сравнить RuleSelector-based runtime с Event → bindings/subscriptions.

Определить relation Event ↔ Rule/Condition/Action ↔ Execution и проверить её на Fire Event/routing без cyclic dependencies. EventDispatcher/EventBus/EventRuntime пока не выбраны. System — организационный адаптер и ownership boundary; Events/Commands/State не должны существовать только через System как центральный runtime container.

Для будущей команды Fire Event уже принято: вызов из Action по умолчанию испускает Event в том же Scope, где выполняется Action. Explicit cross-scope targeting — возможное дальнейшее расширение. Реализация Fire Event сейчас не добавляется.

Context предполагается носителем данных. Bound Commands в ActionContext могут смешивать данные и сервисы; текущий API сохранён, но после следующей архитектурной итерации его нужно пересмотреть совместно с context enrichment/hydration. Новые Context/State не получают сервисы.

Только после этого возвращаемся к Events + Systems implementation, затем TimeSystem/Wait и Global Variables как отдельной system/capability. SystemState сейчас предполагается глобальным первым этапом. После Global State/Variables нужно отдельно определить hierarchical/scoped lookup, inheritance и override: global → child → grandchild/local. Это не минимальный EcaScopeState.

Готовые Systems пакета предполагаются рядом с Runtime/Core: `Runtime/Systems/<System>/Core` для самостоятельного функционала и `Runtime/Systems/<System>/Eca` для адаптера. Примеры — Time и Global Variables / Global State. Внешние/клиентские Unity-системы могут иметь любую структуру; ECA-модуль лишь адаптирует их.

## Границы checkpoint

Не реализованы Systems, EventRegistry, Fire Event Command, dispatcher/bus/runtime, subscriptions/bindings, TimeSystem/Wait, GlobalVariables, scoped SystemState, GUID/stable ID API, DI/service locator/singleton. Commands API, Execution overlap/limit и Scope lifecycle сохранены. Root engine хранит приложение своим способом.

Комментарии об архитектуре — обычные block comments, не XML documentation нестабильного public API. Rule shortcut API нужен позже, factory-style API и универсальная hydration — возможности дальнейшего улучшения, не основание для временных механизмов сейчас.

[README](../README.md) содержит введение и исследовательский блок про академический ECA, Warcraft III, Construct, GDevelop, Game Creator и RPG Maker. [MentalTests](Architecture/MentalTests.md) перенесён в историческую Architecture и не является source of truth.

## Тесты и CI: фактическое состояние

Источник конфигурации — [.github/workflows/tests.yml](../.github/workflows/tests.yml): один Unity EditMode job на ubuntu-latest, Unity 6000.3.19f1, GameCI v4. Checkout в корень; rsync готовит копию в _ci/EcaSystemsPackage; packageMode: true, projectPath указывает на эту копию. Триггеры — pull_request, push только main, workflow_dispatch. Feature push сам CI не запускает.

PlayMode tests/job пока отсутствуют. Добавить с реальными PlayerLoop/Unity lifecycle-сценариями. Secrets — UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD; значения не выводить. Check name — EcaSystems EditMode Tests, artifact — unity-editmode-results из artifacts/EditMode.

Input coverageEnabled не задан. Прежний recovery-срез сообщал, что попытка false породила несовместимый CLI flag --no-coverageEnabled; input удалили. Отсутствие input не гарантирует отключение coverage во временном GameCI-проекте. Coverage рассматривать отдельно; checkpoint workflow не меняет.

История до checkpoint: recovery сообщал CI 31/31 passed на Unity 6000.3.19f1, вместе с проблемой генерации coverage report и возврата Personal seat. Это историческое сообщение, не новая проверка CI.

Проверки checkpoint: Core и тесты компилируются отдельно с C# 9; NUnit вне Unity — 37/37 passed; один локальный Unity 6000.5.6f1 EditMode run — 37/37 passed, без пропусков. Локальная Unity отличается от версии CI. PlayMode и удалённый GameCI для этой ветки не запускались. Подробности — [Testing.md](Testing.md).

## Рабочий процесс

Пользователь обсуждает архитектуру, затем передаёт временную инструкцию. Более поздние решения имеют приоритет; перед изменениями нужно читать актуальный код и Git-состояние. Не реализовывать отложенные возможности без запроса. Постоянная документация ведётся на русском.

Изменения checkpoint выполняются в новой ветке с commit и push. Временный instructions-файл удаляется перед commit. PR пользователь создаёт вручную.
