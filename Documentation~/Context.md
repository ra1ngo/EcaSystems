# EcaSystems - Development Context

## Purpose

EcaSystems is a Unity-first UPM framework for interaction between independent Systems through ECA. Core has no Unity dependency. Base remains a useful standalone minimal ECA layer. Execution v1 is complete; Scope is the next separate stage.

This document records current agreed decisions. [ToDo.md](ToDo.md) contains prerequisites for first practical use; [Roadmap.md](Roadmap.md) contains optional future capabilities. Architecture/ documents are retained discussion history and may describe superseded decisions; they do not override this context.

## Current architecture

```text
external source
  -> EcaEvent<TEventContext>
  -> Fire
  -> Rules
  -> Conditions
  -> Actions
```

EcaEvent is a typed declaration with an Id, not a C# event or event source. External integration calls Fire. Routing uses Event.Id with type compatibility validation. Rule consists of Event, optional Condition and one Action. Condition.Check returns bool; Action.Run returns a Task representing completion/failure, not a business result.

- EcaRuleRegistry: storage, registration and validation.
- EcaRuleSelector: selection/query.
- IEcaRuleChecker / EcaRuleChecker: check a single Rule.
- IEcaRuleRunner / EcaRuleRunner: invoke Action.Run(context).
- EcaContextType: retained for type validation.
- EcaEngine uses Base contexts; EcaExecutionEngine uses execution contexts with live GroupState.

## Core invariants

- All Conditions of one Fire are checked before any Actions of that Fire: collect passing Rules/Groups first, then run Actions.
- EventContext is the logically immutable payload of one Fire; it is not copied.
- RuleId is unique in RuleRegistry; incompatible context types are rejected.
- If ExecutionGroup registration fails, Engine rolls back Rule registration.
- Base Action.Run(context) and RuleRunner.Run(rule, context) take no cancellation token.
- Core has no knowledge of Unity, Entity, Global State or concrete Systems.

## Current Execution v1

Execution tracks concrete long-running Rule invocations over time.

```text
EcaExecutionEngine
  -> EcaRuleExecutionRegistry
  -> EcaRuleExecutionGroup<TEventContext>
  -> EcaRuleExecution<TEventContext>
```

Each Group belongs to one Rule and holds RunMode, live GroupState and active executions. It directly uses a shared IEcaRuleRunner and owns lifecycle: create -> Running -> Completed/Failed -> IncrementFinished -> remove from active list. MarkRunning increments Started before Run. Exceptions, including OperationCanceledException, are ordinary Failed results. A null Task from the runner also produces failure.

EcaRuleExecution holds Id, RuleId, Rule, Context, Status and Exception. Lifecycle is Pending -> Running -> Completed | Failed. Pending intentionally remains as a brief initial state. Public IReadOnlyList<EcaRuleExecution<TEventContext>> Executions intentionally remains; it is an active list, not history.

RunMode is the single configuration object:

| Setting | Semantics |
| --- | --- |
| Overlap.Ignore | Ignore a new run while an execution is active |
| Overlap.Allow | Allow simultaneous executions in the group |
| Limit = -1 | Unlimited; default |
| Limit = 0 | Create no executions |
| Limit = N > 0 | At most N actual starts over the group lifetime |
| Limit < -1 | ArgumentOutOfRangeException |

Group checks Limit before creating an execution, then applies the overlap switch. Ignored Fire does not consume Limit. Engine checks Conditions before Group applies Limit/Overlap. GroupState only has EcaRuleExecutionTotalStarted and EcaRuleExecutionTotalFinished, both live counters. Failure consumes an actual start and increments Finished too.

## Decisions and why

### Immediate Unregister

```text
Unregister(rule)
  -> remove Rule from RuleRegistry
  -> immediately remove Group from ExecutionRegistry
  -> do not cancel or await running Actions
  -> permit immediate registration of the same RuleId
```

Unregister returns false if Rule removal failed and leaves the Group untouched. Null is rejected by existing RuleRegistry validation. Successful Unregister returns true after removing the Group.

Existing async operations retain the references they need and finish naturally in the old Group. Re-registration creates an independent new Group with fresh GroupState and Limit lifetime. Old/new Actions may temporarily overlap even with Ignore. Old Group completion cannot remove or update the new Group. This is a conscious v1 simplification; async/graceful unregister is on the Roadmap.

### Limit instead of Once / DoN

Once is Limit = 1; DoN is Limit = N. Once per game/scene/NPC is a future Scope lifetime question rather than a separate mode.

### Overlap switch

Only Ignore and Allow are needed. Policy / Strategy / Plan / Scheduler are premature; extensibility remains on the Roadmap.

## Rejected / deferred approaches

Cancellation API, IEcaCancellableAction and IEcaExecutionExecutor were removed from Execution v1. A Task cannot universally be killed; CancellationToken is a cooperative request. Domain-specific cleanup does not guarantee that arbitrary Action.Run stops. Universal Reset over arbitrary Task produced overly complex semantics.

Reset, interrupt, pause/resume and save/load execution may require an explicit step/command/history model with a program counter and serializable state. This is separate future design, not the current architecture. Queue is deferred until a real use case: it is unnecessary for the current minimal practical Execution layer.

V1 has no Closing, UnregisterAsync, execution history, inspection extensions, Priority, Retry, Timeout, MaxConcurrency, Dependencies, Sequences or Parallel. Do not add further Execution features in this stage.

## Scope direction

Scope determines ownership/lifetime of ExecutionGroups and therefore Limit lifetime. Global, scene and local scopes are needed. One Rule definition may have independent groups/state in different local/entity scopes without Entity knowledge in Core.

Event routing between scopes is unresolved. EcaScopeEngine is not a final decision: discuss routing/ownership first and compare a separate engine with composition around EcaExecutionEngine. Scope is not implemented yet.

## Systems direction

Systems architecture follows Scope: a mechanism for integrating independent Systems that provide Events, Conditions, future Commands and context-state extensions. Global State / variables is a System, not a hardcoded Base feature. TimeSystem is the first planned concrete System. Design Commands after stabilizing a practical use case; do not mix them into Base Action in advance.

## Terminology

- System, not Module: provider of ECA primitives/extensions.
- Event: typed declaration; external source is separate.
- Rule: Event + optional Condition + one Action.
- Execution: one concrete Rule run.
- ExecutionGroup: active executions and state of one Rule over the group lifetime.
- RunMode: Overlap + Limit.
- Scope: future ownership/lifetime and routing boundary.
- Commands: future model distinct from current Base Action.

## Current next step

Execution v1 is complete. Next is separate design of Scope ownership, lifetime and event routing as described in ToDo.md. Scope implementation and Execution extensions require a subsequent agreed iteration.
