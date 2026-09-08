# EcaSystems — инструкция для локального ChatGPT Work

## Цель

Применить к **текущему локальному коду EcaSystems** последний согласованный архитектурный рефакторинг Base + Execution.

Это замена не применившемуся git-патчу.  
Нужно **сначала изучить актуальные локальные файлы проекта**, а затем внести изменения в соответствии с этой инструкцией.

Важно:

- не переписывать архитектуру сверх описанного ниже;
- не реализовывать отложенные функции;
- не делать Scope-слой сейчас;
- не реализовывать Queue / Reset сейчас;
- не добавлять Systems / Global State сейчас;
- не менять namespace/package structure без необходимости;
- сохранить текущее поведение там, где ниже явно не указано обратное;
- после изменений обновить `EcaSystemsSmokeTest` и добиться прохождения тестов без ошибок.

---

# 1. Base: убрать CancellationToken

Base-слой не имеет понятия cancellation, поэтому он не должен зависеть от `CancellationToken`.

## `IEcaAction<TContext>`

Должно быть концептуально:

```csharp
public interface IEcaAction<in TContext>
{
    Task Run(TContext context);
}
```

Убрать `CancellationToken` из сигнатуры.

## `IEcaRuleRunner`

Должно быть концептуально:

```csharp
public interface IEcaRuleRunner
{
    Task Run<TContext>(IEcaRule<TContext> rule, TContext context);
}
```

## `EcaRuleRunner`

Просто вызывает:

```csharp
return rule.Action.Run(context);
```

## `EcaEngine`

Убрать передачу `CancellationToken.None`.

Base `Fire()` должен остаться максимально простым.

---

# 2. Добавить non-generic `IEcaRule`

Нужен общий тип Rule, чтобы Registry мог хранить Rules простым списком.

Концептуально:

```csharp
public interface IEcaRule
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    IEcaEvent Event { get; }
}
```

Generic интерфейс:

```csharp
public interface IEcaRule<TContext> : IEcaRule
{
    IEcaCondition<TContext> Condition { get; }
    IEcaAction<TContext> Action { get; }
}
```

Существующий `EcaRule<TContext>` должен продолжить реализовывать generic интерфейс.

Не менять текущий `EcaRuleConfig` сверх необходимого для компиляции.

---

# 3. Разделить `EcaRuleRegistry` и выборку Rules

Сейчас Registry не должен одновременно быть storage + selector.

## `EcaRuleRegistry`

Ответственность:

- хранить Rules;
- `Register`;
- `Unregister`;
- validation;
- cleanup/clear при необходимости.

Внутреннее хранилище должно стать простым:

```csharp
List<IEcaRule>
```

а наружу можно дать:

```csharp
IReadOnlyList<IEcaRule> Rules
```

Убрать из Registry ответственность `GetRulesForEvent`.

Сохранить существующие важные validations, в частности:

- duplicate `RuleId`;
- совместимость типов Rule/EventContext;
- прочие текущие проверки регистрации.

Не убирать `EcaContextType` только ради этого рефакторинга.

## Новый `IEcaRuleSelector`

Пока нужна одна выборка — Rules для Event.

Концептуально:

```csharp
public interface IEcaRuleSelector
{
    IReadOnlyList<IEcaRule<TContext>> ForEvent<TContext>(IEcaEvent ecaEvent);
}
```

## Новый `EcaRuleSelector`

Получает `IEcaRuleRegistry` через constructor и выполняет выборку из `registry.Rules`.

Например:

```csharp
public EcaRuleSelector(IEcaRuleRegistry ruleRegistry);
```

Selector отвечает **только за выборку**.

Не переносить в него ответственность Register/Unregister.

Сохранять текущую типобезопасность Event/Context. Не превращать несовместимые Rules в молчаливые ошибки.

---

# 4. Упростить `IEcaRuleChecker`

Checker должен проверять **один Rule**.

Концептуально:

```csharp
public interface IEcaRuleChecker
{
    bool Check<TContext>(IEcaRule<TContext> rule, TContext context);
}
```

Implementation:

```csharp
return rule.Condition == null || rule.Condition.Check(context);
```

Убрать из Checker перебор списка Rules.

---

# 5. Важная семантика: все Conditions раньше любых Actions

Эту семантику обязательно сохранить и в Base, и в Execution:

```text
Condition A
Condition B
Condition C

только после этого:

Action A
Action B
Action C
```

То есть нельзя сделать:

```text
Condition A
Action A
Condition B
Action B
```

## В Base `EcaEngine`

Engine сам:

1. получает Rules через Selector;
2. проверяет каждый Rule через single-rule Checker;
3. складывает прошедшие Rules;
4. только после проверки всех Conditions запускает Actions через Runner.

