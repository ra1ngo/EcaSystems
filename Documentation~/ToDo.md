# EcaSystems — необходимые этапы

Здесь только работа до первого полноценного применения в играх. Execution v1, Scope v1, Base context refactor и Commands v1 завершены. Актуальные решения — в [Context.md](Context.md), необязательные будущие возможности — в [Roadmap.md](Roadmap.md). Документы ведутся на русском языке.

## Актуальный порядок следующих итераций — 2026-09-26

- [x] Standalone TimeSystem.
- [x] TimeSystem architecture refactor + Time/Eca adapter.
- [x] PR #17 registry cleanup: canonical CheckRegistered, live items/public Resolve, local System registries, concrete Time Commands и cached Events.
- [x] Core2 Fire/Context refactor: IEcaRule<E,R>, nullable раздельные contexts конкретного Fire, event-only Fire и Scope-owned EventEmitter.
- [x] EcaSystemsRuntime v1 — production composition root: EcaScopeRuntime, global System/Event/Command/Namespace registries, EcaSystemConnector и EcaCommandRunner; без автоматического root Scope. Внешний lifespan не принадлежит Core; optional lifecycle connector управляет declared bindings. Class/delegate Rule creation реализован; Unity/visual authoring остаётся Roadmap.
- [x] RuleCreator/CreateRule, AEcaCondition/AEcaAction, shared checker/runner и state-aware Commands без Bind; Time Commands мигрированы.
- [x] State registrations/resolver без Bind, State initialization Condition/Action, Base RuleId, технический ForceFire и local ExecutionRuntime composition внутри Scope.
- [x] PR #21 follow-up: State ID lookup + exact declared type, canonical ID/type/delegate validation, runtime RuleId invariant. ScopeRuntime создаёт per-Scope registries и передаёт их ссылками в Scope; Scope создаёт только ExecutionRuntime из dependencies.
- [x] Scope lifecycle с multiple listeners, transactional reverse rollback и atomic subtree prepare/commit Dispose.
- [x] Optional System LifecycleConnector, registry/coordinator и transactional late sync без дублирования topology.
- [x] GetSnapshot convention для актуальных Core2/Variables registries.
- [x] ScopeId identity: globally unique active IDs в runtime, reuse только новым instance; regression tests.
- [ ] Небольшой end-to-end Sandbox / PlayMode scenario.
- [ ] External event adapters.
- [ ] Project cleanup/consolidation.
- [ ] Signals / FireEvent / routing только после конкретного сценария.
- [ ] Продолжать Roadmap по приоритету.
## Core2 — checkpoint 2026-09-17

- [x] Core2/Base завершён; API и архитектура не меняются в Execution-итерации.
- [x] Core2/Layers/Execution: generic Group Check/Run, composition Runtime, ALL CONDITIONS → ALL EXECUTIONS, поздний admission, immediate/reentrant Fire, lifetime Limit/Overlap, lifecycle и rollback/unregister.
- [x] Scope: EcaScopeRuntime manager + isolated EcaScope, независимые Execution graphs, hierarchy/ParentScopeId, local Fire, recursive Dispose и ScopeId reuse без отмены Actions; enrichment через Func<IEcaExecutionRuleState<E>, EcaScopeState, R> сохранён.
- [x] Commands validated как самостоятельный Concept: standalone tests и BaseRuntime integration через явные state/context; Base не зависит от Commands.
- [x] Commands abstraction: AEcaCommand/AEcaCommand<R,C,A> используют class virtual dispatch для Unity compatibility; Registry.Register(AEcaCommand) принимает heterogeneous Commands, Contains добавлен.
- [x] EventEmitter concept: public IEcaEventEmitter.Fire<E>, internal Bind одного IEcaEventHandler, прямой generic callback; Base integration через test runtime. Обычный Fire layer-aware; технический caller-state Base bypass снова называется ForceFire.
- [x] NUnit/Unity EditMode tests для Execution и недостающие Unity metadata.
- [x] Systems v1: passive EcaSystem/Namespace, отдельные registries, EcaSystemConnector.Connect/Disconnect с prevalidation и локальным rollback; EventRegistry расширен non-generic Register/Unregister/Contains.
- [x] Standalone TimeSystem Core v1: независимая Unity assembly, ID timers, lifecycle events, PlayerLoop и Awaitable Wait; изолированные tests.
- [x] TimeSystem architecture refactor + первый Time/Eca adapter: aggregate lifecycle, typed event snapshots, семь Commands.
- [ ] После production composition / Sandbox рассмотреть external event adapters к IEcaEventEmitter.Fire<E>: C# events, callbacks, observables, polling, UnityEvent/InputAction и другие источники; список в Roadmap. Сейчас реализованы прямой Emitter API и конкретный TimeEcaAdapter.
- [ ] Рассмотреть StateBuilder/StateFactory позже, если последовательное расширение RuleState между слоями станет достаточно сложным, повторяемым или неудобным через Func. Execution использует Func<E, EcaExecutionGroupState, R>, Scope — Func<IEcaExecutionRuleState<E>, EcaScopeState, R>; отдельный builder не проектируется.
- [ ] Следующие слои согласовывать отдельными итерациями. FireEventCommand, Unity bridge, cancellation и Reset/Queue сейчас не реализованы.

Разделы ниже про завершённые Core/Core1 возможности не означают их наличие в Core2.

## Завершено

