# VariablesSystem — Core V3 + ECA V1

VariablesSystem является standalone system. Она разрабатывается для EcaSystems и использует Eca-prefixed naming, но Core VariablesSystem не зависит от EcaSystems runtime.

Assembly/namespace — `EcaSystems.Variables`. References пусты, `noEngineReferences: true`. В `Core/` находится standalone implementation; отдельная assembly `EcaSystems.Variables.Eca` в `Eca/` ссылается только на Variables и Core2, без Unity dependencies. Архитектурный контекст — [Context](Documentation~/Context.md), следующие шаги — [ToDo](Documentation~/ToDo.md), будущие возможности — [Roadmap](Documentation~/Roadmap.md).

## Ownership и использование

System управляет forest Stores со stable globally unique per-system ordinal IDs. Store содержит local Variables. ParentId optional и immutable, null означает root. Автоматического global/default Store нет, ID не является path. Parent означает structural ownership, **Variable lookup строго local**: navigation и bubbling не добавляют inheritance/fallback/override. Store/Variable IDs не обязаны совпадать с EcaScope/RuleId.

```csharp
var variables = new EcaVariablesSystem();
variables.VariableChanged += change => Console.WriteLine(change.StoreId);
var global = variables.CreateStore("global");
var entities = variables.CreateStore("entities");
var player = variables.CreateStore("player", "entities");
global.Declare("difficulty", 2);
entities.Declare("parentOnly", 7);
var health = player.Declare("health", 100);
health.SetValue(90);                 // тот же pipeline, что player.SetValue("health", 90)
var value = player.GetValue<int>("health");
var snapshot = entities.GetSubtreeState();
// player.GetValue<int>("difficulty") и player.GetValue<int>("parentOnly") бросают:
// variables не ищутся ни в другом root, ни в parent.
```

## Variable: Data, facade, controller

`EcaVariableDefinition` — immutable Id/ValueType/DefaultValue, **без StoreId**. `EcaVariableData` содержит owning StoreId, Definition и единственные live CurrentValue/OldValue (public read, internal write). `EcaVariable` предоставляет facade над Data и делегирует behavior internal sealed `EcaVariableController`. Data/facade constructors internal.

`EcaVariable` public API:

```csharp
public EcaVariableData Data { get; }
public string StoreId { get; }
public EcaVariableDefinition Definition { get; }
public object CurrentValue { get; }
public object OldValue { get; }
public T GetValue<T>();
public void SetValue<T>(T value);
public void ForceSetValue<T>(T value);
public void SetCurrentValue<T>(T value);
```

Definition.DefaultValue никогда не меняется; при declaration Current=Old=Default, event отсутствует. Поддержаны только int/float/bool/string; string null допустим. Type contract exact, без conversions; equality — `EqualityComparer<T>`.Default, включая float NaN.

| Операция | Old | Current | Event |
| --- | --- | --- | --- |
| SetValue, equal | без изменений | без изменений | нет |
| SetValue, changed | предыдущий Current | new | один snapshot |
| ForceSetValue | предыдущий Current | new | один snapshot даже при equal |
| SetCurrentValue | без изменений | new | нет |

Store proxies получают тот же facade через Registry и вызывают его методы. GetVariable/TryGetVariable возвращают exact instance из Declare. Invalid ID → ArgumentException; unsupported T → NotSupportedException; duplicate/missing/wrong exact type → InvalidOperationException.

Порядок validation:

- Store Get/Set/ForceSet/SetCurrent: **ID → existence → supported T → exact T**. Поэтому GetValue<double>("missing") даёт InvalidOperationException.
- TryGetValue: ID → missing=false + default, даже для unsupported T; supported/exact type проверяется только у существующей Variable.
- Direct EcaVariable: supported T → exact T, без изменений.
- Declare: ID → supported declaration T → duplicate registration.

## Local Registry

Store владеет одним `EcaVariableRegistry`: только он хранит ordinal Dictionary<string, EcaVariable> и отвечает за ID validation, existence и storage. Store сохраняет declaration, tree/navigation, bubbling, snapshots и convenience facade; EcaVariable делегирует type/mutation behavior Controller без повторной generic validation в Store proxies.

Public API `EcaVariableRegistry`:

```csharp
public IReadOnlyCollection<EcaVariable> Variables { get; }
public bool Contains(string variableId);
public EcaVariable Resolve(string variableId);
public bool TryResolve(string variableId, out EcaVariable variable);
```