---

# 6. Переименовать state группы

Текущий:

```text
EcaRuleExecutionState
```

переименовать в:

```text
EcaRuleExecutionGroupState
```

Потому что это состояние **группы executions одного Rule**, а не одной конкретной execution.

Существующие counters сохранить:

```csharp
EcaRuleExecutionTotalStarted
EcaRuleExecutionTotalFinished
```

Пока не делать дополнительный cleanup naming.

В `EcaExecutionContext` свойство должно стать:

```csharp
RuleExecutionGroupState
```

Тип:

```csharp
EcaRuleExecutionGroupState
```

---

# 7. `EventContext` и Group State

Семантика:

- `EventContext` — общий input/payload одного `Fire`;
- framework считает его логически immutable;
- разные RuleExecutionGroups не должны использовать EventContext как свой mutable state;
- каждая Group мутирует только свой `EcaRuleExecutionGroupState`.

Не делать deep copy EventContext.

Не вводить `EcaEventState`.

---

# 8. Добавить `EcaExecutionContextFactory`

Нужна отдельная stateless-сущность с одной ответственностью:

```text
EventContext + GroupState
→ EcaExecutionContext
```

## `IEcaExecutionContextFactory`

Концептуально:

```csharp
public interface IEcaExecutionContextFactory
{
    EcaExecutionContext<TEventContext> Create<TEventContext>(
        TEventContext eventContext,
        EcaRuleExecutionGroupState groupState);
}
```

## `EcaExecutionContextFactory`

Обычная stateless implementation.

Не делать Builder/Composer сейчас.

---

# 9. `EcaRuleExecution<TEventContext>`

Сделать `EcaRuleExecution` generic и повысить связность Execution с Rule.

Концептуально одна Execution — это конкретный запуск:

```text
Rule + ExecutionContext + lifecycle state
```

Примерная сигнатура:

```csharp
public sealed class EcaRuleExecution<TEventContext>
{
    public long Id { get; }

    public IEcaRule<EcaExecutionContext<TEventContext>> Rule { get; }

    public EcaExecutionContext<TEventContext> Context { get; }

    public EcaRuleExecutionStatus Status { get; }

    public Exception Exception { get; }

    public CancellationToken CancellationToken { get; }

    internal EcaRuleExecution(
        long id,
        IEcaRule<EcaExecutionContext<TEventContext>> rule,
        EcaExecutionContext<TEventContext> context);

    internal void RequestCancellation();

    internal void MarkRunning();
    internal void MarkCancelling();
    internal void MarkCompleted();
    internal void MarkCancelled();
    internal void MarkFailed(Exception exception);
}
```

Важно:

- `CancellationToken` **остается только в Execution-слое**;
- не возвращать token в Base `IEcaAction` / `IEcaRuleRunner`;
- пока не придумывать окончательный пользовательский cancellation API для Action/Commands;
- не добавлять `Cancel()` в Base Action сейчас.

---

# 10. Статус `Cancelling`

В `EcaRuleExecutionStatus` добавить промежуточный статус:

```csharp
Pending,
Running,
Cancelling,
Completed,
Cancelled,
Failed
```

Семантика:

```text
Running → Cancelling → Cancelled
```

`Cancelled` — финальное состояние.

`Cancelling` — cancellation уже начата, но cleanup/завершение еще не закончено.

Для будущей отмены queued execution допустим переход:

```text
Pending → Cancelled
```

без `Cancelling`.

---

# 11. Новый `IEcaExecutionExecutor`

Выполнение Execution вынести из `EcaRuleExecutionGroup`.

Использовать термин **Executor**, не Runner.

## `IEcaExecutionExecutor`

Концептуально:

```csharp
public interface IEcaExecutionExecutor
{
    Task Execute<TEventContext>(
        EcaRuleExecution<TEventContext> execution);

    Task Cancel<TEventContext>(
        EcaRuleExecution<TEventContext> execution);
}
```

## `EcaExecutionExecutor`

Stateless относительно конкретного Engine/Scope.

Он внутри использует Base `EcaRuleRunner`.

По текущему решению `EcaRuleRunner` создается **внутри `EcaExecutionExecutor`**, например один раз в constructor.

Не передавать `IEcaRuleRunner` отдельно в `EcaExecutionEngine`.

`EcaExecutionExecutor` в дальнейшем должен быть пригоден для шаринга одним экземпляром между множеством будущих Scope Engines.

---

# 12. `EcaRuleExecutionGroup<TEventContext>`

Group сделать generic.

Нужен также non-generic interface для heterogeneous Registry.

## Non-generic interface

