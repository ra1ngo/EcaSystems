# VariablesSystem — Context

## Назначение

`VariablesSystem` — standalone runtime-система хранения типизированных игровых переменных по stable string ID.

Stores V2 реализован как самодостаточная assembly EcaSystems.Variables без ссылок на EcaSystems Core2 или Unity. При этом она целенаправленно создаётся для удобной интеграции с EcaSystems и позже может быть вынесена в отдельный репозиторий.

## Размещение

Текущая структура внутри EcaSystems:

```text
Runtime/Systems/Variables/
├── Core/
├── Eca/
├── Documentation~/
└── README.md
```

`Core` не зависит от ECA. `Eca` зарезервирован под будущий адаптер, production code отсутствует.

## Нейминг

Название системы: `VariablesSystem`.

Папка системы: `Variables`.

Для concrete файлов и классов используется префикс `Eca`, даже если класс находится в standalone Core. Это сознательный namespace/name isolation от встроенных типов и сторонних библиотек.

Примеры:
- `EcaVariablesSystem`
- `EcaVariableStore`
- `EcaVariable`
- `EcaVariableDefinition`
- `EcaVariableChanged`

Префикс не означает зависимость Core от EcaSystems runtime.

Рабочий ECA system id: `variables`.

## Variable semantics после Stores V2

EcaVariablesSystem — Store manager. EcaVariableStore — owner локальных Variables; прежний flat API удалён из System без proxies и implicit/default/global Store. Каждый Store сохраняет Variable semantics V1.

Базовая модель строится вокруг explicit declarations и stable IDs.

Начальные поддерживаемые value types:
- `int`
- `float`
- `bool`
- `string`

`enum` отложен до обсуждения persistence/SaveLoad.

ID является identity переменной, type является строгим contract.

Duplicate declaration, missing variable и wrong requested/set type должны давать exception.

Реализованный Variable API EcaVariableStore:
- Declare
- GetValue
- TryGetValue
- Contains
- SetValue
- ForceSetValue
- SetCurrentValue

Mutation semantics:
- `SetValue<T>(id, value)` — обычная tracked-запись только при фактическом изменении значения. Если новое значение равно CurrentValue, операция является no-op: OldValue не меняется и VariableChanged не испускается. Если значение отличается, OldValue получает прежний CurrentValue, затем CurrentValue меняется и испускается VariableChanged.
- `ForceSetValue<T>(id, value)` — безусловная tracked-запись. Всегда выполняет OldValue = CurrentValue, затем CurrentValue = value и испускает VariableChanged, даже если значение не изменилось. Force не обходит ID/type validation.
- `SetCurrentValue<T>(id, value)` — прямое присваивание только CurrentValue. OldValue не меняется и VariableChanged не испускается.

Пример:

```text
Old = 0, Current = 0

SetValue(50)
→ Old = 0, Current = 50, VariableChanged

SetValue(50)
→ Old = 0, Current = 50, no-op

ForceSetValue(50)
→ Old = 50, Current = 50, VariableChanged

SetCurrentValue(100)
→ Old = 50, Current = 100, no event
```

Разделение намеренно сохраняет три разных смысла: обычное изменение, принудительный tracked write и direct current assignment. В будущем это должно естественно расширяться в History.

## Модель Variable

Первая модель intentionally non-generic.

`EcaVariable` содержит:
- `EcaVariableDefinition Definition`
- `object CurrentValue`
- `object OldValue`

`EcaVariableDefinition` содержит immutable declaration metadata, как минимум ID, declared value Type и default value.

OldValue хранит непосредственно предыдущее tracked значение. В будущем вместо одного OldValue может появиться History; сейчас History не реализуется.

Generic access допустим на API-методах `Declare<T>/GetValue<T>/TryGetValue<T>/SetValue<T>/ForceSetValue<T>/SetCurrentValue<T>`, но сами `EcaVariable` и `EcaVariableDefinition` в MVP не generic.

Отдельные interfaces для Variable/Definition в MVP не требуются без конкретной необходимости.

## Change event

Каждый EcaVariableStore имеет собственное синхронное событие Action<EcaVariableChanged> VariableChanged. Aggregate System event, Store events, StoreId в payload и propagation в parent отсутствуют.

Typed events вроде `IntVariableChanged` / `BoolVariableChanged` не нужны.

Payload события — отдельный concrete `EcaVariableChanged`, а не сам `EcaVariable`.

`EcaVariableChanged` по данным полностью соответствует snapshot `EcaVariable`:
- Definition
- CurrentValue
- OldValue

Между `EcaVariable` и `EcaVariableChanged` делается явный mapping/copy. Это физически разные сущности, чтобы runtime-owned mutable Variable не выдавался наружу как event payload.

Событие изменения Store/partition будет спроектировано позже.

## Store manager / forest

EcaVariablesSystem public API:

```csharp
public EcaVariableStore CreateStore(string storeId, string parentId = null);
public bool ContainsStore(string storeId);
public EcaVariableStore GetStore(string storeId);
public bool TryGetStore(string storeId, out EcaVariableStore store);
```

