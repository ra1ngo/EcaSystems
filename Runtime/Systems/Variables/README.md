# VariablesSystem

`VariablesSystem` — небольшая standalone runtime-система типизированных игровых переменных по stable string ID.

Она разрабатывается внутри EcaSystems и оптимизирована под дальнейшую интеграцию с ECA, но **Core VariablesSystem не зависит от EcaSystems runtime**. Систему предполагается позднее можно будет вынести в отдельный репозиторий без переноса Core2.

## Статус

Архитектура находится в проектировании. Первая итерация ещё не реализована.

План первой версии:
- explicit variable declarations;
- stable string IDs;
- exact value type contract;
- `int`, `float`, `bool`, `string`;
- current/old values;
- единое событие изменения variable.

Stores/groups/scopes/entity groups, Save/Load, enum, history и reactive features будут рассматриваться отдельными итерациями.

## Структура

```text
Variables/
├── Core/            # standalone VariablesSystem
├── Eca/             # адаптер к EcaSystems
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