Концептуально:

```csharp
public interface IEcaRuleExecutionGroup
{
    string RuleId { get; }

    EcaRuleExecutionGroupState State { get; }
}
```

## Generic Group

Концептуально:

```csharp
public sealed class EcaRuleExecutionGroup<TEventContext>
    : IEcaRuleExecutionGroup
{
    public string RuleId { get; }

    public IEcaRule<EcaExecutionContext<TEventContext>> Rule { get; }

    public EcaRuleExecutionGroupState State { get; }

    public IReadOnlyList<EcaRuleExecution<TEventContext>> Executions { get; }

    public EcaOverlap Overlap { get; }

    public EcaRuleExecutionGroup(
        IEcaRule<EcaExecutionContext<TEventContext>> rule,
        EcaOverlap overlap,
        IEcaExecutionExecutor executor);

    public void Fire(
        EcaExecutionContext<TEventContext> context);

    public Task Cancel(
        EcaRuleExecution<TEventContext> execution);

    public Task CancelAll();

    public void Close();
}
```

## Ответственность Group

После того как Condition уже прошла, Engine вызывает:

```csharp
group.Fire(context);
```

Далее именно Group:

- применяет текущую overlap policy;
- решает создавать execution или нет;
- создает `EcaRuleExecution<TEventContext>`;
- хранит ее;
- передает execution в `IEcaExecutionExecutor`.

Сейчас реализованы только:

```text
EcaOverlap.Ignore
EcaOverlap.Allow
```

Их поведение сохранить.

**Не реализовывать сейчас:**

- Reset;
- Queue;
- custom overlap strategies;
- priority;
- retry;
- timeout;
- max concurrency;
- pause;
- dependencies;
- sequence/parallel.

---

# 13. `EcaRuleExecutionRegistry`

Остается ключованным по `RuleId`.

Не переходить на object/reference keys.

Нужен heterogeneous storage, например:

```csharp
Dictionary<string, IEcaRuleExecutionGroup>
```

Registry должен создавать Group при регистрации.

Концептуальный API:

```csharp
public interface IEcaRuleExecutionRegistry
{
    void Register<TEventContext>(
        IEcaRule<EcaExecutionContext<TEventContext>> rule,
        EcaOverlap overlap,
        IEcaExecutionExecutor executor);

    EcaRuleExecutionGroup<TEventContext> Get<TEventContext>(
        string ruleId);

    bool TryGet<TEventContext>(
        string ruleId,
        out EcaRuleExecutionGroup<TEventContext> group);

    bool Remove(string ruleId);

    void Clear();
}
```

Implementation должна валидировать тип при generic `Get/TryGet`, а не делать небезопасный silent cast.

---

# 14. Новый `EcaExecutionEngine`

Execution Engine теперь связывает:

```text
RuleRegistry / RuleSelector / RuleChecker
                ↕
        ExecutionRegistry
```

Он не должен знать детали:

- создания конкретной Execution;
- Ignore/Allow lifecycle;
- Execute lifecycle;
- Cancel lifecycle;
- Queue/Reset.

## Constructor

Концептуально:

```csharp
public EcaExecutionEngine(
    IEcaRuleRegistry ruleRegistry,
    IEcaRuleSelector ruleSelector,
    IEcaRuleChecker ruleChecker,
    IEcaRuleExecutionRegistry executionRegistry,
    IEcaExecutionContextFactory contextFactory,
    IEcaExecutionExecutor executionExecutor);
```

Default constructor может создавать стандартные implementation.

## Register

При Register:

1. Rule регистрируется в `EcaRuleRegistry`;
2. Group регистрируется/создается в `EcaRuleExecutionRegistry`;
3. в Group передается общий `IEcaExecutionExecutor`;
4. если второй шаг упал, нужен rollback регистрации Rule — сохранить текущее атомарное поведение.

## Fire

Pipeline должен быть:

```text
1. RuleSelector.ForEvent(...)
2. Для каждого Rule:
   - найти Group по RuleId
   - ContextFactory.Create(eventContext, group.State)
   - Checker.Check(rule, context)
   - если true: сохранить пару Group + Context

3. Только ПОСЛЕ проверки всех Conditions:
   - для каждой прошедшей пары вызвать Group.Fire(context)
```

Таким образом сохраняется фундаментальная семантика:

```text
all Conditions of one Fire
before
any Actions of that Fire
```

---

# 15. `Unregister` — НЕ додумывать сейчас

Это отдельная незакрытая архитектурная задача.

Проблема:

```text
Rule может быть Unregister,
когда его Execution еще Running/Cancelling/Pending
```

Не придумывать сейчас окончательную policy:

