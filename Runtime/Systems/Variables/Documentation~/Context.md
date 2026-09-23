# VariablesSystem — Context

## Назначение

`VariablesSystem` — standalone runtime-система хранения типизированных игровых переменных по stable string ID.

Система должна оставаться независимой от EcaSystems Core2 и позже может быть вынесена в отдельный репозиторий.

## Размещение

Текущая структура внутри EcaSystems:

```text
Runtime/Systems/Variables/
├── Core/
├── Eca/
└── Documentation~/
```

`Core` не зависит от ECA. `Eca` является адаптером/экспортом в EcaSystems.

## Нейминг

Утверждён основной нейминг: `VariablesSystem`.

Папка системы: `Variables`.

Рабочий ECA system id: `variables`.

## MVP Core

Базовая модель строится вокруг explicit declarations и stable IDs.

Начальные поддерживаемые value types:
- `int`
- `float`
- `bool`
- `string`

`enum` отложен до обсуждения persistence/SaveLoad.

ID является identity переменной, type является строгим contract.

Базовое направление API:
- Declare
- Get
- TryGet
- Set
- Contains

Конкретные signatures ещё не согласованы.

## ECA integration

State используется для чтения, Commands — для mutation, Events — для изменений.

State export должен быть read-oriented и не обязан отдавать сам `VariablesSystem`.

Предварительное направление:
- State: `variables.state`
- Event: один общий `VariableChanged`
- Command: как минимум `Set`

Отдельные `IntVariableChanged`, `FloatVariableChanged` и т.п. не нужны.

Payload события изменения должен содержать полную информацию о переменной/definition и old/current values. Точная форма snapshot/definition ещё обсуждается.

## State payload в EcaSystems

Для ECA StateResolver требуется возможность передавать opaque payload произвольного типа как `object`.

Payload нужен, например, чтобы передать Variables-specific selector вроде `groupVarId`, store id или entity id.

EcaSystems не должен интерпретировать этот payload; он только передаётся registered state resolver function.

Точный API StateResolver с payload ещё не согласован.

## Независимость partitioning от ECA

VariablesSystem не должен требовать соответствия собственных partition IDs структурам ECA.

В частности:
- Variables storage/store может существовать без EcaScope.
- EcaScope может существовать без Variables storage/store.
- Variables entity/store/group id не обязан совпадать с `ScopeId`.
- Variables entity id не обязан совпадать с ECA `RuleId`.
- Mapping между ECA coordinates и Variables coordinates является политикой ECA adapter/resolver.

Например ECA adapter может при необходимости использовать:
- `ScopeId` как внешний selector;
- `RuleId` как entity id;
- explicit payload как store/group/entity id;
- комбинацию этих значений.

Ни одно соответствие не является обязательным контрактом VariablesSystem.

## Groups / Stores

Группы переменных нужны, но не входят в первую Core-итерацию.

Также рассматривается более общий concept отдельного store/partition, который может заменить жёсткое разделение на scope/group и использоваться для разных доменных случаев.

Пока не зафиксировано:
- нужен ли отдельный Scope concept внутри VariablesSystem;
- нужен ли отдельный Group concept;
- либо достаточно общего Store/Partition abstraction;
- как соотносятся Store, Group и Entity.

Hierarchical inheritance/fallback не реализуется в первой итерации.

## Save/Load

Save/Load — отдельная следующая задача после стабилизации базового Variables API.

Actual variables принадлежат VariablesSystem; EcaStateRegistry не владеет и не сериализует их.
