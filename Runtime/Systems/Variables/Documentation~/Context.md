# VariablesSystem — актуальный Context Core V3 + ECA V1

## Границы

Standalone assembly/namespace EcaSystems.Variables без dependencies на Core/Core1/Core2 и Unity. Concrete Eca-prefixed naming служит изоляции имён. Standalone production находится в Core. Eca содержит отдельную assembly EcaSystems.Variables.Eca с references только на Variables/Core2, noEngineReferences=true; adapter V1 реализован. Точные public signatures собраны в [README](../README.md).

## Ownership и authoritative tree

EcaVariablesSystem хранит единственный authoritative ordinal `Dictionary<string, EcaVariableStore>`. Store IDs globally unique внутри System, opaque strings, не paths. ParentId optional и immutable; parent должен существовать до child, поэтому cycles при CreateStore невозможны. Roots может быть несколько, automatic default/global/root Store не создаётся. CreateStore сначала валидирует storeId/non-null parentId, затем duplicate ID и existence parent. Self-parent нового ID — missing parent, существующего — duplicate. Ошибки не резервируют ID.

Каждый Store содержит owner reference на System и собственный get-only EcaVariableRegistry Variables. Local ordinal Dictionary<string, EcaVariable> находится только в Registry. Registry отвечает за ID validation, existence и storage; generic type validation и event routing в нём отсутствуют. Parent/children/siblings object graph, indexes и tree service не добавлены. ParentId задаёт ownership; navigation/events не добавляют Variable inheritance. GetVariable/GetValue/Contains/Try и все setters работают только local. Одинаковый Variable ID в parent/child — независимые declaration/data, возможны разные value types. Stores независимы от EcaScope/RuleId; adapter использует explicit StoreId, без automatic Scope/Rule mapping.

## Definition → Data → facade → Controller

EcaVariableDefinition (Id, ValueType, DefaultValue) immutable, StoreId в ней отсутствует. DefaultValue сохраняется навсегда, включая возможный будущий Load.

EcaVariableData — public sealed, constructor internal, StoreId и Definition get-only, CurrentValue/OldValue public get + internal set. Это единственное live хранилище; initial Current/Old равны DefaultValue. StoreId берётся из owning Store.

EcaVariable — public sealed facade, constructor internal. Data выдаётся exact reference; StoreId/Definition/CurrentValue/OldValue proxy на Data без дублирования. `GetValue<T>`/`SetValue<T>`/`ForceSetValue<T>`/`SetCurrentValue<T>` делегируют internal sealed EcaVariableController. Controller валидирует supported/exact type, меняет Data, maps event и уведомляет owning Store. Новых interfaces нет.

Store Declare валидирует ID и supported declaration T, создаёт Definition/Data/facade с owning Store и вызывает internal Registry.Register. Duplicate registration проверяет Registry после declaration type. Register не public: внешний код не может перенести facade в чужой Store. Remove/Unregister отсутствуют. Registry.Resolve/TryResolve и Store.GetVariable/TryGetVariable возвращают exact instance из Declare. Registry.Variables — live read-only collection фасадов; mutable Dictionary наружу не выдаётся.

Store сохраняет tree/node, navigation, bubbling, snapshots и convenience facade. Его Get/Set proxies resolve-ят Variable через Registry и вызывают facade; type/mutation behavior принадлежит Controller. Двойной generic validation нет. Mandatory Store precedence: **ID → existence → supported T → exact T**. Missing + unsupported T даёт InvalidOperationException из lookup. TryGetValue: ID → missing=false + default независимо от T; только existing Variable проверяет supported/exact T. Direct Variable по-прежнему проверяет supported T → exact T. Declare: ID → supported T → duplicate registration.

## Variable semantics

Поддержаны только int/float/bool/string, string null разрешён. Unsupported T → NotSupportedException; invalid ID → ArgumentException; duplicate/missing/wrong exact T → InvalidOperationException. Assignable/numeric conversion отсутствует. TryGetValue только для missing возвращает false + default; TryGetVariable — false + null. GetVariable/GetStore/Try возвращают exact runtime instances.

SetValue использует `EqualityComparer<T>`.Default: equal — без mutation/event и без изменения Old; changed — Old=previous Current, Current=new, один event. ForceSetValue всегда делает tracked write/event, включая equal. SetCurrentValue меняет только Current, не Old/event. Следующая tracked mutation использует direct Current как Old. Declare не испускает event. Прямая facade mutation и Store proxy проходят один Controller pipeline.

## Два отдельных mapper

Internal sealed EcaVariableChangedMapper имеет единственный public Map(EcaVariable) → EcaVariableChanged. Controller maps после mutation, ДО callbacks. EcaVariableChanged immutable: StoreId/Definition/CurrentValue/OldValue get-only, constructor internal. StoreId всегда исходного Store.

Internal sealed EcaVariableSnapshotMapper отдельно имеет Map(EcaVariable) → EcaVariableSnapshot; не использует Changed mapper. Snapshot — самостоятельная immutable entity, не Data, не event и не ECA EventState. Обе mappings копируют values, shared Definition безопасна ввиду immutability. Generic mapping framework и overloads других source entities не добавлены.

## Core snapshots

GetStoreState перечисляет Variables.Variables и возвращает EcaVariableStoreState: StoreId, ParentId, derived IsRoot и read-only snapshot collection Variables. Каждый EcaVariableSnapshot содержит StoreId/Definition/CurrentValue/OldValue. Только local Variables, без parent/children.

