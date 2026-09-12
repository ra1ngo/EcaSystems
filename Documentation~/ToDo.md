# EcaSystems — необходимые этапы

Здесь только работа до первого полноценного применения в играх. Execution v1, Scope v1, Base context refactor и Commands v1 завершены. Актуальные решения — в [Context.md](Context.md), необязательные будущие возможности — в [Roadmap.md](Roadmap.md). Документы ведутся на русском языке.

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

## 1. Validation Core1 Base — текущая итерация

- [x] Параллельный prototype в Runtime/Core1: временная вертикальная ось A, плоские A/Abstractions, Base и Utils. Старый Runtime/Core и прежние tests сохранены.
- [x] Event declaration-only; Rule = 1 Event + optional Condition + 1 Action; Condition/Action остаются программными executable contracts.
- [x] RuleState только с EventState; RunnerContext — отдельная infrastructure. Независимые ConditionRunner/ActionRunner, без factories, lifecycle/status и ExecutionState.
- [x] EventRegistry + обычное синхронное C# Fired event, immediate/reentrant Fire без queue/pendingEvents.
- [x] RuleRun/type-erasure seam без dynamic/reflection execution; один BaseEngine работает с разными TEventState и владеет ALL CONDITIONS → ALL ACTIONS.
- [x] Отдельные Core1 tests: generic/empty payload, один State в двух ролях, фазовый порядок, nested/reentrant Fire, ошибки, snapshot, async semantics и замена runner из другой assembly.
- [ ] Обсудить результаты prototype и API перед следующим слоем. Технические детали и проверки — в Context и кратком recovery.

## 2. Execution reuse of BaseEngine — после Base

- [ ] Использовать другой RuleRunner/RuleState поверх того же EcaBaseEngine, не копировать Fire pipeline.
- [ ] Определить расширенные данные State, group/admission Overlap/Limit после всех Conditions, наблюдение Task, lifetime/unregister и infrastructure RunnerContext.
- [ ] В новом Execution переименовать EcaRunMode в EcaExecutionMode; не переносить mode в Base.
- [ ] Обсудить возможный отдельный Group layer между Execution и Scope (Roadmap), без реализации заранее.
- [ ] Не добавлять ActionRegistry/ConditionRegistry до реального use case; StateBuilder/ContextFactory не считать обязательным решением.

## 3. Архитектура Scope/Systems после Execution

- [ ] Развить две оси из Core1 Base (A временно); сохранить понятную структуру до MVP.
- [ ] При необходимости сравнить Core1 RuleRegistry selection с более широкими bindings/subscriptions, сохраняя декларативную Rule.
- [ ] Развить relation Event ↔ Rule/Condition/Action ↔ Execution на выбранном RuleRun seam.
- [ ] Спроектировать Fire Event/routing и ownership Scope поверх выбранного синхронного EventDispatcher, без циклических зависимостей.
- [ ] Рассматривать System как адаптер и ownership boundary, а не центральный runtime container Events/Commands/State. Не закреплять прежнее предположение, что отдельные Condition Queries не нужны.
- [ ] Сохранить расширение Context по слоям; решить создание и выбор расширенных контекстов для ScopeState. Текущий Scope.Fire поддерживает только точные Execution role types; контракт Scope ещё не означает runtime wiring.
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

Core1 имеет минимальные Base/empty shortcuts. Дальнейшая ergonomics/factory API учтена в [Roadmap.md](Roadmap.md). Универсальная фабрика не является выбранным решением: новый State создаёт конкретный RuleRunner; расширение данных и infrastructure обсуждается в этапах 2–3.
