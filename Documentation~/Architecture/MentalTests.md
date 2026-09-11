# EcaSystems — Mental Architecture Tests

> **Исторический архитектурный документ (historical architecture document).** Содержит устаревшие решения и не является source of truth. Актуальные решения: [Context.md](../Context.md). Название минимального engine обновлено до EcaBaseEngine; остальные рассуждения сохранены как история.

> Status: architecture checkpoint before implementation of `EcaBaseEngine`.
>
> This document records mental tests of the current EcaSystems Core model, including passed scenarios, deferred features, edge cases, and implementation constraints discovered during testing.

## Current Core Model

### `EcaEvent<TEventContext>`

A static typed declaration describing an external event.

Responsibilities:

- `Id`
- `Name`
- optional `Description`
- `TEventContext` defines the immutable context type associated with the event

An `EcaEvent` is **not** a C# event, callback, signal, or executable trigger.

The original event may come from anywhere outside EcaSystems:

- C# event
- Unity callback
- `UnityEvent`
- third-party library callback
- network event
- arbitrary application code

An adapter reports the occurrence to EcaSystems through `EcaBaseEngine.Fire(event, context)`.

For events without data, use `EcaEventContextEmpty`.

### `EcaRule<TEventContext>`

A passive declaration of a reaction to an event.

```text
WHEN Event
IF Condition
DO Action
```

Responsibilities:

- `Id`
- `Name`
- optional `Description`
- `Event`
- optional `Condition`
- `Action`
- execution policy such as `Overlap` may initially live on the rule for simplicity

`EcaRule` does not subscribe to external events, fire itself, start or stop itself, own active executions, own cancellation, or perform routing.

### `IEcaCondition<TEventContext>`

A synchronous predicate:

```text
Check(context) -> bool
```

Requirements:

- receives the immutable event context
- should have no side effects
- should not perform asynchronous work

### `IEcaAction<TEventContext>`

A single reaction callback:

```text
Run(context, cancellationToken) -> Task
```

The `Task` is analogous to `Promise<void>`. It does not represent a business result. It represents only the lifecycle of the action: running, completed, failed, or cancelled.

The action starts immediately when `Run` is called. As in JavaScript `async` functions, synchronous code runs immediately until the first incomplete `await`.

There is exactly **one Action per EcaRule**. Normal C# inside that Action provides local variables, branching, loops, sequential awaits, parallel tasks, and calls to future Commands.

### `EcaRuleExecution<TEventContext>`

Represents one concrete execution of one Rule.

```text
Rule1
├── Execution1-1
└── Execution1-2

Rule2
├── Execution2-1
└── Execution2-2
```

Likely responsibilities:

- execution `Id`
- `Rule`
- immutable event `Context`
- status
- cancellation
- possibly exception/error information

### `EcaBaseEngine`

Central runtime service.

Conceptual storage:

```text
ecaRules:
    Map<EventId, List<EcaRule>>

activeExecutions:
    Map<RuleId, List<EcaRuleExecution>>
```

Conceptual API:

- `Register(rule)`
- `Unregister(rule)`
- `Fire(event, context)`

Core routing:

```text
External event
    ↓
adapter
    ↓
EcaBaseEngine.Fire(Event, immutable Context)
    ↓
find Rules by EventId
    ↓
Check ALL matching Conditions
    ↓
freeze the list of Rules that passed
    ↓
apply overlap policy per Rule
    ↓
create RuleExecutions
    ↓
run Actions
```

### Important dispatch semantic

For one `Fire`, **all Conditions are checked before any Actions are started**.

This is intentional. Rules react to an event; they are not procedural steps in a sequence. An Action from Rule A must not change whether Rule B responds to the same occurrence after Rule B's condition has already been evaluated.

## Result Legend

- ✅ **PASS** — works with the current model without changing Core
- 🟡 **PARTIAL / DEFERRED** — architecture supports it, but an additional future mechanism is needed
- ❌ **NOT SUPPORTED** — intentionally unsupported or requires a fundamentally different execution model

# Mental Tests

## 1. Basic ECA

