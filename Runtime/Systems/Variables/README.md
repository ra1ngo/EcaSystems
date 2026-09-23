# VariablesSystem

`VariablesSystem` — небольшая standalone runtime-система типизированных игровых переменных по stable string ID.

Она разрабатывается внутри EcaSystems и оптимизирована под дальнейшую интеграцию с ECA, но **Core VariablesSystem не зависит от EcaSystems runtime**. Систему предполагается позднее можно будет вынести в отдельный репозиторий без переноса Core2.

## Статус

Core V1 реализован: assembly `EcaSystems.Variables`, namespace `EcaSystems.Variables`. Assembly не ссылается на Core2 или Unity (`noEngineReferences: true`). Variables/Eca пока только зарезервирован.

Реализовано в первой версии:
- explicit variable declarations;
- stable string IDs;
- exact value type contract;
- `int`, `float`, `bool`, `string`;
- current/old values;
- `SetValue` для обычного change-only tracked set;
- `ForceSetValue` для принудительного tracked set даже при same-value;
- `SetCurrentValue` для direct current assignment без изменения OldValue и event;
- единое событие изменения variable.

Stores/groups/scopes/entity groups, Save/Load, enum, history и reactive features будут рассматриваться отдельными итерациями.

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
EcaVariable
EcaVariableDefinition
EcaVariableChanged
```

Префикс используется для изоляции имён от built-in и third-party типов и не означает, что standalone Core зависит от EcaSystems.

## Архитектурный принцип

VariablesSystem владеет actual variable data.

EcaSystems позже может читать Variables через State и изменять их через Commands, но EcaStateRegistry не владеет и не сериализует данные VariablesSystem.

Будущие containers VariablesSystem не обязаны совпадать с ECA Scope или Execution/Rule identity. Любое сопоставление между этими координатами должно оставаться политикой ECA adapter.

## Public API V1

VariablesSystem является standalone system. Она разрабатывается для EcaSystems и использует Eca-prefixed naming, но Core VariablesSystem не зависит от EcaSystems runtime.

`EcaVariablesSystem`:

```csharp
public event Action<EcaVariableChanged> VariableChanged;
public EcaVariable Declare<T>(string variableId, T defaultValue);
public bool Contains(string variableId);
public T GetValue<T>(string variableId);
public bool TryGetValue<T>(string variableId, out T value);
public void SetValue<T>(string variableId, T value);
public void ForceSetValue<T>(string variableId, T value);
public void SetCurrentValue<T>(string variableId, T value);
```

`Declare` возвращает runtime-owned `EcaVariable` с публичными getters `Definition`, `CurrentValue`, `OldValue`. Definition (`Id`, `ValueType`, `DefaultValue`) immutable: исходный DefaultValue никогда не меняется. На declaration Old и Current равны Default, event отсутствует. Создание Definition/Variable/Changed принадлежит системе; их constructors internal.

| Setter | OldValue | CurrentValue | VariableChanged |
| --- | --- | --- | --- |
| SetValue, equal | без изменений | без изменений | нет |
| SetValue, changed | прежний Current | новое значение | один snapshot |
| ForceSetValue | прежний Current | новое значение | один snapshot, даже при equal |
| SetCurrentValue | без изменений | новое значение | нет |

Equality — `EqualityComparer<T>.Default`, включая float NaN. `string null` допустим. `EcaVariableChanged` — отдельный immutable snapshot с getters Definition/CurrentValue/OldValue, явно mapped из Variable перед callbacks; Definition сохраняет ту же ссылку. Последующие и reentrant mutations не меняют snapshot. Event обычный синхронный C# event; исключение подписчика распространяется после записи, автоматического rollback/изоляции подписчиков нет. Concurrent access не синхронизирован.

Все ID используют ordinal comparison, null/empty/whitespace дают ArgumentException. Только int/float/bool/string; другой T даёт NotSupportedException. Duplicate ID, missing обязательного lookup/set и wrong exact T дают InvalidOperationException, conversions отсутствуют. TryGetValue подавляет только missing ID (false + default), string null остаётся успешным результатом. Все setters проверяют ID → supported T → existence → exact declared type до записи. Get/Try/Declare также сначала проверяют ID и supported T.

Tests находятся в `Tests/Editor/Systems/Variables`, отдельная assembly `EcaSystems.Variables.Editor.Tests`. Следующие задачи — [локальный ToDo](Documentation~/ToDo.md); Stores, ECA adapter и Save/Load не реализованы.