`Register(EcaVariable)` internal: только Store.Declare создаёт и регистрирует facade с правильным owner. Public Remove/Unregister отсутствуют. Resolve возвращает exact instance или бросает при missing; TryResolve возвращает false + null. Все lookup methods отклоняют invalid ID. Variables — live read-only collection фасадов, не mutable Dictionary и не point-in-time snapshot. GetStoreState перечисляет Variables.Variables и копирует значения в отдельные snapshots.

## Public API Store/System

`EcaVariableStore`:

```csharp
public EcaVariableRegistry Variables { get; }
public string Id { get; }
public string ParentId { get; }
public bool IsRoot { get; }
public event Action<EcaVariableChanged> VariableChanged;
public EcaVariable Declare<T>(string variableId, T defaultValue);
public bool Contains(string variableId);
public EcaVariable GetVariable(string variableId);
public bool TryGetVariable(string variableId, out EcaVariable variable);
public T GetValue<T>(string variableId);
public bool TryGetValue<T>(string variableId, out T value);
public void SetValue<T>(string variableId, T value);
public void ForceSetValue<T>(string variableId, T value);
public void SetCurrentValue<T>(string variableId, T value);
public EcaVariableStore GetParent();
public bool TryGetParent(out EcaVariableStore parent);
public IReadOnlyList<EcaVariableStore> GetChildren();
public IReadOnlyList<EcaVariableStore> GetSiblings();
public IReadOnlyList<EcaVariableStore> GetSubtree();
public EcaVariableStoreState GetStoreState();
public EcaVariableSubtreeState GetSubtreeState();
```

`EcaVariablesSystem`:

```csharp
public event Action<EcaVariableChanged> VariableChanged;
public EcaVariableStore CreateStore(string storeId, string parentId = null);
public bool ContainsStore(string storeId);
public EcaVariableStore GetStore(string storeId);
public bool TryGetStore(string storeId, out EcaVariableStore store);
public EcaVariablesSystemState GetState();
```

GetStore/TryGetStore возвращают exact instance; missing GetStore бросает InvalidOperationException, Try возвращает false + null. CreateStore требует существующего parent; non-null пустой parentId и invalid storeId дают ArgumentException. Duplicate ID и missing parent дают InvalidOperationException без изменения manager. Self-parent нового ID отклоняется как missing parent, существующего — как duplicate. Flat Variable API System отсутствует.

## Navigation и snapshots

Root.GetParent() возвращает null, TryGetParent — false + null. Children — только direct children; siblings — все остальные Stores с тем же ParentId, **roots являются siblings друг другу**. Subtree включает сам Store и всех descendants ровно один раз, без ancestors/siblings вне subtree. Порядок не является контрактом. Navigation collections — read-only снимки состава с live Store references; последующее CreateStore не меняет старую collection.

`EcaVariableSnapshot` содержит StoreId/Definition/CurrentValue/OldValue. `EcaVariableStoreState` содержит StoreId/ParentId/IsRoot и `IReadOnlyList<EcaVariableSnapshot>` Variables, только local. `EcaVariableSubtreeState` содержит RootStoreId и `IReadOnlyList<EcaVariableStoreState>` Stores — плоскую topology через ParentId. Это immutable point-in-time snapshots: mutation, Declare и создание descendant не меняют уже полученные данные или collections. Immutable Definition может сохранять ту же ссылку.

## Bottom-up VariableChanged

Порядок: **source Store → parent → grandparent → root → EcaVariablesSystem**. Store.VariableChanged получает изменения самого Store и всего subtree; System event — terminal aggregate. Для local-only обработки: `if (change.StoreId == store.Id) ...`.

Controller после записи создаёт snapshot через отдельный internal sealed EcaVariableChangedMapper (только Map(EcaVariable) → EcaVariableChanged), затем owning Store явно проводит его вверх по authoritative `_stores` System. Topology не строится подписками child.VariableChanged += parent. На всех уровнях передаётся тот же snapshot с неизменным source StoreId. Подписка System работает и для будущих Stores.

Для EcaVariableSnapshot используется другой internal sealed EcaVariableSnapshotMapper, также только Map(EcaVariable). Event и Core snapshot не переиспользуются друг вместо друга.