1. **One Event → one Rule → Condition true → Action** — ✅ PASS. Normal route: `Fire → Check → Execution → Action`.
2. **Condition false** — ✅ PASS. No execution is created.
3. **Rule without Condition** — ✅ PASS. Action is eligible immediately after event matching and overlap checks.
4. **One Event → multiple Rules** — ✅ PASS. All matching Conditions are checked first, then qualifying Rules create executions.
5. **Different Events use the same Context type** — ✅ PASS. Routing is by `EventId`, not context type.
6. **Same Event fires repeatedly with different Context values** — ✅ PASS. Each `Fire` receives its own immutable context.
7. **Event has no registered Rules** — ✅ PASS. `Fire` becomes a no-op after lookup.
8. **Several unrelated Events fire independently** — ✅ PASS.

## 2. Event Identity and Type Safety

9. **Wrong context passed to an Event** — ✅ PASS if `Fire` is generic and couples `EcaEvent<TEventContext>` with `TEventContext`.
10. **Two Event declarations accidentally use the same `EventId`** — 🟡 validation required. Incompatible duplicate IDs should be rejected.
11. **Event without context data** — 🟡 API ergonomics only. Use `EcaEventContextEmpty`.

## 3. Event Dispatch Semantics

12. **One `Fire` shares one immutable Context across all matching Rules** — ✅ PASS.
13. **Action A changes state used by Condition B for the same Event** — ✅ PASS with the fixed two-phase semantic: check all Conditions first, then start Actions.
14. **Rule ordering** — ✅ PASS conceptually. Rules are independent reactions. Iteration should still be deterministic for debugging.
15. **Condition has side effects** — 🟡 allowed by C#, forbidden by contract/documentation. Conditions should be side-effect-free predicates.
16. **Rule registered during an active `Fire`** — 🟡 implementation detail. Current dispatch should use a snapshot/frozen set of matching Rules; the new Rule starts participating on future `Fire` calls.
17. **Rule unregistered during an active `Fire`** — 🟡 implementation detail. Same snapshot rule applies. Existing execution lifecycle is a separate policy.

## 4. Action Semantics

18. **Fully synchronous Action** — ✅ PASS. It can return a completed `Task`.
19. **Synchronous work before first `await`** — ✅ PASS. `Run` starts immediately and executes synchronously until the first incomplete await, analogous to JavaScript async functions.
20. **Sequential Commands inside one Action** — ✅ PASS. Ordinary sequential `await`.
21. **Command returns a value** — ✅ PASS. Keep it as a local variable inside Action; no result-bearing Action interface is needed.
22. **Local variables** — ✅ PASS. Use C# locals.
23. **Constants** — ✅ PASS. Use literals, fields, constructors, or closures.
24. **External application state** — ✅ PASS. Conditions/Actions may reference external state/systems.
25. **`if / else`** — ✅ PASS inside Action.
26. **`while`** — ✅ PASS inside Action.
27. **`foreach`** — ✅ PASS inside Action.
28. **Sequential flow** — ✅ PASS through normal `await`.
29. **Parallel flow** — ✅ PASS through normal Task composition.
30. **Race / timeout** — ✅ PASS at Core level using async/cancellation primitives; helpers can come later.
31. **Blocking vs non-blocking operation** — ✅ PASS. Action decides whether to await an operation.

## 5. Overlap and Multiple Executions

32. **`Overlap.Allow`** — ✅ PASS. One Rule may have multiple simultaneous `EcaRuleExecution`s.
33. **`Overlap.Ignore`** — ✅ PASS. If `activeExecutions[RuleId].Count > 0`, no new execution is created.
34. **Different Rules for the same Event use different overlap policies** — ✅ PASS. Overlap is evaluated per Rule.
35. **Future `Overlap.Reset`** — 🟡 supported by the model. Cancel existing execution(s), then start a new one. Per-execution cancellation is the natural mechanism.
36. **Future `Overlap.Queue`** — 🟡 additional policy state required. Pending event contexts/occurrences must be queued per Rule.
37. **Stateful Action with `Overlap.Allow`** — 🟡 API rule required. Execution-local mutable state should live inside `Run()` locals; shared mutable Action fields can create races.
38. **Cancellation of one of several active executions** — ✅ PASS architecturally. Each execution owns its own cancellation source/token.

## 6. Reentrancy and Recursive Events

39. **Action fires another EcaEvent** — ✅ PASS. The nested event is routed normally.
40. **Action fires the same Event synchronously** — 🟡 edge case. With `Ignore`, the execution must be registered as active before calling `Action.Run`. With `Allow`, recursive `Fire` can cause synchronous recursion until an await yields. A future queued-dispatch mechanism could avoid stack growth if needed.

