# VariablesSystem — актуальный Context Core V3

## Границы

Standalone assembly/namespace EcaSystems.Variables без dependencies на Core/Core1/Core2 и Unity. Concrete Eca-prefixed naming служит изоляции имён. Production только в Core, Eca зарезервирован. Variables/ECA adapter остаётся следующей отдельной итерацией; read State/mutation Commands/change Events — только будущее направление, не реализация. Точные public signatures собраны в [README](../README.md).

## Ownership и authoritative tree

EcaVariablesSystem хранит единственный authoritative ordinal `Dictionary<string, EcaVariableStore>`. Store IDs globally unique внутри System, opaque strings, не paths. ParentId optional и immutable; parent должен существовать до child, поэтому cycles при CreateStore невозможны. Roots может быть несколько, automatic default/global/root Store не создаётся. CreateStore сначала валидирует storeId/non-null parentId, затем duplicate ID и existence parent. Self-parent нового ID — missing parent, существующего — duplicate. Ошибки не резервируют ID.

Каждый Store содержит owner reference на System и отдельный local `Dictionary<string, EcaVariable>`. Parent/children/siblings object graph, indexes, registry/tree service не добавлены. ParentId задаёт ownership; navigation/events не добавляют Variable inheritance. GetVariable/GetValue/Contains/Try и все setters работают только local. Одинаковый Variable ID в parent/child — независимые declaration/data, возможны разные value types. Stores независимы от EcaScope/RuleId; будущий adapter выбирает mapping сам.

## Definition → Data → facade → Controller

EcaVariableDefinition (Id, ValueType, DefaultValue) immutable, StoreId в ней отсутствует. DefaultValue сохраняется навсегда, включая возможный будущий Load.

EcaVariableData — public sealed, constructor internal, StoreId и Definition get-only, CurrentValue/OldValue public get + internal set. Это единственное live хранилище; initial Current/Old равны DefaultValue. StoreId берётся из owning Store.

EcaVariable — public sealed facade, constructor internal. Data выдаётся exact reference; StoreId/Definition/CurrentValue/OldValue proxy на Data без дублирования. `GetValue<T>`/`SetValue<T>`/`ForceSetValue<T>`/`SetCurrentValue<T>` делегируют internal sealed EcaVariableController. Controller валидирует supported/exact type, меняет Data, maps event и уведомляет owning Store. Новых interfaces нет.

Store Declare отвечает за ID, type declaration и создание facade. GetVariable/TryGetVariable возвращают exact instance из Declare, не копию. Store Get/Set proxies делегируют behavior facade. Supported type validation перед missing lookup сохранена для совместимости V1/V2: ID → supported T → existence → exact T. Controller содержит общую supported type проверку, а Store вызывает её до lookup, включая TryGetValue.

## Variable semantics

Поддержаны только int/float/bool/string, string null разрешён. Unsupported T → NotSupportedException; invalid ID → ArgumentException; duplicate/missing/wrong exact T → InvalidOperationException. Assignable/numeric conversion отсутствует. TryGetValue только для missing возвращает false + default; TryGetVariable — false + null. GetVariable/GetStore/Try возвращают exact runtime instances.

SetValue использует `EqualityComparer<T>`.Default: equal — без mutation/event и без изменения Old; changed — Old=previous Current, Current=new, один event. ForceSetValue всегда делает tracked write/event, включая equal. SetCurrentValue меняет только Current, не Old/event. Следующая tracked mutation использует direct Current как Old. Declare не испускает event. Прямая facade mutation и Store proxy проходят один Controller pipeline.

## Два отдельных mapper

Internal sealed EcaVariableChangedMapper имеет единственный public Map(EcaVariable) → EcaVariableChanged. Controller maps после mutation, ДО callbacks. EcaVariableChanged immutable: StoreId/Definition/CurrentValue/OldValue get-only, constructor internal. StoreId всегда исходного Store.

Internal sealed EcaVariableSnapshotMapper отдельно имеет Map(EcaVariable) → EcaVariableSnapshot; не использует Changed mapper. Snapshot — самостоятельная immutable entity, не Data, не event и не ECA EventState. Обе mappings копируют values, shared Definition безопасна ввиду immutability. Generic mapping framework и overloads других source entities не добавлены.

## Core snapshots

GetStoreState возвращает EcaVariableStoreState: StoreId, ParentId, derived IsRoot и read-only snapshot collection Variables. Каждый EcaVariableSnapshot содержит StoreId/Definition/CurrentValue/OldValue. Только local Variables, без parent/children.

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

Payload позже позволит ECA adapter передавать Variables-specific selector вроде container/store/group/entity id без обязательного совпадения с EcaScope/RuleId.

## Следующие итерации

Variables/ECA adapter и SaveLoad не реализованы. Remove/Clear/Reparent/Copy/Templates, parent lookup/inheritance/override, Store lifecycle events, History, enum, reactive/computed/watch/collections остаются отложенными. Согласованные будущие возможности — [Roadmap](Roadmap.md), необходимые шаги — [ToDo](ToDo.md).
