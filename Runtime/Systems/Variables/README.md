# VariablesSystem

`VariablesSystem` — небольшая standalone runtime-система типизированных игровых переменных по stable string ID.

Она разрабатывается внутри EcaSystems и оптимизирована под дальнейшую интеграцию с ECA, но **Core VariablesSystem не зависит от EcaSystems runtime**. Систему предполагается позднее можно будет вынести в отдельный репозиторий без переноса Core2.

## Статус

Stores V2 реализован: assembly `EcaSystems.Variables`, namespace `EcaSystems.Variables`. Assembly не ссылается на Core2 или Unity (`noEngineReferences: true`). Variables/Eca пока только зарезервирован.

Variable semantics, сохранённые из V1 в каждом Store:
- explicit variable declarations;
- stable string IDs;
- exact value type contract;
- `int`, `float`, `bool`, `string`;
- current/old values;
- `SetValue` для обычного change-only tracked set;
- `ForceSetValue` для принудительного tracked set даже при same-value;
- `SetCurrentValue` для direct current assignment без изменения OldValue и event;
- единое событие изменения variable.

Stores образуют forest через immutable ParentId. Reparent/copy/templates, inheritance, Save/Load, enum, history и reactive features остаются будущими возможностями.

## Структура

```text
Variables/
├── Core/            # standalone VariablesSystem
├── Eca/             # зарезервировано, adapter не реализован
├── Documentation~/  # локальный context / todo системы
└── README.md
```

## Naming

Название системы — `VariablesSystem`.

Concrete classes/files используют префикс `Eca`:

```text
EcaVariablesSystem
EcaVariableStore
EcaVariable
EcaVariableDefinition
EcaVariableChanged
```

Префикс используется для изоляции имён от built-in и third-party типов и не означает, что standalone Core зависит от EcaSystems.

## Архитектурный принцип

EcaVariablesSystem управляет Stores; каждый EcaVariableStore владеет своими actual variable data.

EcaSystems позже может читать Variables через State и изменять их через Commands, но EcaStateRegistry не владеет и не сериализует данные VariablesSystem.

Stores VariablesSystem не обязаны совпадать с ECA Scope или Execution/Rule identity. Любое сопоставление между этими координатами должно оставаться политикой ECA adapter.

## Stores и local lookup

Store IDs глобально уникальны внутри одного EcaVariablesSystem и сравниваются ordinal; ID не является path. ParentId optional (`null` для root) и immutable. Несколько roots образуют forest. Parent должен уже существовать и означает только ownership, без поиска variables, inheritance/fallback/override или propagation events.

Manager не создаёт implicit global/default/root Store. Старые system-level VariableChanged/Declare/Contains/GetValue/TryGetValue/SetValue/ForceSetValue/SetCurrentValue удалены без proxy methods. Store создаётся только через manager; его constructor internal.

```csharp
var variables = new EcaVariablesSystem();
var global = variables.CreateStore("global");
var entities = variables.CreateStore("entities");
var player = variables.CreateStore("player", "entities");

global.Declare("difficulty", 2);
entities.Declare("parentOnly", 7);
player.Declare("health", 100);
var health = player.GetValue<int>("health");
// Оба вызова дают InvalidOperationException: lookup строго local.
player.GetValue<int>("difficulty"); // не ищет в global/других Stores
player.GetValue<int>("parentOnly"); // не ищет даже в своём parent
```

CreateStore проверяет storeId, затем non-null parentId на null/empty/whitespace (ArgumentException), duplicate StoreId (InvalidOperationException), затем existence parent (InvalidOperationException). Self-parent нового ID отклоняется как missing parent, существующего — как duplicate; Store не создаётся и не изменяется. GetStore missing даёт InvalidOperationException, ContainsStore — false, TryGetStore — false + null; существующий lookup возвращает exact instance. Invalid ID во всех lookup methods даёт ArgumentException.

Одинаковый variable ID допустим в разных Stores, в том числе parent/child, с независимыми значениями и declared types. VariableChanged испускается только конкретным Store. StoreId в snapshot, aggregate event, bubbling и Store events отсутствуют.

## Public API Stores V2

VariablesSystem является standalone system. Она разрабатывается для EcaSystems и использует Eca-prefixed naming, но Core VariablesSystem не зависит от EcaSystems runtime.

`EcaVariablesSystem`:

```csharp
public EcaVariableStore CreateStore(string storeId, string parentId = null);
public bool ContainsStore(string storeId);
public EcaVariableStore GetStore(string storeId);
public bool TryGetStore(string storeId, out EcaVariableStore store);
```

`EcaVariableStore`:

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

`Declare` возвращает runtime-owned `EcaVariable` с публичными getters `Definition`, `CurrentValue`, `OldValue`. Definition (`Id`, `ValueType`, `DefaultValue`) immutable: исходный DefaultValue никогда не меняется. На declaration Old и Current равны Default, event отсутствует. Создание Definition/Variable/Changed принадлежит Store; их constructors internal.

| Setter | OldValue | CurrentValue | VariableChanged |
| --- | --- | --- | --- |
| SetValue, equal | без изменений | без изменений | нет |
| SetValue, changed | прежний Current | новое значение | один snapshot |
| ForceSetValue | прежний Current | новое значение | один snapshot, даже при equal |
| SetCurrentValue | без изменений | новое значение | нет |

Equality — `EqualityComparer<T>.Default`, включая float NaN. `string null` допустим. `EcaVariableChanged` — отдельный immutable snapshot с getters Definition/CurrentValue/OldValue, явно mapped из Variable перед callbacks; Definition сохраняет ту же ссылку. Последующие и reentrant mutations не меняют snapshot. Event обычный синхронный C# event; исключение подписчика распространяется после записи, автоматического rollback/изоляции подписчиков нет. Concurrent access не синхронизирован.

Все ID используют ordinal comparison, null/empty/whitespace дают ArgumentException. Только int/float/bool/string; другой T даёт NotSupportedException. Duplicate ID, missing обязательного lookup/set и wrong exact T дают InvalidOperationException, conversions отсутствуют. TryGetValue подавляет только missing ID (false + default), string null остаётся успешным результатом. Все setters проверяют ID → supported T → existence → exact declared type до записи. Get/Try/Declare также сначала проверяют ID и supported T.

Tests находятся в `Tests/Editor/Systems/Variables`, отдельная assembly `EcaSystems.Variables.Editor.Tests`. Следующие обязательные задачи — [локальный ToDo](Documentation~/ToDo.md), будущие возможности — [Variables Roadmap](Documentation~/Roadmap.md). Store ownership реализован; ECA adapter и Save/Load пока не реализованы.