Внутри manager — один ordinal Dictionary<string, EcaVariableStore>. Store IDs globally unique per system, stable и не являются путями. Get/Try возвращают exact Store instance. Missing GetStore даёт InvalidOperationException; ContainsStore/TryGetStore — false (out store = null). Все lookup methods отклоняют null/empty/whitespace ID с ArgumentException.

EcaVariableStore имеет get-only string Id/ParentId и internal constructor. ParentId optional и immutable; null означает root. Parent — только structural ownership, parent lookup отсутствует. Parent должен существовать до создания child, поэтому новые связи не образуют cycles. Не нужны children collection, Parent object reference, traversal API или StoreRegistry.

CreateStore сначала проверяет корректность storeId и non-null parentId (ArgumentException), затем duplicate StoreId и missing parent (InvalidOperationException). CreateStore("a", "a") для нового ID отклоняется как missing parent, для существующего — как duplicate; состояния manager не меняет. ID уникален также между разными branches; отдельные EcaVariablesSystem независимы.

Store хранит собственный ordinal Dictionary<string, EcaVariable>, не имеет reference на parent/manager и выполняет строго local lookup. Variable, существующая только у parent, для child отсутствует: Contains/Try false, Get и все setters бросают InvalidOperationException. Local одинаковый ID parent/child — независимые Variables, а не override. Events только local. Stores независимы от EcaScope/RuleId; их семантическое назначение задаёт приложение.

Remove/Clear/Reparent/Copy/Templates, inheritance/fallback/override и Store events не реализованы; будущие возможности сохранены в Roadmap. Flat System Variable API намеренно удалён; автоматических Store нет.

## Независимость partitioning от ECA

VariablesSystem не должен требовать соответствия собственных Stores структурам ECA.

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

Core2 State расширен отдельным opaque payload произвольного типа `object`, без dependency из Variables Core.

Реализованы overloads, сохраняющие прежние no-payload signatures/behavior:

```csharp
Register<T>(string id, Func<IEcaRuleState, T> resolve);
Register<T>(string id, Func<IEcaRuleState, object, T> resolve);

Resolve<T>(string stateId, IEcaRuleState ruleState);
Resolve<T>(string stateId, IEcaRuleState ruleState, object payload);
```

Payload не интерпретируется EcaSystems и передаётся exact resolver function.

Resolver shape — строгий contract выбранного overload; payload null допустим и не означает no-payload вызов. До внешней функции проверяются ID → registration → exact T → shape → non-null RuleState. Shape mismatch даёт InvalidOperationException, fallback отсутствует. Один ID уникален независимо от T/shape. Connector переносит original delegate без wrapper; canonical validation включает ID/type/shape/delegate identity и не вызывает функцию при composition/rollback.

Payload позже позволит ECA adapter передавать Variables-specific selector вроде container/store/group/entity id без обязательного совпадения с EcaScope/RuleId.

## ECA integration

State используется для чтения, Commands — для mutation, Events — для изменений.

State export должен быть read-oriented и не обязан отдавать сам `EcaVariablesSystem`.

Предварительное направление:
- State: `variables.state`
- Event: один общий `VariableChanged`
- Command: как минимум `SetValue`

ECA adapter не входит в первую Variables Core-итерацию.

## Save/Load

Save/Load — отдельная следующая задача после стабилизации базового Variables API и container model.

Actual variables принадлежат VariablesSystem; EcaStateRegistry не владеет и не сериализует их.

## Точный Variable contract EcaVariableStore

```csharp
public string Id { get; }
public string ParentId { get; }
public event Action<EcaVariableChanged> VariableChanged;
public EcaVariable Declare<T>(string variableId, T defaultValue);
public bool Contains(string variableId);
public T GetValue<T>(string variableId);
public bool TryGetValue<T>(string variableId, out T value);
public void SetValue<T>(string variableId, T value);
public void ForceSetValue<T>(string variableId, T value);
public void SetCurrentValue<T>(string variableId, T value);
```

Namespace — EcaSystems.Variables. Все пять concrete классов sealed. Definition имеет get-only Id/ValueType/DefaultValue; Variable — публично read-only Definition/CurrentValue/OldValue; Changed — get-only snapshot этих данных. Constructors трёх data-классов internal, mutation производится EcaVariableStore. DefaultValue immutable навсегда, включая возможный будущий Load.

Реализация минимальна: ordinal Dictionary<string, EcaVariable> внутри каждого Store, без отдельного registry/controller. Поддержаны только int/float/bool/string, string null разрешён. Invalid ID → ArgumentException; unsupported T → NotSupportedException; duplicate/missing/wrong exact T → InvalidOperationException. TryGetValue возвращает false + default только для missing ID. Setters валидируют ID, supported T, existence и exact type до mutation/event. EqualityComparer<T>.Default определяет same-value. Declare event не создаёт; SetCurrentValue сохраняет Old; следующая tracked-запись берёт Old из текущего direct value.

Каждый tracked event явно копирует Definition/CurrentValue/OldValue в новый EcaVariableChanged до вызова подписчиков; shared Definition безопасна, так как immutable. Reentrant writes не меняют уже созданный snapshot. Это обычный synchronous C# event, без новой lifecycle/queue/exception-isolation архитектуры. Тесты вынесены в отдельную EcaSystems.Variables.Editor.Tests assembly.