Events синхронны/reentrant: nested mutation полностью проходит маршрут до продолжения outer callbacks, outer snapshot не меняется. Исключение subscriber распространяется без замены, останавливает следующих subscribers и оставшийся маршрут; mutation уже выполнена, rollback/isolation/queue отсутствуют. Concurrent access не синхронизирован.

Remove/Reparent/Copy/Templates, inheritance, SaveLoad, Store lifecycle events, History, enum и reactive features не реализованы. Tests: `Tests/Editor/Systems/Variables`, assembly `EcaSystems.Variables.Editor.Tests`.

## Whole-system State и ECA exports

`EcaVariablesSystem.GetState()` возвращает `EcaVariablesSystemState` с `IReadOnlyList<EcaVariableStoreState> Stores`: flat point-in-time snapshot всего forest, каждый root/descendant ровно один раз, с local Variables. Topology выражается ParentId/IsRoot, порядок не гарантирован. Это отдельный contract от SubtreeState; live Store references в нём нет. Последующие CreateStore/Declare и все три setters snapshot не меняют; empty System даёт empty Stores.

`VariablesEcaSetup.CreateSystem(variables)` создаёт passive descriptor: Id/Namespace `variables`, Name `Variables`, local registries. Экспорты:

| Вид | ID | Payload / Args | Результат |
| --- | --- | --- | --- |
| State | `variables.state` | без payload | EcaVariablesSystemState |
| State | `variables.store` | EcaVariablesStatePayload(StoreId) | EcaVariableStoreState |
| State | `variables.subtree` | EcaVariablesStatePayload(StoreId) | EcaVariableSubtreeState |
| Event | `variables.variable.changed` | EcaVariableChangedEventState | — |
| Command | `variables.variable.set` | EcaSetVariableArgs(StoreId, VariableId, object Value) | Task |
| Command | `variables.variable.force-set` | EcaSetVariableArgs(StoreId, VariableId, object Value) | Task |

`variables.state` — canonical/default broad read model для будущего visual programming. Store/Subtree — адресные read models с explicit selector, без automatic StoreId↔ScopeId/RuleId mapping. Resolvers возвращают Core snapshots напрямую. Payload — sealed class с get-only StoreId: invalid ID в constructor, null/wrong opaque payload дают ArgumentException; missing Store — InvalidOperationException. Неверный payload/no-payload overload отклоняется Core2 shape validation без fallback.

EventState — отдельный immutable flat snapshot: StoreId, VariableId, ValueType, CurrentValue, OldValue. Internal EcaVariableChangedEventStateMapper явно копирует Core EcaVariableChanged; Definition/Data/Core Changed не выдаются как ECA EventState.

Commands — отдельные EcaSetVariableCommand/EcaForceSetVariableCommand. Они resolve-ят local Variable и явно dispatch-ят только int/float/bool/string по Definition.ValueType в Core SetValue<T>/ForceSetValue<T>. Value требует exact runtime type, null допустим только для string; mismatch даёт ArgumentException без conversions, dynamic/reflection invoke или object setter в Core. Args с invalid StoreId/VariableId отклоняются constructor; null args — ArgumentNullException. Set equal не испускает event, ForceSet equal испускает; Commands сами не fire-ят Event.

## Explicit adapter lifecycle

```csharp
var system = VariablesEcaSetup.CreateSystem(variables);
var adapter = new VariablesEcaAdapter(variables, system.Events, scope.EventEmitter);
connector.Connect(system);
adapter.Connect();
// Core mutation → source Store → ancestors → System aggregate → adapter → emitter.
adapter.Disconnect();
connector.Disconnect(system);
```

Connect повторно бросает InvalidOperationException; Disconnect idempotent, reconnect допустим. Constructor не подключает adapter: он один раз resolve-ит и проверяет typed canonical Event + metadata, затем кеширует declaration. Подписка только на System.VariableChanged охватывает Stores, созданные после Connect. Condition/Action contexts null. Если прежний Core subscriber прерывает bubbling exception, ECA Event не возникает; rollback/isolation нет. Adapter lifecycle принадлежит caller, не Connector/Core runtime.

Tests adapter находятся в `Tests/Editor/Systems/Variables/Eca`, отдельная assembly `EcaSystems.Variables.Eca.Editor.Tests`. Save/Load — следующий отдельный этап. SetCurrentValue/Declare/CreateStore Commands и другие deferred features не добавлены.
