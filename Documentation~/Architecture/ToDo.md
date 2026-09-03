# EcaSystems — ToDo

> Current checkpoint: base ECA layer + RuleRuntime layer are implemented and the Unity smoke test passes with **40 PASS / 0 FAIL**.
>
> This file records the current architecture decisions and the work intentionally left for later.

## Current stable baseline

- [x] Keep package structure under:
  - `Runtime/Core`
  - `Runtime/Unity`
- [x] Split `Runtime/Core` into:
  - `Base`
  - `RuleRuntime`
  - `Helpers`
- [x] Keep `DelegateEcaCondition` in `Runtime/Core/Helpers`.
- [x] Keep `EcaEventContextEmpty` for events without payload instead of duplicating the public API with non-generic overloads.
- [x] Keep `using EcaRuleId = System.String` as a readability alias for now.
- [x] Keep current constructor/config validation as-is for now.
- [x] Base layer remains fully usable without RuleRuntime.
- [x] Base pipeline is `EcaRuleRegistry -> EcaRuleChecker -> EcaRuleRunner -> EcaEngine`.
- [x] `EcaRuleRegistry` owns `Register`, `Unregister`, and `GetRulesForEvent`.
- [x] `EcaRuleChecker` is stateless and only checks Conditions.
- [x] All Conditions for one `Fire` are evaluated before any Actions are started.
- [x] `EcaRuleRunner` runs one already-filtered Rule and does not store or check anything.
- [x] RuleRuntime layer reuses the same Registry / Checker / Runner components as Base.
- [x] `EcaRuleRuntimeRegistry` owns Rule runtime instances.
- [x] `EcaRuleRuntime` owns active `EcaRuleExecution` instances, execution lifecycle, overlap behavior, and future queue behavior.
- [x] `EcaRuleRuntimeState` lives between executions of one Rule.
- [x] `EcaRuleRuntimeState` currently contains:
  - `EcaRuleExecutionTotalStarted`
  - `EcaRuleExecutionTotalFinished`
- [x] Runtime state passed through context is live, not a snapshot.
- [x] Conditions may see counters before the current execution starts.
- [x] Actions may see the same live state after the current execution has already incremented `Started`.
- [x] `EcaRuleExecutionStatus` currently includes `Pending`, `Running`, `Completed`, `Cancelled`, `Failed`.
- [x] Implement and smoke-test `Overlap.Ignore`.
- [x] Implement and smoke-test `Overlap.Allow`.
- [x] Finished executions are removed from the active execution collection.
- [x] Failed Actions finish their execution lifecycle and increment `Finished`.
- [x] Unity smoke test passes: **40 PASS / 0 FAIL**.

## Next architecture tasks

- [ ] Revisit the temporary name `IEcaRuleOverlapped`.
- [ ] Revisit naming of the second engine (`EcaRuleRuntimeEngine`) after the RuleRuntime API settles.
- [ ] Decide the exact public API for inspecting `EcaRuleRuntime` and active executions.
- [ ] Decide whether `EcaRuleRuntimeRegistry` should expose only lookup methods or additional runtime-management operations.
- [ ] Decide lifecycle semantics for `Unregister` while a Rule has active executions:
  - keep running
  - cancel
  - remove runtime immediately
  - preserve runtime until active executions finish
- [ ] Decide what happens to `EcaRuleRuntimeState` after `Unregister`.
- [ ] Define cancellation behavior explicitly:
  - cancelling one execution
  - cancelling all executions of one Rule
  - cancellation status/error semantics
- [ ] Decide whether execution exceptions need public observation hooks/events in addition to `Failed` status.
- [ ] Revisit the second global architecture problem that was intentionally postponed.

## RuleRuntime expansion

- [ ] Implement and test `Overlap.Reset`.
  - Cancel active execution(s).
  - Create the replacement execution.
  - Verify counter semantics.
- [ ] Design `Overlap.Queue` before implementing it.
  - Decide what is queued before an execution exists.
  - Keep queue ownership inside `EcaRuleRuntime`.
  - Define FIFO semantics.
  - Define queue behavior on failure/cancellation/reset/unregister.
- [ ] Add mental tests for Queue before code.
- [ ] Decide whether queued work needs its own small data type or can remain internal implementation data.
- [ ] Verify recursive `Fire` behavior with `Ignore`, `Allow`, future `Reset`, and future `Queue`.
- [ ] Verify that all selected executions become `Pending` before any corresponding Action starts.
- [ ] Add explicit cancellation smoke tests.
- [ ] Add explicit failed/cancelled status assertions before completed executions are removed, if runtime observation requires them.

## Context / runtime state

- [ ] Keep `EventContext` and Rule runtime state as separate parts of the ECA context.
- [ ] Add new context fields only when a concrete requirement appears.
- [ ] Do not introduce `EcaEventState`.
- [ ] Do not introduce parallel runtime-specific Checker/Runner interfaces.
- [ ] Revisit whether Conditions and Actions should receive any additional RuleRuntime information beyond the current counters.
- [ ] Decide whether runtime state should eventually expose:
  - active execution count
  - pending queue count
  - last execution status
  - last failure
  - other values only when a real use case requires them

## Validation / IDs

- [ ] Keep current runtime validation for now.
- [ ] Revisit whether `Name` should remain required.
- [ ] Later consider replacing `using EcaRuleId = System.String` with a strongly typed ID only if accidental ID mixing becomes a real problem.
- [ ] Keep EventId collision/type validation covered by tests.
- [ ] Keep duplicate RuleId validation covered by tests.

## Testing

- [ ] Preserve the current `EcaSystemsSmokeTest` as a fast Unity integration check.
- [ ] Keep the expected baseline at `FAIL 0`.
- [ ] Add tests whenever a new overlap strategy or lifecycle rule is introduced.
- [ ] Add tests for:
  - unregister with active execution
  - explicit cancellation
  - recursive Fire
  - multiple Rules with different runtime policies
  - execution failure observation
  - future Queue
  - future Reset
- [ ] Later move stable behavior tests from the smoke-test MonoBehaviour into formal Unity Test Framework tests.

## Deferred — do not implement yet

- [ ] Commands layer.
- [ ] Modules layer.
- [ ] Visual programming layer.
- [ ] EventBlock.
- [ ] Templates.
- [ ] Gates as a dedicated framework primitive.
- [ ] Signals / WaitSignal.
- [ ] FSM / Event Pages.
- [ ] EventGroup.
- [ ] JSON/data-authored Rules.
- [ ] Persistent RuleRuntime save/load.
- [ ] Saving and resuming in the middle of an active Action.
- [ ] Execution middleware/policies beyond concrete requirements.
- [ ] Thread-safe/background-thread `Fire`; keep Unity integration main-thread oriented until a real integration requires otherwise.

## Documentation / cleanup

- [ ] Keep `Documentation~/Architecture/mental-tests.md` synchronized with architecture changes.
- [ ] Update mental tests when Reset/Queue/unregister semantics are decided.
- [ ] Add a short architecture overview once the RuleRuntime API stabilizes.
- [ ] Remove obsolete experimental files/classes from earlier ECA implementations if any remain in the repository.