## 7. Errors and Lifecycle

41. **Action throws/faults** — 🟡 error policy required. Execution becomes `Failed`; failure of Rule A must not prevent unrelated Rule B from executing.
42. **Action is cancelled** — ✅ PASS architecturally. Execution becomes `Cancelled`.
43. **Rule is unregistered while its Action is running** — 🟡 policy not yet fixed. Likely: block new executions and cancel current ones.
44. **Duplicate Rule registration** — 🟡 validation required. Prefer unique Rule IDs per `EcaBaseEngine`.

## 8. External Event Integration

45. **C# event** — ✅ PASS. Adapter handler calls `EcaBaseEngine.Fire`.
46. **Unity callback** — ✅ PASS. `Start`, `OnTriggerEnter`, `OnCollisionEnter`, etc. can call `Fire` through an adapter.
47. **`UnityEvent`** — ✅ PASS. Listener calls `Fire`.
48. **Third-party callback API** — ✅ PASS. Any callback can report an EcaEvent to the Engine.
49. **External event arrives from a background thread** — 🟡 Unity integration issue. Either require main-thread `Fire` in Unity or marshal callbacks before firing. Core should not become Unity-thread-aware prematurely.

## 9. External Async Systems / Future Commands

50. **Callback-based asynchronous library** — ✅ compatible. Future Command adapter can promisify completion into a `Task`.
51. **External API already returns `Task`** — ✅ compatible.
52. **Unity API returns `Awaitable`** — ✅ compatible through Unity integration/Commands.
53. **Cancellation through external library** — 🟡 adapter responsibility. Command maps Eca cancellation to the library's own mechanism.

## 10. Old GFlow Flow Constructs

54. **EventBlock** — ✅ not needed in Core. Code-first semantics are ordinary Action code. EventBlock remains a likely Visual-layer concept.
55. **EventBlock condition evaluated after state mutation** — ✅ PASS. Ordinary imperative C# naturally observes current state inside Action.
56. **AND / OR / NOT condition tree** — ✅ PASS in Core with ordinary predicate logic. Visual layer may need a serializable expression tree.
57. **Templates** — ✅ PASS in Core through normal functions/factories/builders/closures. Visual layer may later expose typed Template slots.

## 11. Persistent State Features

58. **Gate** — ✅ / 🟡 does not require a Core primitive. A Gate may be ordinary external state checked by a Condition; a dedicated API can be added later for semantics/debugging.
59. **DoOnce** — 🟡 works through stateful code-first objects/closures; persistence/serialization is deferred.
60. **DoN / counter** — 🟡 same as DoOnce.
61. **Save/load persistent Rule state** — 🟡 future state layer. May become necessary for DoOnce, DoN, persistent Gates, counters, Visual state, EventGroup state.
62. **Save in the middle of an active Action and resume later** — ❌ intentionally unsupported. Task continuations are not serialized; current requirements do not need this.

## 12. Signals and Coordination

63. **Signal / WaitSignal** — 🟡 future feature. Could be an awaitable command/signal primitive or another EcaEvent depending on semantics.
64. **Multiple Events reuse the same Condition/Action** — ✅ PASS. Multiple Rules may reference the same reusable objects.
65. **One logical Rule reacting to several Events with shared overlap/state** — 🟡 not equivalent to simple reuse. May require a future multi-event Rule or higher-level construct.

## 13. Future Higher-Level Systems

66. **FSM** — 🟡 higher layer. ECA can drive transitions, but FSM owns current state, enter/exit semantics, and transition rules.
67. **Event Pages** — 🟡 higher/Visual layer. RPG Maker-style pages can be built above ECA.
68. **EventGroup** — 🟡 future runtime entity. Shared state, activation, overlap, gates, or coordination may require group-level runtime state.
69. **MultiGate / Router / Random / Cycle / Loop** — 🟡 flow/Visual feature. Code-first usage can express these inside Action.
70. **ExecutionPolicy / middleware** — ✅ current central Engine architecture supports it well. Future policies could intercept Rule/Action execution without changing the ECA model.

## 14. Visual Programming