- [x] Execution v1.
- [x] Scope v1: собственные engines/registries, общая Rule по ссылке с независимыми GroupState/Limit, ScopeId/ParentScopeId, локальный Fire, каскадный Dispose без отмены Actions.
- [x] Base context refactor: явные TEventContext, TConditionContext, TActionContext.
- [x] Commands v1: registry, runner, bound API только для Action.
- [x] Execution integration: все Conditions до допуска по Limit/Overlap, затем создание контекста через new и однократный bind вне конструктора, затем Action.
- [x] Scope integration: общий CommandRunner при независимом состоянии scopes.

- [x] Нормальная тестовая инфраструктура Unity Test Framework + NUnit.
- [x] Полная миграция meaningful сценариев старого smoke test в EditMode.
- [x] GitHub Actions CI: один EditMode job, Unity 6000.3.19f1 / GameCI packageMode, копия пакета в _ci/EcaSystemsPackage; прошлый recovery-срез сообщает 31/31 passed.
- [x] Architecture checkpoint: EcaBaseEngine, общий Execution context contract, EcaScopeState и Scope context contract; runtime wiring ScopeState явно отложен.

## 1. Core1 Execution/Scope — реализовано, требуется review

- [x] Параллельная вертикаль Abstractions → Base → Execution → Scope в Runtime/Core1; старый Core и tests сохранены.
- [x] IEcaRuleRun вынесен в плоскую Abstractions; один extensible Base bridge обслуживает Base/Execution/Scope.
- [x] Canonical BaseEngine.Register валидирует текущую specialization до storage; standalone Execution/Scope имеют точные типизированные public Register.
- [x] Fired стал internal C# event; nested Fire синхронный/reentrant без очередей и public gameplay callback.
- [x] Execution reuse BaseEngine через ExecutionRuleRunner; non-generic Group/RuleExecution, EcaExecutionMode, admission после всех Conditions, наблюдение Action Task.
- [x] Scope reuse Execution + BaseEngine через ScopeRuleRunner; вертикальные State, shared EventRegistry, local runtime и local Fire, hierarchy/lifetime.
- [x] Unregister/Dispose не отменяют active Actions; повторная регистрация создаёт независимые GroupState/Limit.
- [x] Core1 Base regression и новые Execution/Scope tests: State, compatibility, Limit/Overlap, lifecycle/errors, несколько TEventState, nested Fire, isolation, shared EventRegistry, hierarchy, dispose и re-registration.
- [ ] Review public API Core1 Execution/Scope и общей модели bridge/wrapper. Core1 не объявлять окончательной заменой Runtime/Core до этого review.
- [ ] Отдельно обсудить Commands через RunnerContext и Systems; не начинать их реализацию автоматически.

## 2. Следующие архитектурные решения после review

- [ ] Согласовать будущий доступ к Commands из RunnerContext, сохраняя RuleState только данными; текущий Commands v1 старого Core не менять до решения.
- [ ] Согласовать ownership/exports Systems и интеграцию с общим EventRegistry и local runtime scopes.
- [ ] Возможный Group vertical layer между Execution и Scope остаётся только future mental-test в Roadmap.
- [ ] Не добавлять ActionRegistry/ConditionRegistry до реального use case; StateBuilder/ContextFactory не считать обязательным решением.

## 3. Архитектура Scope/Systems после Execution

- [ ] Развить две оси из Core1 Base (A временно); сохранить понятную структуру до MVP.
- [ ] При необходимости сравнить Core1 RuleRegistry selection с более широкими bindings/subscriptions, сохраняя декларативную Rule.
- [ ] Развить relation Event ↔ Rule/Condition/Action ↔ Execution на выбранном RuleRun seam.
- [ ] Спроектировать Fire Event/routing и ownership Scope поверх выбранного синхронного EventDispatcher, без циклических зависимостей.
- [ ] Рассматривать System как адаптер и ownership boundary, а не центральный runtime container Events/Commands/State. Не закреплять прежнее предположение, что отдельные Condition Queries не нужны.
- [x] Core1 ScopeState подключён в реальный Scope.Fire через ScopeRuleState и общий Base bridge; старый Core Scope wiring остаётся без изменений.
- [ ] Пересмотреть bound Commands с учётом RuleState = data / RunnerContext = infrastructure, сохранив текущий Commands v1 до решения.
- [ ] После этого вернуться к Events + Systems implementation: интеграционная поверхность Events, Commands, State, возможные Condition Queries; SystemState сначала предполагается глобальным.
- [ ] Использовать термин System, не Module. DI и размещение root engine остаются решением приложения.

## 4. TimeSystem

- [x] Standalone TimeSystem Core v1 в Runtime/Systems/Time/Core, без ECA dependencies; отдельные runtime/test assemblies.
- [x] Time/Eca adapter в Runtime/Systems/Time/Eca: passive descriptor, шесть Events и семь Commands, включая адаптацию Awaitable Wait.

## Внешние Systems

Продуктовые задачи внешних Systems ведутся только в их локальных Documentation~/ToDo.md. Общий план содержит Core2 integration contracts и инфраструктуру, не планы хранения/типов/истории конкретной System.

## 7. Интеграция Unity и удобство пакета

- [ ] Добавить необходимые обёртки и интеграционные тесты Scope/Systems.
- [ ] Проверить применение UPM-пакета в реальном игровом сценарии.

Cancellation, Reset, Queue, плавный Unregister и маршрутизация между scopes не обязательны для первого применения.

## Позже: удобство API и улучшение кода

Core1 имеет минимальные Base/empty shortcuts. Дальнейшая ergonomics/factory API учтена в [Roadmap.md](Roadmap.md). Универсальная фабрика не является выбранным решением: вертикальный State создаёт конкретный RuleRunner; дальнейшее расширение для Commands/Systems обсуждается после review.
