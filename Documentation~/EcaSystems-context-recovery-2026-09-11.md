# EcaSystems — контекст для продолжения работы

Дата актуализации: 2026-09-12, Core1 Base prototype.

Этот recovery-файл обновлён после Core1 Base prototype. Актуальные решения определяет [Context.md](Context.md), ближайшие этапы — [ToDo.md](ToDo.md), будущие возможности — [Roadmap.md](Roadmap.md). Текущий этап — validation Core1 Base; следующий — обсуждение Execution reuse of BaseEngine, затем Scope/Systems.

[Полный исторический recovery snapshot](Architecture/EcaSystems-context-recovery-full-2026-09-11.md) сохранён отдельно из base/main; он может содержать устаревшие решения и не переопределяет этот краткий срез и более новые решения.

## Core1: новый параллельный prototype

Новая архитектура находится в `Runtime/Core1`, namespace/assembly `EcaSystems.Core1`. Временная вертикальная ось — `A`, `A/Abstractions` плоский; пока реализованы только Abstractions + Base и Utils. Старый `Runtime/Core`, старые tests и historical full snapshot не менялись и остаются reference implementation до отдельного решения о миграции. Core1 имеет отдельную `Tests/Editor/Core1/EcaSystems.Core1.Editor.Tests.asmdef`.

Event — декларация (metadata + EventStateType), без Fire/Run. Condition/Action — программные executable declarations с собственными metadata, `Check(state, runnerContext)` / `Task Run(state, runnerContext)`. Rule = 1 Event + optional Condition + 1 Action, ссылки прямые. ActionRegistry/ConditionRegistry отсутствуют до реального use case. `RuleState` = данные, `RunnerContext` = infrastructure. Base State содержит только EventState; Condition и Action получают тот же экземпляр, каждый Rule/Fire получает новый State. Пустой payload — `EcaEventStateEmpty`, readonly struct без Value; есть non-generic Event/Rule shortcuts.

EventRegistry заполняется заранее, duplicate Id отклоняется; Id + точный EventStateType определяют идентичность. Fire не регистрирует Event. RuleRegistry требует зарегистрированный Event и Action, выбирает снимок Rules в порядке регистрации. EventDispatcher зависит только от EventRegistry и испускает **обычное синхронное C# событие Fired**. Queue/pendingEvents/deferred dispatch отсутствуют. Engine подписывается на Dispatcher; Dispose только снимает подписку. Fire вызывается на Dispatcher.

BaseEngine работает через `IEcaRuleRunner` и opaque `IEcaRuleRun`: сначала CreateRun для всех Rules, затем Check всех, затем RunAction прошедших. **ALL CONDITIONS → ALL ACTIONS принадлежит Engine**, локально одному invocation. Task не ожидаются, как в старом Base; синхронные ошибки прерывают pipeline, ошибка Condition не допускает его Action-фазу.

Типовая модель: полный `IEcaRule<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext>` и простой Base `EcaRule<TEventState>`. Condition/Action contravariant по входам; RuleState covariant по read-only payload, Event/Rule invariant. Base runner использует `EcaRuleState<T>` и marker runner contexts. Другую специализацию должен обслужить другой runner.

C# не умеет восстановить неизвестный закрытый T из non-generic token. Решение состоит из двух маленьких мостов: generic EventOccurrence вызывает `Visit<T>` на отдельном CreateRequest; после typed-проверки RuleRunner создаёт private RuleRun<T> с typed Rule/State. Token связан со stateless RunBridge<T>, который возвращает управление generic-методам **RuleRunner**, а те вызывают независимые ConditionRunner/ActionRunner. Token и Rule не имеют методов выполнения. Private pairing token/bridge и owner guard делают внутреннее приведение безопасным; dynamic/reflection execution нет. Публичны только opaque token/runner seam и occurrence visitor в Base, чтобы другой слой из своей assembly мог заменить runner. Конкретные request/token/bridge скрыты. Подробное объяснение — в разделе Core1 документа Context.

Один engine проверенно обрабатывает custom reference payload, int, nullable int и empty state. Test-only replacement runner из отдельной assembly создаёт производный State и собственную infrastructure без изменения Engine; trace Create1 → Create2 → Check1 → Check2 → Action1 → Action2. Это validation расширяемости, не Execution implementation.

Точный nested Fire trace: **Check A1 → Check A2 → Action A starts → Check B1 → Check B2 → Action B1:nested → Action B2 → Action A continues → Action A2**. B запускается непосредственно в стеке A до возврата Fire. Async completion не ожидается. Проверены также повторный Fire того же Event с независимым State и Fire из Condition: вложенные Actions могут пройти до оставшихся внешних Conditions, поскольку invariant действует на каждый invocation отдельно.

Не реализованы Core1 Execution/Scope/Systems/Commands integration, StateBuilder, ContextFactory, lifecycle/status model, ExecutionState, cancellation. Следующему Execution оставлены расширение State, создание данных, group/admission Overlap/Limit после Conditions, async observability, lifetime/unregister и инфраструктура RunnerContext. Он должен **переиспользовать BaseEngine через другой RuleRunner/RuleState**, не копировать Fire. Будущий EcaRunMode → EcaExecutionMode — только Execution. Возможный Group layer между Execution и Scope и queued/deferred Event processing как optional future execution policy отражены в Roadmap; Save/Load, Gates, Queries, Sequence, Inspection не дублируются.

