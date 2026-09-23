# VariablesSystem — ToDo

## Итерация 1 — Core API

- [ ] Согласовать public API `VariablesSystem`.
- [ ] Согласовать declaration model и concrete variable representation.
- [ ] Зафиксировать exact type semantics.
- [ ] Реализовать `int / float / bool / string`.
- [ ] Реализовать Declare/Get/TryGet/Set/Contains в согласованной форме.
- [ ] Согласовать semantics duplicate declaration, wrong type и missing ID.
- [ ] Согласовать default/current/old value representation.
- [ ] Добавить standalone tests.
- [ ] Не добавлять ECA dependency.

## Итерация 2 — Groups / Stores

- [ ] Спроектировать grouping/partitioning независимо от EcaScope.
- [ ] Сравнить отдельные Scope + Group concepts с единым Store/Partition concept.
- [ ] Определить выбор store/group/entity по explicit ID.
- [ ] Не добавлять hierarchy/inheritance без отдельного решения.
- [ ] Добавить tests независимых stores/groups.

## Итерация 3 — ECA adapter

- [ ] Добавить `Runtime/Systems/Variables/Eca`.
- [ ] Согласовать read-only State contract.
- [ ] Согласовать общий `VariableChanged` event payload.
- [ ] Добавить минимальный Set Command.
- [ ] Согласовать mapping ECA ScopeId/RuleId → Variables store/group/entity только как adapter policy.
- [ ] Не требовать наличия соответствующих scopes/groups с обеих сторон.

## EcaSystems prerequisite

- [ ] Спроектировать opaque `object` payload для StateResolver.
- [ ] Payload должен передаваться resolver function без интерпретации Core.
- [ ] Проверить backward-compatible API shape и validation.
- [ ] Покрыть payload forwarding tests.

## Следом

- [ ] Save/Load VariablesSystem отдельной итерацией.
- [ ] Enum после решения persistence contract.
- [ ] Scope/store hierarchy, inheritance и override — отдельная будущая итерация.
- [ ] Reactive/computed/watch/collections сюда не добавлять.
