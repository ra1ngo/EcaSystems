# EcaSystems - Roadmap

These capabilities do not block first practical use. See [ToDo.md](ToDo.md) for required next stages and [Context.md](Context.md) for current decisions. Execution v1 is complete; the items below are unimplemented possibilities requiring separate design and practical use cases.

## Future advanced execution

An ordinary C# Task cannot universally be forcibly stopped and then resumed at an arbitrary point. Cooperative requests do not guarantee that arbitrary Action.Run code stops or performs domain cleanup.

Interrupt, resume, pause, reset and save/load execution will likely require an explicit executable model:

```text
Action / Program
  Step / Command 1
  Step / Command 2
  ...

Execution state
  current step / program counter
  history
  serializable state where applicable
```

- [ ] Investigate this model against a real use case; it may also provide the foundation for save/load.
- [ ] Define interruption and cleanup contracts separately if needed.
- [ ] Consider Queue and Reset only as possible future capabilities, not mandatory standard modes.
- [ ] Evaluate Priority, Retry, Timeout, MaxConcurrency, Dependencies, Sequences and Parallel when justified.
- [ ] Decide ownership of each capability separately; do not expand minimal Execution v1 in advance.

## Future async / graceful Unregister

V1 removes Rule and Group immediately and lets running Actions finish naturally. Possible future semantics:

```text
UnregisterAsync(old rule)
  -> Rule stops receiving new Fire
  -> old Group becomes retiring/closing
  -> existing executions finish naturally
  -> await completes after the entire old Group finishes
```

The same RuleId must be re-registerable before old UnregisterAsync completes. The new active Group is independent of the old retiring Group; the old Group must not reserve its RuleId in the active registry.

- [ ] Consider separate active vs retiring group storage.
- [ ] Consider generation/instance identity.
- [ ] Remove retiring groups by identity, not a simple Remove(ruleId), to avoid removing a replacement.
- [ ] Design and test separately; do not implement Closing or UnregisterAsync now.

## Execution inspection / history

Group already exposes public IReadOnlyList Executions. GroupState currently contains only two live counters.

- [ ] Consider LastExecution, PreviousExecution, History, ActiveCount, PendingCount and LastFailure.
- [ ] Decide between a separate ExecutionHistory and observation events.
- [ ] Keep mutable internal collections protected.

## Debugging / observation

- [ ] Observe started / completed / failed and exception information.
- [ ] Diagnostic API and Debug UI for groups and active executions.
- [ ] Visualize Event -> Rule -> Execution chains.

## Cyclic / reentrant Fire diagnostics

- [ ] Diagnose Event A -> Action -> Event A and longer cycles.
- [ ] Consider event-chain tracking and configurable depth limits.
- [ ] Diagnose before restricting legitimate complex event chains.

## Custom overlap extensibility

Current modes are only Ignore / Allow with a simple switch.

- [ ] Revisit custom overlap when a practical need exists.
- [ ] Then evaluate Policy / Strategy / Plan without mutable access to Group internals.
- [ ] Do not treat extensibility or additional standard modes as already agreed architecture.

## Visual programming / Editor

- [ ] Visual Rule Editor and node/graph representation.
- [ ] UI for Events, Conditions, Actions, future Commands, Systems and variables.
- [ ] Debug visualization and stable IDs for editing/serialization.

## Serialization / data authoring

- [ ] JSON/data-authored Rules and ScriptableObject authoring as needed.
- [ ] Stable formats, versioning and migrations without coupling Core to a data format.

## Persistence / save-load

- [ ] Save/load Global State, timer state and scopes/rules when practically needed.
- [ ] Investigate mid-execution save/resume through the explicit step/state model above; do not promise arbitrary async Action resumption.

## Advanced testing / performance

- [ ] Stress tests for many scopes/executions.
- [ ] Property tests for future policies and tests for cyclic diagnostics.
- [ ] Profiling, benchmarks and diagnostics for forgotten registrations/lifecycle leaks.