Проверки 2026-09-12: раздельная C# 9-компиляция Core1 и его tests — 0 errors / 0 warnings; NUnit вне Unity — Core1 **25/25**, старые **37/37**. Полный Unity **6000.5.6f1** batchmode EditMode suite — **62 passed, 0 failed, 0 skipped**, включая обе assemblies (25 + 37), завершение code 0. XML: `.validation~/core1-unity-results.xml`, log: `.validation~/core1-unity.log` (локальные игнорируемые артефакты). В log есть timeout Unity cloud config при завершении процесса, результат tests Passed. PlayMode и удалённый GameCI не запускались. Это локальная версия Unity, отличная от CI 6000.3.19f1.

## Проект и сохранённый Runtime/Core

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

## Направления после Base/Execution prototype

Вертикальная ось — features/layers: Base, Commands, Execution, Scope, Systems, возможный Inspection/Debug. Это не строгая линейная Clean Architecture; понятную структуру папок желательно сохранить до MVP.

Горизонтальная ось — самостоятельные Events, Conditions, Actions, Commands, State, Rules, Context, Execution. Core1 сохраняет Rule как декларативную композицию и разделяет dispatcher/runners. Более широкие bindings/subscriptions могут потребовать отдельного сравнения после validation.

Развить relation Event ↔ Rule/Condition/Action ↔ Execution на выбранных EventDispatcher и RuleRun seam. System — организационный адаптер и ownership boundary; Events/Commands/State не должны существовать только через System как центральный runtime container.

Для будущей команды Fire Event уже принято: вызов из Action по умолчанию испускает Event в том же Scope, где выполняется Action. Explicit cross-scope targeting — возможное дальнейшее расширение. Реализация Fire Event сейчас не добавляется.

Bound Commands в старом ActionContext смешивают данные и сервисы; текущий API сохранён до отдельного решения. Core1 явно разделяет RuleState = data и RunnerContext = infrastructure. Новые State не получают сервисы.

Только после этого возвращаемся к Events + Systems implementation, затем TimeSystem/Wait и Global Variables как отдельной system/capability. SystemState сейчас предполагается глобальным первым этапом. После Global State/Variables нужно отдельно определить hierarchical/scoped lookup, inheritance и override: global → child → grandchild/local. Это не минимальный EcaScopeState.

Готовые Systems пакета предполагаются рядом с Runtime/Core: `Runtime/Systems/<System>/Core` для самостоятельного функционала и `Runtime/Systems/<System>/Eca` для адаптера. Примеры — Time и Global Variables / Global State. Внешние/клиентские Unity-системы могут иметь любую структуру; ECA-модуль лишь адаптирует их.

## Сохранённые границы старого checkpoint

В старом Runtime/Core не реализованы Systems, EventRegistry, Fire Event Command, dispatcher/bus/runtime, subscriptions/bindings, TimeSystem/Wait, GlobalVariables, scoped SystemState, GUID/stable ID API, DI/service locator/singleton. EventRegistry и синхронный dispatcher теперь есть только в Core1. Старые Commands API, Execution overlap/limit и Scope lifecycle сохранены. Root engine хранит приложение своим способом.

Комментарии об архитектуре — обычные block comments, не XML documentation нестабильного public API. Core1 имеет минимальные Base/empty Rule shortcuts; дальнейшее удобство API, factory-style API и универсальная hydration — возможности дальнейшего обсуждения.

[README](../README.md) содержит введение и исследовательский блок про академический ECA, Warcraft III, Construct, GDevelop, Game Creator и RPG Maker. [MentalTests](Architecture/MentalTests.md) перенесён в историческую Architecture и не является source of truth.

## Тесты и CI: фактическое состояние

Источник конфигурации — [.github/workflows/tests.yml](../.github/workflows/tests.yml): один Unity EditMode job на ubuntu-latest, Unity 6000.3.19f1, GameCI v4. Checkout в корень; rsync готовит копию в _ci/EcaSystemsPackage; packageMode: true, projectPath указывает на эту копию. Триггеры — pull_request, push только main, workflow_dispatch. Feature push сам CI не запускает.

PlayMode tests/job пока отсутствуют. Добавить с реальными PlayerLoop/Unity lifecycle-сценариями. Secrets — UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD; значения не выводить. Check name — EcaSystems EditMode Tests, artifact — unity-editmode-results из artifacts/EditMode.

Input coverageEnabled не задан. Прежний recovery-срез сообщал, что попытка false породила несовместимый CLI flag --no-coverageEnabled; input удалили. Отсутствие input не гарантирует отключение coverage во временном GameCI-проекте. Coverage рассматривать отдельно; checkpoint workflow не меняет.

История до checkpoint: recovery сообщал CI 31/31 passed на Unity 6000.3.19f1, вместе с проблемой генерации coverage report и возврата Personal seat. Это историческое сообщение, не новая проверка CI.

Проверки checkpoint: Core и тесты компилируются отдельно с C# 9; NUnit вне Unity — 37/37 passed; один локальный Unity 6000.5.6f1 EditMode run — 37/37 passed, без пропусков. Локальная Unity отличается от версии CI. PlayMode и удалённый GameCI для этой ветки не запускались. Подробности — [Testing.md](Testing.md).

## Рабочий процесс

Пользователь обсуждает архитектуру, затем передаёт временную инструкцию. Более поздние решения имеют приоритет; перед изменениями нужно читать актуальный код и Git-состояние. Не реализовывать отложенные возможности без запроса. Постоянная документация ведётся на русском.

Core1 prototype выполняется в новой ветке `codex/core1-base-prototype` от актуального main, с commit и push. Временный instructions-файл и его .meta удаляются перед final commit. PR пользователь создаёт вручную.
