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

---

# 21. Execution-layer cancellation contract для Action — ДОБАВИТЬ СЕЙЧАС

В Base cancellation отсутствует:

```csharp
public interface IEcaAction<in TContext>
{
    Task Run(TContext context);
}
```

Но в Execution-слое нужен отдельный capability interface для пользовательской отмены Action.

## Новый `IEcaCancellableAction<TContext>`

Концептуально:

```csharp
public interface IEcaCancellableAction<in TContext>
{
    Task Cancel(TContext context);
}
```

Это отдельный интерфейс Execution-слоя. Он **не должен** добавляться в Base.

Конкретная Action Execution-слоя при необходимости может реализовывать оба интерфейса:

```csharp
public sealed class SomeAction :
    IEcaAction<EcaExecutionContext<SomeEventContext>>,
    IEcaCancellableAction<EcaExecutionContext<SomeEventContext>>
{
    public Task Run(EcaExecutionContext<SomeEventContext> context)
    {
        ...
    }

    public Task Cancel(EcaExecutionContext<SomeEventContext> context)
    {
        ...
    }
}
```

Причина отдельного `Cancel(context)`:

- один экземпляр Action может участвовать в нескольких одновременных executions при `Allow`;
- без context вызов `Cancel()` не адресует конкретный запуск;
- cancellation hook должен быть execution-specific по данным конкретного запуска.

Важно:

- ошибка `Run()` сама по себе **не означает cancellation**;
- обычное исключение должно приводить к `Failed`;
- `Cancel()` вызывается только в cancellation-сценарии framework.

---

# 22. `EcaExecutionExecutor.Cancel(...)` должен вызывать пользовательский cancellation hook

`IEcaExecutionExecutor` по-прежнему:

```csharp
public interface IEcaExecutionExecutor
{
    Task Execute<TEventContext>(
        EcaRuleExecution<TEventContext> execution);

    Task Cancel<TEventContext>(
        EcaRuleExecution<TEventContext> execution);
}
```

Внутри `Cancel(...)` нужно использовать lifecycle Execution и пользовательский capability interface.

Концептуально:

```text
Running
↓
MarkCancelling
↓
RequestCancellation
↓
если execution.Rule.Action implements IEcaCancellableAction<TContext>
    await action.Cancel(execution.Context)
↓
MarkCancelled
```

Точный порядок internal cleanup можно адаптировать к актуальному локальному коду, но semantics должны быть именно такими:

- `Cancelling` — промежуточный статус;
- `Cancelled` — финальный статус;
- `Cancel()` пользовательской Action может быть async;
- framework ожидает завершения `Cancel()` перед финальным `Cancelled`;
- cancellation не должна автоматически запускаться при обычном failure.

Если Action не реализует `IEcaCancellableAction<TContext>`, framework не должен придумывать пользовательскую cleanup-логику за неё.

---

# 23. ВАЖНО: канал передачи `CancellationToken` в Action пока НЕ РЕШАТЬ

После удаления `CancellationToken` из Base API остаётся незакрытый вопрос:

```text
EcaRuleExecution имеет CancellationToken,
но IEcaAction.Run(context) его больше не принимает.
```

Сейчас `CancellationToken` оставляем в:

```csharp
EcaRuleExecution<TEventContext>
```

но **не придумываем самостоятельно**, как он попадет внутрь пользовательской Action.

Не делать без отдельного архитектурного решения:

- не добавлять token обратно в Base `IEcaAction`;
- не добавлять token обратно в Base `IEcaRuleRunner`;
- не засовывать token в `EcaExecutionContext` только ради закрытия compile/API gap;
- не создавать новый Action API с token без отдельного решения;
- не менять lifecycle архитектуру ради этого вопроса.

Если текущая реализация `EcaExecutionExecutor` может работать без передачи token в Action — сохранить token внутри Execution как часть будущего cancellation API.

Это **явный TODO**, а не задача Work самостоятельно закрыть архитектурным решением.

---

# 24. `EcaRuleExecutionGroupState` — будущая read-only информация о executions

Сейчас после rename:

```text
EcaRuleExecutionState
→ EcaRuleExecutionGroupState
```

GroupState хранит live состояние всей группы executions одного Rule.

Существующие counters сохранить:

```csharp
EcaRuleExecutionTotalStarted
EcaRuleExecutionTotalFinished
```

Архитектурное требование на будущее:

Action через `EcaExecutionContext.RuleExecutionGroupState` должна в дальнейшем иметь возможность получить read-only информацию вроде:

```text
- текущие executions группы;
- statuses executions;
- previous execution;
- active/pending counts;
```

Но **конкретный API сейчас не проектировать и не реализовывать самостоятельно**.

Не отдавать наружу mutable внутренние коллекции.

Сейчас достаточно:

- корректного rename;
- сохранения live GroupState;
- существующих counters;
- возможности расширить state позже без изменения базовой модели context.

---

# 25. Future Commands: cancellation обязателен как архитектурное требование

Commands сейчас **не реализовывать**.

Но зафиксировать будущую semantics:

- у каждой асинхронной Command должен быть cancellation-сценарий;
- конкретная Command сама определяет cleanup при отмене;
- пользовательская система сама определяет, как отменять внутреннюю последовательность Commands;
- framework не должен жёстко навязывать одну стратегию cleanup;
- cancellation конкретной Command должна быть execution-specific.

Например в будущем Action может выполнять:

```text
Command A
↓
Command B
↓
Command C
```

и при cancellation пользователь решает:

```text
- остановить текущую Command;
- выполнить cleanup;
- не запускать последующие Commands;
- дождаться нужного async cleanup;
```

Это только TODO/архитектурная запись. Не создавать Command API в текущей задаче.

---

# 26. Lifecycle / Dispose — обязательная задача ДО Scope-слоя, но НЕ реализовывать сейчас

Текущая архитектура должна быть подготовлена к будущему lifecycle:

```text
Scope destroyed
↓
RuleRegistry releases Rules / Conditions / Actions
↓
ExecutionRegistry releases Groups
↓
Groups корректно завершают или отменяют executions
```

Перед созданием Scope-слоя обязательно спроектировать полноценный lifecycle для:

```text
EcaRuleRegistry
EcaRuleExecutionRegistry
EcaRuleExecutionGroup
EcaExecutionEngine / future Scope
```

Будущая возможная модель:

```text
Active
↓
Closing
↓
Disposed
```

Но сейчас **не реализовывать окончательную Dispose/Close semantics без отдельного решения**.

Особенно не принимать самостоятельно решение:

- cancel active executions on unregister;
- wait for them;
- let them finish;
- remove group immediately;
- keep closing group until idle.

Текущий `Unregister` lifecycle остаётся отдельным TODO.

---

# 27. Registry ownership и сильные ссылки

`EcaRuleRegistry` хранит Rules, а Rules содержат ссылки на Conditions/Actions.

Поэтому lifecycle registry принципиально важен:

```text
registry жив
→ Rule жив
→ Condition/Action живы
```

Это одна из причин будущего Scope-слоя с отдельными registry на scope.

В текущей задаче:

- не переходить на weak references;
- не ключевать ExecutionRegistry по object reference Rule;
- `EcaRuleExecutionRegistry` остаётся key-by-RuleId;
- просто не забывать, что `Clear/Dispose` registry потребуется до Scope.

---

# 28. Future reentrant/cyclic event protection — только TODO

Позже нужен механизм диагностики/защиты от event cycles:

```text
Event A
→ Action
→ Event B
→ Action
→ Event A
→ ...
```

или прямой reentrant вызов:

```text
Event A
→ Action
→ Event A
```

Сейчас:

- не реализовывать cycle detector;
- не добавлять depth limit;
- не менять Fire semantics ради этого.

Только сохранить TODO.

---

# 29. Future Execution flexibility mental tests — только TODO

После завершения текущего Execution refactor, но до реализации сложных policies, провести отдельные mental experiments:

```text
Priority
Retry
Timeout
MaxConcurrency
Pause
ExecutionHistory
Dependencies
Sequences
Parallel
```

Цель:

- проверить, можно ли добавить эти возможности локально;
- убедиться, что `ExecutionGroup + Executor + Policy` архитектура не требует капитальной переделки;
- определить, какие из этих возможностей относятся к Group, Executor, Scheduler, middleware или отдельным policies.

Сейчас ничего из этого не реализовывать.

---

# 30. Дополнение к финальной проверке Work

Перед завершением дополнительно проверить:

1. в Base нет `CancellationToken` в `IEcaAction` и `IEcaRuleRunner`;
2. в Execution добавлен `IEcaCancellableAction<TContext>`;
3. `EcaExecutionExecutor.Cancel(...)` использует `IEcaCancellableAction<TContext>` при наличии;
4. обычное exception из `Run()` приводит к `Failed`, а не вызывает пользовательский `Cancel()`;
5. добавлен `Cancelling` и он не смешан с финальным `Cancelled`;
6. `CancellationToken` остаётся частью `EcaRuleExecution`, но Work **не придумал новый канал передачи token в Action**;
7. `EcaRuleExecutionGroupState` остаётся live state группы, не одной execution;
8. read-only execution inspection для GroupState помечен как future TODO, но не реализован самовольно;
9. Command cancellation записан как future requirement, но Commands не добавлены;
10. lifecycle/Dispose перед Scope явно оставлен TODO;
11. reentrant/cyclic Fire detection не реализован;
12. Priority/Retry/Timeout/MaxConcurrency/Pause/History/Dependencies/Sequences/Parallel не реализованы.