GetSubtreeState возвращает EcaVariableSubtreeState: RootStoreId и read-only snapshot collection Stores. Включает исходный Store и всех descendants ровно один раз, без ancestors/siblings. Форма flat, ParentId сохраняет topology (включая parent вне выбранного subtree). Collections скопированы и read-only, values point-in-time. Последующие mutation/Declare/CreateStore не меняют snapshots. Порядок не является semantic contract. SaveLoad/history не реализуются этим API.

## Navigation

IsRoot computed как ParentId == null. GetParent root → null, TryGetParent root → false + null; child → exact parent instance. GetChildren — direct only; GetSiblings — другие Stores с тем же ParentId, self исключён. Roots siblings друг другу; single root имеет empty siblings. GetSubtree — self + все descendants ровно один раз. Order не гарантируется.

Navigation спрашивает owning System и scan-ит authoritative dictionary, без дополнительных indexes. Collections являются read-only снимками состава; их элементы — live Store references. Это отличается от GetStoreState/GetSubtreeState, которые копируют также Variable values. IsAttached и Store lifecycle API не добавлены.

## Synchronous bottom-up bubbling

Маршрут: source Store subscribers → parent subscribers → ... → root subscribers → System subscribers. Store.VariableChanged имеет subtree semantics; local-only filter — change.StoreId == store.Id. Siblings и другие roots не получают event. EcaVariablesSystem.VariableChanged — terminal aggregate ровно один раз на завершённый маршрут; работает для Stores, созданных после подписки.

Owning Store явно передаёт один immutable EcaVariableChanged вверх через parent lookup у System. Подписок child.VariableChanged += parent нет. StoreId не подменяется по пути. Каждый уровень заканчивает свои subscribers перед следующим уровнем.

Reentrant mutation синхронно выполняет nested маршрут и затем возвращается к outer callback; оба snapshots независимы. Если subscriber бросает, exact exception выходит наружу: следующие subscribers и следующие уровни не вызываются, запись уже совершена, rollback/isolation/queue отсутствуют. Модель рассчитана на последовательный доступ, concurrent synchronization не добавлена.

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

Variables adapter передаёт EcaVariablesStatePayload(StoreId) для адресных Store/Subtree snapshots без совпадения с EcaScope/RuleId.

## Whole-forest snapshot и Variables/ECA V1

EcaVariablesSystem.GetState() собирает EcaVariablesSystemState.Stores из существующих GetStoreState(): flat read-only point-in-time collection всех roots/descendants, каждый ровно один раз, order unspecified. StoreState содержит только local Variable snapshots. Topology остаётся ParentId/IsRoot. Snapshot не меняется после CreateStore/Declare/SetValue/ForceSetValue/SetCurrentValue. Это главный broad read model, не subtree wrapper и не live Store collection.

VariablesEcaSetup.CreateSystem создаёт passive descriptor variables/variables/Variables и local registries с ровно шестью exports:

- variables.state: no-payload → EcaVariablesSystemState, canonical/default read model будущего visual programming.
- variables.store: EcaVariablesStatePayload(StoreId) → EcaVariableStoreState.
- variables.subtree: тот же payload → EcaVariableSubtreeState.
- variables.variable.changed: IEcaEvent<EcaVariableChangedEventState>.
- variables.variable.set и variables.variable.force-set: EcaSetVariableArgs(StoreId, VariableId, object Value).

State payload class валидирует непустой StoreId. Null/wrong opaque payload → ArgumentException, missing Store → InvalidOperationException; нет fallback. Core2 resolver-shape contract сохранён, no-payload и payload overload не взаимозаменяемы. Results — Core snapshots напрямую, без wrapper. IDs централизованы в internal key/Ids mappings по Time/Eca pattern.

VariablesEcaAdapter один раз resolve-ит typed Event и сверяет EventStateType; кеширует instance. Explicit Connect подписывает только System.VariableChanged, duplicate Connect бросает, Disconnect idempotent, reconnect работает. Constructor не auto-connect; exports connect перед adapter.Connect, adapter.Disconnect перед exports disconnect. Lifecycle caller-owned, не Connector/runtime-owned. Созданные после Connect Stores автоматически охвачены System aggregate.

Отдельный internal EcaVariableChangedEventStateMapper maps Core Changed в immutable flat ECA snapshot: StoreId/VariableId/ValueType/CurrentValue/OldValue. Emitter получает null contexts. Reentrant mutation не меняет outer snapshot; раннее исключение Core subscriber прерывает route до ECA без rollback.

Два sealed Commands наследуют AEcaCommand<IEcaRuleState, IEcaActionContext, EcaSetVariableArgs>, инкапсулируют System. Null args отклоняется, затем explicit Store/Variable lookup. Dispatch по declared int/float/bool/string вызывает Core SetValue<T> либо ForceSetValue<T>. Value exact runtime type; null только string; mismatch → ArgumentException. Conversions/dynamic/reflection invoke/object Core setter отсутствуют. Command не fire-ит Event вручную: mutation → bubbling → System → adapter. Selection не зависит от RuleState/ScopeState. SetCurrentValue Command не добавлен.

## Следующие итерации

Variables/ECA V1 завершён; SaveLoad — следующий отдельный этап, пока не реализован. Remove/Clear/Reparent/Copy/Templates, parent lookup/inheritance/override, Store lifecycle events, History, enum, reactive/computed/watch/collections остаются отложенными. Согласованные будущие возможности — [Roadmap](Roadmap.md), необходимые шаги — [ToDo](ToDo.md).
