# EcaSystems — необходимые этапы

Здесь только работа до первого полноценного применения в играх. Execution v1, Scope v1, Base context refactor и Commands v1 завершены. Актуальные решения — в [Context.md](Context.md), необязательные будущие возможности — в [Roadmap.md](Roadmap.md). Документы ведутся на русском языке.

## Завершено

- [x] Execution v1.
- [x] Scope v1: собственные engines/registries, общая Rule по ссылке с независимыми GroupState/Limit, ScopeId/ParentScopeId, локальный Fire, каскадный Dispose без отмены Actions.
- [x] Base context refactor: явные TEventContext, TConditionContext, TActionContext.
- [x] Commands v1: registry, runner, bound API только для Action.
- [x] Execution integration: все Conditions до bind Commands и Actions, контексты через new.
- [x] Scope integration: общий CommandRunner при независимом состоянии scopes.

## 1. Архитектура Systems — следующий этап

- [ ] Спроектировать интеграцию независимых Systems после Scope.
- [ ] Определить предоставление Events, Conditions, Commands и расширений состояния контекста.
- [ ] Использовать термин System, не Module.
- [ ] Пересмотреть Commands API при необходимости и спроектировать SystemsState.

## 2. TimeSystem

- [ ] После архитектуры Systems реализовать TimeSystem / EcaTimeSystem и EcaWaitCommand. В текущую итерацию они не входят.

## 3. Global State / переменные

- [ ] Спроектировать как отдельную System, а не встроенную возможность Base.

## 4. Интеграция Unity и удобство пакета

- [ ] Добавить необходимые обёртки и интеграционные тесты Scope/Systems.
- [ ] Проверить применение UPM-пакета в реальном игровом сценарии.

Cancellation, Reset, Queue, плавный Unregister и маршрутизация между scopes не обязательны для первого применения.
