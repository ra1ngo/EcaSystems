# VariablesSystem — ToDo

## Итерация 1 — Core2 State payload + Variables Core

### EcaSystems State prerequisite

- [x] Добавить overload регистрации State resolver с opaque `object payload`.
- [x] Добавить overload `Resolve<T>(stateId, ruleState, payload)`.
- [x] Сохранить текущие no-payload overloads без изменения public behavior.
- [x] Зафиксировать validation при вызове payload/no-payload overload не того registration shape.
- [x] Payload не интерпретируется Core и передаётся exact resolver function.
- [x] Сохранить canonical connector validation exact registration/delegate semantics.
- [x] Добавить focused payload forwarding/validation tests.

### Variables Core

- [x] Создать standalone `Runtime/Systems/Variables/Core` без зависимости от Core2.
- [x] Использовать префикс `Eca` для concrete файлов и классов.
- [x] Реализовать `EcaVariablesSystem`.
- [x] Реализовать non-generic `EcaVariableDefinition`.
- [x] Реализовать non-generic `EcaVariable` с Definition / CurrentValue / OldValue.
- [x] Реализовать `int / float / bool / string`.
- [x] Реализовать explicit declaration.
- [x] Реализовать `Declare<T>/GetValue<T>/TryGetValue<T>/Contains`.
- [x] Реализовать `SetValue<T>`: same-value → no-op; changed value → Old=previous Current, Current=new, VariableChanged.
- [x] Реализовать `ForceSetValue<T>`: всегда Old=previous Current, Current=new, VariableChanged, включая same-value; validation не обходится.
- [x] Реализовать `SetCurrentValue<T>`: меняет только CurrentValue, OldValue не меняется, VariableChanged не испускается.
- [x] Duplicate declaration → exception.
- [x] Missing variable для обязательных операций → exception.
- [x] `TryGetValue<T>`: missing → false.
- [x] Wrong requested/set type → exception, включая TryGetValue.
- [x] Реализовать standalone VariableChanged event.
- [x] Event payload — отдельный `EcaVariableChanged`, явно mapped из `EcaVariable`.
- [x] `EcaVariableChanged` содержит Definition / CurrentValue / OldValue.
- [x] Добавить standalone tests.
- [x] Не добавлять Stores/Groups/Scopes/EntityGroups в этой итерации.
- [x] Не добавлять Save/Load, enum, history или reactivity.

## Итерация 2 — Stores V2 (выполнена)

- [x] EcaVariableStore как единый owner Variables, без специальных Scope/Group/EntityGroup классов.
- [x] Перенести Variable API и VariableChanged в Store, сохранив все semantics V1.
- [x] EcaVariablesSystem как manager: CreateStore/ContainsStore/GetStore/TryGetStore.
- [x] Удалить flat System API без proxies и implicit/default/global Store.
- [x] Stable StoreId, globally unique per System, StringComparer.Ordinal, не path.
- [x] Immutable optional ParentId, несколько roots и forest/tree.
- [x] Parent должен существовать; self-parent отклоняется без mutation.
- [x] Parent означает только ownership; lookup variables строго local, без inheritance/fallback/override.
- [x] В V2 были local events без bubbling; Core V3 ниже заменяет это subtree/aggregate semantics.
- [x] Независимость Store/Variable IDs от EcaScope/RuleId; mapping остаётся будущей adapter policy.
- [x] Мигрировать 16 прежних Variable test cases без ослабления assertions; добавить 14 Store cases.
- [x] Unity 6000.5.6f1: focused 14/14, Variables assembly 30/30, Core2 280/280, EditMode 494/494.
- [ ] Store lifecycle events создания/удаления/перемещения спроектировать отдельно. VariableChanged bubbling реализован в Core V3.

Reparent, Copy, Templates, hierarchical lookup/inheritance/override остаются будущими возможностями [Roadmap](Roadmap.md), не реализованы в V2.

## Итерация 3 — Core V3 (выполнена)

- [x] EcaVariableData как единственный live state, StoreId/Definition/Current/Old.
- [x] EcaVariable facade с direct Get/Set/ForceSet/SetCurrent и internal EcaVariableController.
- [x] Store proxies используют тот же facade/behavior; GetVariable/TryGetVariable возвращают exact instance.
- [x] Definition immutable и без StoreId; StoreId в Data/facade/Changed/Snapshot.
- [x] Отдельные internal EcaVariableChangedMapper и EcaVariableSnapshotMapper.
- [x] Immutable EcaVariableSnapshot, EcaVariableStoreState, EcaVariableSubtreeState и read-only point-in-time collections.
- [x] IsRoot, Parent/TryParent, Children/Siblings/Subtree; roots являются siblings.
- [x] Navigation через authoritative System dictionary, без дополнительных indexes/graphs.
- [x] Synchronous bottom-up bubbling source → ancestors → System без public event wiring.
- [x] Store.VariableChanged subtree-scoped, source StoreId сохраняется; System aggregate работает для будущих Stores.
- [x] Reentrancy, exact exception propagation, no rollback/isolation и local Variable lookup сохранены.
- [x] 23 новых focused cases; прежние 30 cases сохранены, две parent-event expectations адаптированы к V3.
- [x] Unity 6000.5.6f1: focused 23/23, Variables 53/53, Core2 280/280, EditMode 517/517.

## Следующая итерация — Variables / ECA adapter

- [ ] Добавить `Runtime/Systems/Variables/Eca`.
- [ ] Согласовать read-only State contract.
- [ ] Использовать opaque State payload для explicit container/entity selection при необходимости.
- [ ] Согласовать общий ECA `VariableChanged` event payload.
- [ ] Добавить минимальный SetValue Command.
- [ ] Не требовать соответствующих EcaScope/Variables containers с обеих сторон.

## Следом

- [ ] Save/Load VariablesSystem отдельной итерацией.
- [ ] Enum после решения persistence contract.
- [ ] History вместо одиночного OldValue — future feature.
- [ ] Reactive/computed/watch/collections сюда не добавлять.
