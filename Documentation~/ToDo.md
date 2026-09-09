# EcaSystems — необходимые этапы

Здесь только работа до первого полноценного применения в играх. Execution v1, Scope v1, Base context refactor и Commands v1 завершены. Актуальные решения — в [Context.md](Context.md), необязательные будущие возможности — в [Roadmap.md](Roadmap.md). Документы ведутся на русском языке.

## Завершено

- [x] Execution v1.
- [x] Scope v1: собственные engines/registries, общая Rule по ссылке с независимыми GroupState/Limit, ScopeId/ParentScopeId, локальный Fire, каскадный Dispose без отмены Actions.
- [x] Base context refactor: явные TEventContext, TConditionContext, TActionContext.
- [x] Commands v1: registry, runner, bound API только для Action.
- [x] Execution integration: все Conditions до допуска по Limit/Overlap, затем создание контекста через new и однократный bind вне конструктора, затем Action.
- [x] Scope integration: общий CommandRunner при независимом состоянии scopes.

## 1. Архитектура Systems — следующий этап

- [ ] Спроектировать IEcaSystem как пассивный ECA-адаптер / набор exports: Events, Commands и State.
- [ ] Не экспортировать Actions. Отдельные system-specific Conditions пока не нужны: Conditions принадлежат Rules и читают state через context.
- [ ] Спроектировать SystemsState.
- [ ] Спроектировать универсальное создание/hydration ConditionContext и ActionContext, чтобы Systems мог добавлять state/capabilities без повторного переписывания Execution/Scope API. Снять текущее ограничение точной пары execution contexts.
- [ ] Пересмотреть Commands API на реальных сценариях Systems.
- [ ] Использовать термин System, не Module. DI и размещение root engine остаются решением приложения вне framework.

## 2. TimeSystem

- [ ] После архитектуры Systems реализовать TimeSystem / EcaTimeSystem и EcaWaitCommand. В текущую итерацию они не входят.

## 3. Global State / переменные

- [ ] Спроектировать как отдельную System, а не встроенную возможность Base.

## 4. Интеграция Unity и удобство пакета

- [ ] Добавить необходимые обёртки и интеграционные тесты Scope/Systems.
- [ ] Проверить применение UPM-пакета в реальном игровом сценарии.

Cancellation, Reset, Queue, плавный Unregister и маршрутизация между scopes не обязательны для первого применения.
