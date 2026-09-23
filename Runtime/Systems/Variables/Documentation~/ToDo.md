# VariablesSystem — ToDo

## Итерация 1 — Core2 State payload + Variables Core

### EcaSystems State prerequisite

- [ ] Добавить overload регистрации State resolver с opaque `object payload`.
- [ ] Добавить overload `Resolve<T>(stateId, ruleState, payload)`.
- [ ] Сохранить текущие no-payload overloads без изменения public behavior.
- [ ] Зафиксировать validation при вызове payload/no-payload overload не того registration shape.
- [ ] Payload не интерпретируется Core и передаётся exact resolver function.
- [ ] Сохранить canonical connector validation exact registration/delegate semantics.
- [ ] Добавить focused payload forwarding/validation tests.

### Variables Core

- [ ] Создать standalone `Runtime/Systems/Variables/Core` без зависимости от Core2.
- [ ] Использовать префикс `Eca` для concrete файлов и классов.
- [ ] Реализовать `EcaVariablesSystem`.
- [ ] Реализовать non-generic `EcaVariableDefinition`.
- [ ] Реализовать non-generic `EcaVariable` с Definition / CurrentValue / OldValue.
- [ ] Реализовать `int / float / bool / string`.
- [ ] Реализовать explicit declaration.
- [ ] Реализовать Declare/Get/TryGet/Set/Contains в согласованной generic access форме.
- [ ] Duplicate declaration → exception.
- [ ] Missing variable для обязательных операций → exception.
- [ ] Wrong requested/set type → exception.
- [ ] Реализовать standalone VariableChanged event.
- [ ] Event payload — отдельный `EcaVariableChanged`, явно mapped из `EcaVariable`.
- [ ] `EcaVariableChanged` содержит Definition / CurrentValue / OldValue.
- [ ] Добавить standalone tests.
- [ ] Не добавлять Stores/Groups/Scopes/EntityGroups в этой итерации.
- [ ] Не добавлять Save/Load, enum, history или reactivity.

## Итерация 2 — древовидные containers

- [ ] Спроектировать один общий container concept вместо отдельных Store/Group/Scope/EntityGroup сущностей.
- [ ] Рабочий naming: Store; подтвердить или заменить при проектировании.
- [ ] Stable container ID.
- [ ] Optional ParentId.
- [ ] Дерево вложенных containers.
- [ ] Container lifecycle/API обсуждается отдельно.
- [ ] Не связывать container existence с EcaScope.
- [ ] Не связывать container ID с RuleId/entityId автоматически.
- [ ] Mapping ECA coordinates → Variables container остаётся adapter policy.
- [ ] Отдельно решить hierarchy lookup/inheritance/override semantics.
- [ ] Событие изменения container/store спроектировать позже.

## Итерация 3 — Variables / ECA adapter

- [ ] Добавить `Runtime/Systems/Variables/Eca`.
- [ ] Согласовать read-only State contract.
- [ ] Использовать opaque State payload для explicit container/entity selection при необходимости.
- [ ] Согласовать общий ECA `VariableChanged` event payload.
- [ ] Добавить минимальный Set Command.
- [ ] Не требовать соответствующих EcaScope/Variables containers с обеих сторон.

## Следом

- [ ] Save/Load VariablesSystem отдельной итерацией.
- [ ] Enum после решения persistence contract.
- [ ] History вместо одиночного OldValue — future feature.
- [ ] Reactive/computed/watch/collections сюда не добавлять.