71. **Visual Action flow** — ✅ Core compatible. Visual layer can implement `IEcaAction<TContext>` backed by Sequence, If, Else, While, ForEach, Parallel, Commands, locals, etc.
72. **Visual Conditions** — 🟡 Visual representation required for serializable AND/OR/NOT trees and reusable conditions.
73. **Visual local variables** — 🟡 Visual feature; Core code-first Action uses normal locals.
74. **Visual Templates** — 🟡 Visual feature that can ultimately create normal `EcaRule` definitions.
75. **JSON/data-authored Rules** — 🟡 future serialization layer. Stable `EventId` and `RuleId` are useful foundations; typed contexts and authored Conditions/Actions are the harder problem.

## 15. Quest and Gameplay System Integration

76. **Quest-like external system** — ✅ Core compatible. External quest events can be adapted into EcaEvents; EcaSystems itself should not know what a Quest is.
77. **Area / interaction / scene events** — ✅ PASS. Previous Trigger concepts become static EcaEvent declarations, with external adapters reporting occurrences to the Engine.

## 16. Complex Gameplay Flow

78. **Data-driven encounter** — ✅ PASS. An Event can start a Rule whose Action performs async Commands, updates state, and fires another Event.
79. **Several external operations active at once** — ✅ PASS. Different Rule executions may independently be waiting, moving, animating, or performing external work.

# Key Findings

## 1. One Action per Rule is the correct simplification

The previous multi-Action Rule forced Core to reinvent sequencing, local variables, branching, command results, and flow control.

The current model instead uses one programmable async Action. Ordinary C# owns procedural flow.

## 2. `EcaEvent` is a declaration, not an event source

The original event exists outside EcaSystems. Integration code reports it with:

```text
EcaBaseEngine.Fire(EcaEvent, immutable Context)
```

This keeps Core independent of C# events, Unity callbacks, UnityEvent, and third-party event APIs.

## 3. Route by Event identity, not Context type

`EventId` is the routing identity. `TEventContext` exists for type safety. Different Events may intentionally share a context type.

## 4. Check all Conditions before starting any Actions

For one `Fire`:

```text
find matching Rules
↓
Check all Conditions
↓
freeze passing Rules
↓
start Actions
```

This intentionally preserves event semantics rather than procedural Rule ordering.

## 5. Active executions are enough state for `Allow` / `Ignore`

For v0:

```text
activeExecutions:
    RuleId → List<Execution>
```

is sufficient for `Overlap.Allow`, `Overlap.Ignore`, `IsRunning`, and active execution count.

Do not add a persistent `EcaRuleState` only for these features.

Persistent Rule state may become justified later by DoOnce, DoN, save/load, EventGroup, or Visual state.

## 6. Cancellation belongs to an execution

`IEcaAction.Cancel()` becomes ambiguous when `Overlap.Allow` creates multiple simultaneous runs of the same Action object.

Each `EcaRuleExecution` should instead have its own cancellation source/token. This also naturally supports future `Overlap.Reset`.

## 7. Main implementation concerns before/during `EcaBaseEngine`

1. Event ID collision validation.
2. Rule ID duplicate validation.
3. Heterogeneous generic Rule storage in C#.
4. Snapshotting matching Rules during a `Fire`.
5. Checking all Conditions before starting any Action.
6. Registering an execution as active before calling `Action.Run`.
7. Correct cleanup/removal from `activeExecutions`.
8. Error/failure handling without breaking unrelated Rules.
9. Reentrant `Fire`, especially with `Overlap.Allow`.
10. Unity main-thread adaptation for callbacks raised from background threads.
11. Exact `Unregister` semantics for active executions.

None of these currently require changing the basic ECA model.

# Architecture Verdict

The current model passes the core architecture tests:

```text
EcaEvent<TContext>
    static typed event declaration

EcaRule<TContext>
    Event + Condition + Action

IEcaCondition<TContext>
    Check(context)

IEcaAction<TContext>
    Run(context, CancellationToken) -> Task

EcaRuleExecution<TContext>
    one concrete reaction execution

EcaBaseEngine
    Rule registry
    Event routing
    two-phase Condition/Action dispatch
    overlap
    execution tracking
```

Deferred concepts remain additive layers rather than requirements of Core:

- Commands
- Modules
- Visual programming
- EventBlock
- Templates
- Gates
- persistent Rule state
- Signals
- FSM
- EventGroup
- JSON/data authoring
- execution middleware
- `Reset` / `Queue` overlap modes

This document is the architecture baseline immediately before implementation of `EcaBaseEngine`.
