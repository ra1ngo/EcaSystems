# EcaSystems — актуальный framework план

Production source of truth — Runtime/Core2. Runtime/Core и Runtime/Core1 — historical/reference; прежние решения сохранены в [Architecture](Architecture/) и recovery docs, не являются открытым production plan. Актуальные контракты — [Context](Context.md), возможное развитие — [Roadmap](Roadmap.md).

## Завершено в Core2

- [x] Base / Execution / Scope: generic RuleState, ALL CONDITIONS → ALL EXECUTIONS, late admission, immediate/reentrant local Fire, isolated Scope graphs и recursive lifetime без cancellation Actions.
- [x] Nullable раздельные ConditionContext/ActionContext конкретного Fire, Scope-owned transport EventEmitter, technical ForceFire.
- [x] Commands с явными RuleState/context без Bind; StateResolver по stable ID, exact declared type и opaque payload shapes.
- [x] EcaSystemsRuntime composition root, passive System exports, transactional export Connector, class/delegate CreateRule и shared checker/runner.
- [x] Registry GetSnapshot convention: Core2 + standalone System registries, read-only point-in-time membership и focused tests.
- [x] Unity EditMode инфраструктура и GameCI packageMode; rationale local/dev 6000.5.6f1 / CI 6000.3.19f1 сохранён в [Testing](Testing.md).

## Следующие задачи

- [ ] Sandbox / PlayMode end-to-end: проверить UPM в реальном игровом сценарии и удобство composition.
- [ ] External event adapters к IEcaEventEmitter: C# events, callbacks, observables, polling, UnityEvent/InputAction по конкретным сценариям.
- [ ] Project cleanup/consolidation: определить судьбу пустой EcaSystems.Unity assembly, не смешивать historical layers с production.
- [ ] Unity integration/authoring helpers без переноса domain behavior внешних Systems в Core.
- [ ] Signals / FireEvent / routing — только после конкретного сценария и отдельного согласования.
- [ ] Дальнейший Roadmap по практическому приоритету: context composition, diagnostics, identity/registry consistency.

Планы конкретных Systems находятся в [Time ToDo](../Runtime/Systems/Time/Documentation~/ToDo.md) и [Variables ToDo](../Runtime/Systems/Variables/Documentation~/ToDo.md). Эта итерация не добавляет Scope/System lifecycle coordination или automatic bindings. Cancellation, Reset/Queue, cross-scope routing и generic adapter framework не обязательны для первого применения.
