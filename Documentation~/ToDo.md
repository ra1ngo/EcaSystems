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

## 1. Архитектурный refactor перед Systems — следующий этап

- [ ] Определить две оси: layers/features (Base, Commands, Execution, Scope, Systems, возможный Inspection/Debug) × самостоятельные capabilities (Events, Conditions, Actions, Commands, State, Rules, Context, Execution). Это не строгая линейная Clean Architecture; сохранить понятную структуру до MVP.
- [ ] Пересмотреть Rule-centric runtime: сохранить Rule как декларативную/authoring композицию, сравнить RuleSelector с Event → bindings/subscriptions.
- [ ] Определить relation Event ↔ Rule/Condition/Action ↔ Execution.
- [ ] Спроектировать Fire Event/routing без запутанного ownership и циклических зависимостей; EventDispatcher/EventBus/EventRuntime пока не выбирать.
- [ ] Рассматривать System как адаптер и ownership boundary, а не центральный runtime container Events/Commands/State. Не закреплять прежнее предположение, что отдельные Condition Queries не нужны.
- [ ] Сохранить расширение Context по слоям; решить создание и выбор расширенных контекстов для ScopeState. Текущий Scope.Fire поддерживает только точные Execution role types; контракт Scope ещё не означает runtime wiring.
- [ ] После следующей архитектурной итерации пересмотреть bound Commands как service внутри data context совместно с context enrichment/hydration, сохранив текущий Commands v1 до решения.
- [ ] После этого вернуться к Events + Systems implementation: интеграционная поверхность Events, Commands, State, возможные Condition Queries; SystemState сначала предполагается глобальным.
- [ ] Использовать термин System, не Module. DI и размещение root engine остаются решением приложения.

## 2. TimeSystem

- [ ] После архитектуры Systems реализовать TimeSystem / EcaTimeSystem и EcaWaitCommand. В текущую итерацию они не входят.

## 3. Global State / переменные

- [ ] Спроектировать как отдельную System, а не встроенную возможность Base.

## 4. Scope-aware / hierarchical SystemState

- [ ] После Global State/Variables спроектировать global → child → grandchild/local scopes: inheritance, lookup и override semantics. Сейчас SystemState предполагается глобальным; scoped state не реализован. Это отдельная возможность, не EcaScopeState с ScopeId.

## 5. Интеграция Unity и удобство пакета

- [ ] Добавить необходимые обёртки и интеграционные тесты Scope/Systems.
- [ ] Проверить применение UPM-пакета в реальном игровом сценарии.

Cancellation, Reset, Queue, плавный Unregister и маршрутизация между scopes не обязательны для первого применения.

## Позже: удобство API и улучшение кода

Rule shortcut/factory API и универсальный ContextFactory/hydration учтены в [Roadmap.md](Roadmap.md). Универсальная фабрика не является заранее выбранным решением для ScopeState; архитектурный вопрос расширения контекстов входит в этап 1.