- auto cancel;
- wait;
- remove immediately;
- close until idle.

Сохранить текущее поведение настолько близко, насколько возможно, и оставить явный TODO, если требуется.

В будущем планируется lifecycle вида:

```text
Active → Closing → Disposed
```

но сейчас это не реализовывать без отдельного решения.

---

# 16. `EcaContextType`

Не удалять и не переписывать сейчас.

Он нужен для определения `TEventContext` из полного `TContext`, реализующего:

```csharp
IEcaContext<TEventContext>
```

Например:

```text
EcaExecutionContext<PlayerHitEvent>
→ PlayerHitEvent
```

Он используется для проверки совместимости типа Context Rule с `Event.EventContextType`.

В рамках этого рефакторинга:

- сохранить его;
- не переносить query responsibility обратно в Registry;
- не добавлять новые reflection-механизмы без необходимости.

---

# 17. Smoke test

Обновить:

```text
Runtime/Unity/EcaSystemsSmokeTest.cs
```

под новый API и новый naming.

Сохранить существующие смысловые проверки:

- Base empty context;
- Base event context;
- Condition false;
- all Conditions before Actions;
- duplicate RuleId;
- Pending;
- Ignore;
- Allow;
- live GroupState;
- failed Action/execution lifecycle.

Контрольная точка до рефакторинга была:

```text
=== EcaSystems Smoke Tests Finished: PASS 40, FAIL 0 ===
```

После изменения архитектуры smoke-test снова должен компилироваться и проходить без FAIL.

Если количество тестов пришлось изменить исключительно из-за нового API — не скрывать это: указать в итоговом отчете старое и новое количество.

---

# 18. Что НЕ делать в этой задаче

Не реализовывать:

- Scope layer;
- `EcaScopeEngine`;
- global / scene / local scopes;
- entity-scoped overlap;
- Queue;
- Reset;
- полноценный Action/Command cancellation contract;
- Commands;
- Systems;
- Global State;
- TimeSystem;
- PauseSystem;
- Visual programming;
- Event cycles/reentrant Fire protection;
- Priority;
- Retry;
- Timeout;
- MaxConcurrency;
- Pause execution;
- ExecutionHistory;
- Dependencies;
- Sequences;
- Parallel;
- save/load.

Это отдельные будущие этапы.

---

# 19. Желаемый итоговый layout Core

Ориентировочно:

```text
Runtime/Core/
├── Base/
│   ├── IEcaRule.cs
│   ├── EcaRule.cs
│   ├── IEcaRuleRegistry.cs
│   ├── EcaRuleRegistry.cs
│   ├── IEcaRuleSelector.cs
│   ├── EcaRuleSelector.cs
│   ├── IEcaRuleChecker.cs
│   ├── EcaRuleChecker.cs
│   ├── IEcaRuleRunner.cs
│   ├── EcaRuleRunner.cs
│   ├── IEcaAction.cs
│   ├── IEcaCondition.cs
│   ├── EcaContext.cs
│   └── EcaEngine.cs
│
├── Execution/
│   ├── EcaExecutionContext.cs
│   ├── IEcaExecutionContextFactory.cs
│   ├── EcaExecutionContextFactory.cs
│   ├── EcaExecutionEngine.cs
│   ├── IEcaExecutionExecutor.cs
│   ├── EcaExecutionExecutor.cs
│   ├── EcaRuleExecution.cs
│   ├── EcaRuleExecutionGroup.cs
│   ├── EcaRuleExecutionGroupState.cs
│   ├── EcaRuleExecutionRegistry.cs
│   ├── EcaRuleExecutionStatus.cs
│   └── EcaOverlap.cs
│
└── Helpers/
```

Не создавать отдельные `.asmdef` для Base/Execution. Это логические слои одного `EcaSystems.Core`.

---

# 20. Перед завершением работы

Обязательно:

1. изучить текущие локальные файлы до изменений;
2. не ориентироваться на старые snippets, если локальный код новее;
3. убедиться, что все старые ссылки на `EcaRuleExecutionState` заменены на `EcaRuleExecutionGroupState`;
4. убедиться, что Base больше нигде не использует `CancellationToken`;
5. убедиться, что `EcaRuleRegistry` больше не делает выборку по Event;
6. убедиться, что все Conditions одного Fire проверяются до любых Actions;
7. убедиться, что Execution создается внутри Group, а не Engine;
8. обновить smoke-test;
9. проверить Unity compilation;
10. прогнать smoke-test;
11. в конце показать:
    - список измененных/добавленных/переименованных файлов;
    - краткое описание изменений;
    - результат compilation/tests;
    - оставшиеся TODO, но не реализовывать их.
