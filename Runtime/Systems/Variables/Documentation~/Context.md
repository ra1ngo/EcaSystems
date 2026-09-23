# VariablesSystem — Context

## Назначение

`VariablesSystem` — standalone runtime-система хранения типизированных игровых переменных по stable string ID.

Система проектируется как самодостаточная и не должна зависеть от EcaSystems Core2. При этом она целенаправленно создаётся для удобной интеграции с EcaSystems и позже может быть вынесена в отдельный репозиторий.

## Размещение

Текущая структура внутри EcaSystems:

```text
Runtime/Systems/Variables/
├── Core/
├── Eca/
├── Documentation~/
└── README.md
```

`Core` не зависит от ECA. `Eca` является адаптером/экспортом в EcaSystems.

## Нейминг

Название системы: `VariablesSystem`.

Папка системы: `Variables`.

Для concrete файлов и классов используется префикс `Eca`, даже если класс находится в standalone Core. Это сознательный namespace/name isolation от встроенных типов и сторонних библиотек.

Примеры:
- `EcaVariablesSystem`
- `EcaVariable`
- `EcaVariableDefinition`
- `EcaVariableChanged`

Префикс не означает зависимость Core от EcaSystems runtime.

Рабочий ECA system id: `variables`.

## Первая Core-итерация

Первая версия плоская: Store/Group/Scope/EntityGroup пока не реализуются.

Базовая модель строится вокруг explicit declarations и stable IDs.

Начальные поддерживаемые value types:
- `int`
- `float`
- `bool`
- `string`

`enum` отложен до обсуждения persistence/SaveLoad.

ID является identity переменной, type является строгим contract.

Duplicate declaration, missing variable и wrong requested/set type должны давать exception.

Базовое направление API:
- Declare
- Get
- TryGet
- Set
- Contains

Конкретные signatures согласуются перед реализацией.

## Модель Variable

Первая модель intentionally non-generic.

`EcaVariable` содержит:
- `EcaVariableDefinition Definition`
- `object CurrentValue`
- `object OldValue`

`EcaVariableDefinition` содержит immutable declaration metadata, как минимум ID, declared value Type и default value.

OldValue хранит непосредственно предыдущее значение. В будущем вместо одного OldValue может появиться History; сейчас History не реализуется.

Generic access допустим на API-методах `Declare<T>/Get<T>/TryGet<T>/Set<T>`, но сами `EcaVariable` и `EcaVariableDefinition` в MVP не generic.

Отдельные interfaces для Variable/Definition в MVP не требуются без конкретной необходимости.

## Change event

Standalone Core должен иметь одно общее событие изменения variable.

Typed events вроде `IntVariableChanged` / `BoolVariableChanged` не нужны.

Payload события — отдельный concrete `EcaVariableChanged`, а не сам `EcaVariable`.

`EcaVariableChanged` по данным полностью соответствует snapshot `EcaVariable`:
- Definition
- CurrentValue
- OldValue

Между `EcaVariable` и `EcaVariableChanged` делается явный mapping/copy. Это физически разные сущности, чтобы runtime-owned mutable Variable не выдавался наружу как event payload.

Событие изменения Store/partition будет спроектировано позже.

## Будущие Stores / partitions

Store, Group, Scope и EntityGroup рассматриваются как один и тот же общий container/partition concept и не реализуются в первой итерации.

Следующая отдельная итерация должна спроектировать древовидные containers:
- каждый container имеет stable ID;
- container может иметь `ParentId`;
- containers могут вкладываться друг в друга и образуют дерево.

Рабочее имя `Store` пока допустимо: вложенность store сама по себе не считается проблемой. Финальный naming будет подтверждён при проектировании этой итерации.

Не создавать отдельные VariablesScope / VariablesGroup / EntityGroup abstractions без реального различия semantics.

Hierarchical lookup/inheritance/fallback между parent/child отдельно не считается автоматически выбранной семантикой только из-за наличия дерева.

## Независимость partitioning от ECA

VariablesSystem не должен требовать соответствия собственных будущих containers структурам ECA.

В частности:
- Variables container может существовать без EcaScope.
- EcaScope может существовать без Variables container.
- Variables container ID не обязан совпадать с `ScopeId`.
- Variables entity/container ID не обязан совпадать с ECA `RuleId`.
- Mapping между ECA coordinates и Variables coordinates является политикой ECA adapter/resolver.

ECA adapter позже может использовать:
- `ScopeId`;
- `RuleId` как entity selector;
- explicit State payload;
- комбинацию этих значений.

Ни одно соответствие не является обязательным контрактом VariablesSystem.

## Opaque State payload в EcaSystems

Core2 State требуется расширить отдельным opaque payload произвольного типа `object`.

Предпочтительное направление — overloads, сохраняя существующий API:

```csharp
Register<T>(string id, Func<IEcaRuleState, T> resolve);
Register<T>(string id, Func<IEcaRuleState, object, T> resolve);

Resolve<T>(string stateId, IEcaRuleState ruleState);
Resolve<T>(string stateId, IEcaRuleState ruleState, object payload);
```

Payload не интерпретируется EcaSystems и передаётся exact resolver function.

Точные validation semantics для mismatch между registration shape и Resolve overload нужно зафиксировать перед реализацией.

Payload позже позволит ECA adapter передавать Variables-specific selector вроде container/store/group/entity id без обязательного совпадения с EcaScope/RuleId.

## ECA integration

State используется для чтения, Commands — для mutation, Events — для изменений.

State export должен быть read-oriented и не обязан отдавать сам `EcaVariablesSystem`.

Предварительное направление:
- State: `variables.state`
- Event: один общий `VariableChanged`
- Command: как минимум `Set`

ECA adapter не входит в первую Variables Core-итерацию.

## Save/Load

Save/Load — отдельная следующая задача после стабилизации базового Variables API и container model.

Actual variables принадлежат VariablesSystem; EcaStateRegistry не владеет и не сериализует их.
