# EcaSystems - ToDo

Only work required before the first full practical use in games belongs here. Execution v1 is complete. Current decisions live in [Context.md](Context.md); non-blocking future capabilities live in [Roadmap.md](Roadmap.md).

## 1. Scope layer - next stage

- [ ] Define ownership/lifetime for global, scene and local scopes.
- [ ] Support independent ExecutionGroups/state for one Rule definition in different local/entity scopes without Entity knowledge in Core.
- [ ] Define event routing between scopes.
- [ ] Decide after routing/ownership discussions whether EcaScopeEngine is needed or composition around EcaExecutionEngine is sufficient.
- [ ] Validate group and Limit lifetimes for each scope.

## 2. Systems architecture

- [ ] Design System integration after Scope.
- [ ] Define how Systems provide Events, Conditions, future Commands and context-state extensions.
- [ ] Use the term System, not Module.

## 3. Concrete Systems

- [ ] Build TimeSystem as the first practical System.
- [ ] Design Global State / variables as a System rather than a hardcoded Base feature.

## 4. Commands

- [ ] Design after stabilizing a practical use case.
- [ ] Do not mix Commands into Base Action in advance.

## 5. Unity integration / practical package usability

- [ ] Add wrappers and tests as Scope and Systems appear.
- [ ] Validate UPM usability in a real game scenario.

Cancellation, Reset, Queue and graceful unregister are not prerequisites for first use.
