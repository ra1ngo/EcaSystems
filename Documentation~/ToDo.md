# EcaSystems — необходимые этапы

Здесь только работа до первого полноценного применения в играх. Execution v1, Scope v1, Base context refactor и Commands v1 завершены. Актуальные решения — в [Context.md](Context.md), необязательные будущие возможности — в [Roadmap.md](Roadmap.md). Документы ведутся на русском языке.

## Core2 — checkpoint 2026-09-15

- [x] Core2/Base завершён; API и архитектура не меняются в Execution-итерации.
- [x] Core2/Layers/Execution: generic Group Check/Run, composition Runtime, ALL CONDITIONS → ALL EXECUTIONS, поздний admission, immediate/reentrant Fire, lifetime Limit/Overlap, lifecycle и rollback/unregister.
- [x] Scope: EcaScopeRuntime manager + isolated EcaScope, независимые Execution graphs, hierarchy/ParentScopeId, local Fire, recursive Dispose и ScopeId reuse без отмены Actions; enrichment через Func<IEcaExecutionRuleState<E>, EcaScopeState, R> сохранён.
- [x] NUnit/Unity EditMode tests для Execution и недостающие Unity metadata.
- [ ] Рассмотреть StateBuilder/StateFactory позже, если последовательное расширение RuleState между слоями станет достаточно сложным, повторяемым или неудобным через Func. Execution использует Func<E, EcaExecutionGroupState, R>, Scope — Func<IEcaExecutionRuleState<E>, EcaScopeState, R>; отдельный builder не проектируется.
- [ ] Следующие слои согласовывать отдельными итерациями. Core2 Commands integration, Fire Event command/receiver, EventDispatcher/UnityEvent, cancellation и Reset/Queue сейчас не реализованы.

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

- [ ] После архитектуры Systems реализовать TimeSystem / EcaTimeSystem и EcaWaitCommand. В текущую итерацию они не входят.

## 5. Global State / переменные

- [ ] Спроектировать как отдельную System, а не встроенную возможность Base.

## 6. Scope-aware / hierarchical SystemState

- [ ] После Global State/Variables спроектировать global → child → grandchild/local scopes: inheritance, lookup и override semantics. Сейчас SystemState предполагается глобальным; scoped state не реализован. Это отдельная возможность, не EcaScopeState с ScopeId.

## 7. Интеграция Unity и удобство пакета

- [ ] Добавить необходимые обёртки и интеграционные тесты Scope/Systems.
- [ ] Проверить применение UPM-пакета в реальном игровом сценарии.

Cancellation, Reset, Queue, плавный Unregister и маршрутизация между scopes не обязательны для первого применения.

## Позже: удобство API и улучшение кода

Core1 имеет минимальные Base/empty shortcuts. Дальнейшая ergonomics/factory API учтена в [Roadmap.md](Roadmap.md). Универсальная фабрика не является выбранным решением: вертикальный State создаёт конкретный RuleRunner; дальнейшее расширение для Commands/Systems обсуждается после review.
