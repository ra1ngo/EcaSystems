using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;
using UnityEngine;

namespace EcaSystems.Unity
{
    public sealed class EcaSystemsSmokeTest : MonoBehaviour
    {
        private int _passed;
        private int _failed;

        private async void Start()
        {
            Debug.Log("=== EcaSystems Smoke Tests ===");

            TestBaseEmptyContext();
            TestBaseEventContext();
            TestBaseConditionFalse();
            TestBaseChecksAllConditionsBeforeActions();
            TestDuplicateRuleId();

            await TestExecutionGroupPending();
            await TestExecutionIgnore();
            await TestExecutionAllow();
            await TestExecutionFailedAction();
            await TestExecutionConditionOrder();
            await TestCancellation();
            await TestFailureStatus();
            TestRegistryValidation();

            Debug.Log(
                $"=== EcaSystems Smoke Tests Finished: PASS {_passed}, FAIL {_failed} ==="
            );
        }

        // =====================================================================
        // BASE
        // =====================================================================

        private void TestBaseEmptyContext()
        {
            Debug.Log("--- Base: EcaEventContextEmpty ---");

            var engine = new EcaEngine();

            var testEvent = new EcaEvent<EcaEventContextEmpty>(
                "test.base.empty",
                "Base Empty Event"
            );

            var action = new BaseCountingAction<EcaEventContextEmpty>();

            var rule = new EcaRule<EcaContext<EcaEventContextEmpty>>(
                new EcaRuleConfig<EcaContext<EcaEventContextEmpty>>
                {
                    Id = "test.base.empty.rule",
                    Name = "Base Empty Rule",
                    Event = testEvent,
                    Action = action
                }
            );

            engine.Register(rule);

            engine.Fire(testEvent);
            engine.Fire(testEvent);

            Expect(
                "Base empty event runs Action on every Fire",
                action.RunCount == 2
            );

            Expect(
                "Base unregister succeeds",
                engine.Unregister(rule)
            );

            engine.Fire(testEvent);

            Expect(
                "Unregistered Rule no longer runs",
                action.RunCount == 2
            );
        }

        private void TestBaseEventContext()
        {
            Debug.Log("--- Base: EventContext ---");

            var engine = new EcaEngine();

            var testEvent = new EcaEvent<TestEventContext>(
                "test.base.context",
                "Base Context Event"
            );

            var action = new BaseCountingAction<TestEventContext>();

            var rule = new EcaRule<EcaContext<TestEventContext>>(
                new EcaRuleConfig<EcaContext<TestEventContext>>
                {
                    Id = "test.base.context.rule",
                    Name = "Base Context Rule",
                    Event = testEvent,
                    Action = action
                }
            );

            engine.Register(rule);

            engine.Fire(
                testEvent,
                new TestEventContext(42)
            );

            Expect(
                "Base Action receives EventContext",
                action.LastValue == 42
            );
        }

        private void TestBaseConditionFalse()
        {
            Debug.Log("--- Base: Condition false ---");

            var engine = new EcaEngine();

            var testEvent = new EcaEvent<TestEventContext>(
                "test.base.condition-false",
                "Condition False Event"
            );

            var action = new BaseCountingAction<TestEventContext>();

            var condition =
                new DelegateEcaCondition<EcaContext<TestEventContext>>(
                    _ => false
                );

            var rule = new EcaRule<EcaContext<TestEventContext>>(
                new EcaRuleConfig<EcaContext<TestEventContext>>
                {
                    Id = "test.base.condition-false.rule",
                    Name = "Condition False Rule",
                    Event = testEvent,
                    Condition = condition,
                    Action = action
                }
            );

            engine.Register(rule);

            engine.Fire(
                testEvent,
                new TestEventContext(1)
            );

            Expect(
                "Condition false prevents Action",
                action.RunCount == 0
            );
        }

        private void TestBaseChecksAllConditionsBeforeActions()
        {
            Debug.Log("--- Base: all Conditions before Actions ---");

            var engine = new EcaEngine();

            var testEvent = new EcaEvent<TestEventContext>(
                "test.base.condition-order",
                "Condition Order Event"
            );

            var sharedState = new SharedState();

            var firstRule =
                new EcaRule<EcaContext<TestEventContext>>(
                    new EcaRuleConfig<EcaContext<TestEventContext>>
                    {
                        Id = "test.base.condition-order.first",
                        Name = "First Rule",
                        Event = testEvent,
                        Action = new SetFlagAction(sharedState)
                    }
                );

            var secondAction =
                new BaseCountingAction<TestEventContext>();

            var secondRule =
                new EcaRule<EcaContext<TestEventContext>>(
                    new EcaRuleConfig<EcaContext<TestEventContext>>
                    {
                        Id = "test.base.condition-order.second",
                        Name = "Second Rule",
                        Event = testEvent,
                        Condition = new FlagMustBeFalseCondition(sharedState),
                        Action = secondAction
                    }
                );

            engine.Register(firstRule);
            engine.Register(secondRule);

            engine.Fire(
                testEvent,
                new TestEventContext(1)
            );

            Expect(
                "First Action changed shared state",
                sharedState.Value
            );

            Expect(
                "Second Condition was checked before First Action",
                secondAction.RunCount == 1
            );
        }

        private void TestDuplicateRuleId()
        {
            Debug.Log("--- Base: duplicate RuleId ---");

            var engine = new EcaEngine();

            var testEvent = new EcaEvent<TestEventContext>(
                "test.base.duplicate",
                "Duplicate Rule Event"
            );

            var rule1 =
                new EcaRule<EcaContext<TestEventContext>>(
                    new EcaRuleConfig<EcaContext<TestEventContext>>
                    {
                        Id = "same.rule.id",
                        Name = "Rule One",
                        Event = testEvent,
                        Action = new BaseCountingAction<TestEventContext>()
                    }
                );

            var rule2 =
                new EcaRule<EcaContext<TestEventContext>>(
                    new EcaRuleConfig<EcaContext<TestEventContext>>
                    {
                        Id = "same.rule.id",
                        Name = "Rule Two",
                        Event = testEvent,
                        Action = new BaseCountingAction<TestEventContext>()
                    }
                );

            engine.Register(rule1);

            var exceptionThrown = false;

            try
            {
                engine.Register(rule2);
            }
            catch (InvalidOperationException)
            {
                exceptionThrown = true;
            }

            Expect(
                "Duplicate RuleId is rejected",
                exceptionThrown
            );
        }

        // =====================================================================
        // EXECUTION
        // =====================================================================

        private async Task TestExecutionGroupPending()
        {
            Debug.Log("--- Execution: Pending ---");

            var executor = new DeferredExecutor();
            var testEvent = new EcaEvent<TestEventContext>("test.pending", "Pending");
            var action = new ExecutionGateAction<TestEventContext>();
            var rule = CreateExecutionRule("test.execution.pending.rule", testEvent, action);
            var group = new EcaRuleExecutionGroup<TestEventContext>(rule, EcaOverlap.Ignore, executor);
            var context = new EcaExecutionContextFactory().Create(new TestEventContext(1), group.State);
            group.Fire(context);
            var execution = group.Executions[0];

            Expect("Group.Fire creates Execution", execution != null);
            Expect("New Execution starts as Pending", execution.Status == EcaRuleExecutionStatus.Pending);
            Expect("Pending Execution already belongs to ExecutionGroup", group.Executions.Count == 1);
            group.Fire(context);
            Expect("Ignore rejects another Execution while Pending exists", group.Executions.Count == 1);
            executor.Release();
            action.CompleteAll();
            await WaitUntil(() => group.Executions.Count == 0);
            Expect(
                "Execution is removed after completion",
                group.Executions.Count == 0
            );

            Expect(
                "Execution counters become 1 / 1",
                group.State.EcaRuleExecutionTotalStarted == 1 &&
                group.State.EcaRuleExecutionTotalFinished == 1
            );
        }

        private async Task TestExecutionIgnore()
        {
            Debug.Log("--- Execution: Ignore ---");

            var ruleRegistry = new EcaRuleRegistry();
            var ruleChecker = new EcaRuleChecker();
            var executor = new EcaExecutionExecutor();
            var executionRegistry = new EcaRuleExecutionRegistry();

            var engine = new EcaExecutionEngine(
                ruleRegistry,
                new EcaRuleSelector(ruleRegistry),
                ruleChecker,
                executionRegistry,
                new EcaExecutionContextFactory(),
                executor
            );

            var testEvent = new EcaEvent<TestEventContext>(
                "test.execution.ignore",
                "Execution Ignore Event"
            );

            var condition =
                new ExecutionRecordingCondition<TestEventContext>();

            var action =
                new ExecutionGateAction<TestEventContext>();

            var rule =
                new EcaRule<EcaExecutionContext<TestEventContext>>(
                    new EcaRuleConfig<
                        EcaExecutionContext<TestEventContext>
                    >
                    {
                        Id = "test.execution.ignore.rule",
                        Name = "Execution Ignore Rule",
                        Event = testEvent,
                        Condition = condition,
                        Action = action
                    }
                );

            engine.Register<TestEventContext>(
                rule,
                EcaOverlap.Ignore
            );

            var group = executionRegistry.Get<TestEventContext>(rule.Id);

            // Fire #1
            engine.Fire(
                testEvent,
                new TestEventContext(1)
            );

            Expect(
                "Ignore: first Condition sees 0 / 0",
                condition.Records.Count == 1 &&
                condition.Records[0].Started == 0 &&
                condition.Records[0].Finished == 0
            );

            Expect(
                "Ignore: first Action starts",
                action.RunCount == 1
            );

            Expect(
                "Ignore: Action sees 1 / 0",
                action.Records[0].Started == 1 &&
                action.Records[0].Finished == 0
            );

            Expect(
                "Ignore: one Running Execution exists",
                group.Executions.Count == 1 &&
                group.Executions[0].Status ==
                EcaRuleExecutionStatus.Running
            );

            Expect(
                "Ignore: live ExecutionGroupState is 1 / 0",
                group.State.EcaRuleExecutionTotalStarted == 1 &&
                group.State.EcaRuleExecutionTotalFinished == 0
            );

            // Fire #2 while #1 is still running.
            engine.Fire(
                testEvent,
                new TestEventContext(2)
            );

            Expect(
                "Ignore: Condition still runs on second Fire",
                condition.Records.Count == 2
            );

            Expect(
                "Ignore: second Condition sees 1 / 0",
                condition.Records[1].Started == 1 &&
                condition.Records[1].Finished == 0
            );

            Expect(
                "Ignore: second Action does not start",
                action.RunCount == 1
            );

            Expect(
                "Ignore: still one Execution",
                group.Executions.Count == 1
            );

            // Complete execution #1.
            action.CompleteAll();

            await WaitUntil(
                () =>
                    group.State
                        .EcaRuleExecutionTotalFinished == 1
            );

            Expect(
                "Ignore: after completion state is 1 / 1",
                group.State.EcaRuleExecutionTotalStarted == 1 &&
                group.State.EcaRuleExecutionTotalFinished == 1
            );

            Expect(
                "Ignore: completed Execution removed",
                group.Executions.Count == 0
            );

            // Fire #3 after previous execution finished.
            engine.Fire(
                testEvent,
                new TestEventContext(3)
            );

            Expect(
                "Ignore: third Condition sees previous 1 / 1",
                condition.Records.Count == 3 &&
                condition.Records[2].Started == 1 &&
                condition.Records[2].Finished == 1
            );

            Expect(
                "Ignore: Rule starts again",
                action.RunCount == 2
            );

            Expect(
                "Ignore: second Action sees 2 / 1",
                action.Records[1].Started == 2 &&
                action.Records[1].Finished == 1
            );

            action.CompleteAll();

            await WaitUntil(
                () =>
                    group.State
                        .EcaRuleExecutionTotalFinished == 2
            );

            Expect(
                "Ignore: final state is 2 / 2",
                group.State.EcaRuleExecutionTotalStarted == 2 &&
                group.State.EcaRuleExecutionTotalFinished == 2
            );
        }

        private async Task TestExecutionAllow()
        {
            Debug.Log("--- Execution: Allow ---");

            var ruleRegistry = new EcaRuleRegistry();
            var ruleChecker = new EcaRuleChecker();
            var executor = new EcaExecutionExecutor();
            var executionRegistry = new EcaRuleExecutionRegistry();

            var engine = new EcaExecutionEngine(
                ruleRegistry,
                new EcaRuleSelector(ruleRegistry),
                ruleChecker,
                executionRegistry,
                new EcaExecutionContextFactory(),
                executor
            );

            var testEvent = new EcaEvent<TestEventContext>(
                "test.execution.allow",
                "Execution Allow Event"
            );

            var action =
                new ExecutionGateAction<TestEventContext>();

            var rule =
                new EcaRule<EcaExecutionContext<TestEventContext>>(
                    new EcaRuleConfig<
                        EcaExecutionContext<TestEventContext>
                    >
                    {
                        Id = "test.execution.allow.rule",
                        Name = "Execution Allow Rule",
                        Event = testEvent,
                        Action = action
                    }
                );

            engine.Register<TestEventContext>(
                rule,
                EcaOverlap.Allow
            );

            var group = executionRegistry.Get<TestEventContext>(rule.Id);

            engine.Fire(
                testEvent,
                new TestEventContext(1)
            );

            engine.Fire(
                testEvent,
                new TestEventContext(2)
            );

            Expect(
                "Allow: both Actions start",
                action.RunCount == 2
            );

            Expect(
                "Allow: two Executions exist simultaneously",
                group.Executions.Count == 2
            );

            Expect(
                "Allow: ExecutionGroupState is 2 / 0",
                group.State.EcaRuleExecutionTotalStarted == 2 &&
                group.State.EcaRuleExecutionTotalFinished == 0
            );

            Expect(
                "Allow: first Action initially saw 1 / 0",
                action.Records[0].Started == 1 &&
                action.Records[0].Finished == 0
            );

            Expect(
                "Allow: second Action initially saw 2 / 0",
                action.Records[1].Started == 2 &&
                action.Records[1].Finished == 0
            );

            // The first Action stores a reference to the same live state.
            Expect(
                "Allow: ExecutionGroupState passed to Action is live",
                action.ExecutionGroupStates[0]
                    .EcaRuleExecutionTotalStarted == 2
            );

            action.CompleteAll();

            await WaitUntil(
                () =>
                    group.State
                        .EcaRuleExecutionTotalFinished == 2
            );

            Expect(
                "Allow: all Executions removed after completion",
                group.Executions.Count == 0
            );

            Expect(
                "Allow: final state is 2 / 2",
                group.State.EcaRuleExecutionTotalStarted == 2 &&
                group.State.EcaRuleExecutionTotalFinished == 2
            );
        }

        private async Task TestExecutionFailedAction()
        {
            Debug.Log("--- Execution: Failed Action ---");

            var ruleRegistry = new EcaRuleRegistry();
            var ruleChecker = new EcaRuleChecker();
            var executor = new EcaExecutionExecutor();
            var executionRegistry = new EcaRuleExecutionRegistry();

            var engine = new EcaExecutionEngine(
                ruleRegistry,
                new EcaRuleSelector(ruleRegistry),
                ruleChecker,
                executionRegistry,
                new EcaExecutionContextFactory(),
                executor
            );

            var testEvent = new EcaEvent<TestEventContext>(
                "test.execution.failed",
                "Execution Failed Event"
            );

            var rule =
                new EcaRule<EcaExecutionContext<TestEventContext>>(
                    new EcaRuleConfig<
                        EcaExecutionContext<TestEventContext>
                    >
                    {
                        Id = "test.execution.failed.rule",
                        Name = "Execution Failed Rule",
                        Event = testEvent,
                        Action =
                            new ExecutionThrowingAction<
                                TestEventContext
                            >()
                    }
                );

            engine.Register<TestEventContext>(
                rule,
                EcaOverlap.Ignore
            );

            var group = executionRegistry.Get<TestEventContext>(rule.Id);

            engine.Fire(
                testEvent,
                new TestEventContext(1)
            );

            await WaitUntil(
                () =>
                    group.State
                        .EcaRuleExecutionTotalFinished == 1
            );

            Expect(
                "Failed Action still increments Started",
                group.State.EcaRuleExecutionTotalStarted == 1
            );

            Expect(
                "Failed Action increments Finished",
                group.State.EcaRuleExecutionTotalFinished == 1
            );

            Expect(
                "Failed Execution is removed from ExecutionGroup",
                group.Executions.Count == 0
            );
        }

        // =====================================================================
        // TEST HELPERS
        // =====================================================================


        private static EcaRule<EcaExecutionContext<TestEventContext>> CreateExecutionRule(
            string id, EcaEvent<TestEventContext> ecaEvent,
            IEcaAction<EcaExecutionContext<TestEventContext>> action,
            IEcaCondition<EcaExecutionContext<TestEventContext>> condition = null)
        {
            return new EcaRule<EcaExecutionContext<TestEventContext>>(
                new EcaRuleConfig<EcaExecutionContext<TestEventContext>>
                {
                    Id = id, Name = id, Event = ecaEvent, Action = action, Condition = condition
                });
        }

        private async Task TestExecutionConditionOrder()
        {
            var registry = new EcaRuleRegistry();
            var groups = new EcaRuleExecutionRegistry();
            var engine = new EcaExecutionEngine(registry, new EcaRuleSelector(registry),
                new EcaRuleChecker(), groups, new EcaExecutionContextFactory(), new EcaExecutionExecutor());
            var ecaEvent = new EcaEvent<TestEventContext>("test.execution.order", "Order");
            var first = new ExecutionGateAction<TestEventContext>();
            var second = new ExecutionGateAction<TestEventContext>();
            var rejected = new ExecutionGateAction<TestEventContext>();
            var firstRule = CreateExecutionRule("order.first", ecaEvent, first);
            var secondRule = CreateExecutionRule("order.second", ecaEvent, second,
                new DelegateEcaCondition<EcaExecutionContext<TestEventContext>>(_ => first.RunCount == 0));
            var rejectedRule = CreateExecutionRule("order.false", ecaEvent, rejected,
                new DelegateEcaCondition<EcaExecutionContext<TestEventContext>>(_ => false));
            engine.Register(firstRule, EcaOverlap.Allow);
            engine.Register(secondRule, EcaOverlap.Allow);
            engine.Register(rejectedRule, EcaOverlap.Allow);
            engine.Fire(ecaEvent, new TestEventContext(42));
            var firstGroup = groups.Get<TestEventContext>(firstRule.Id);
            var secondGroup = groups.Get<TestEventContext>(secondRule.Id);
            Expect("Execution: all Conditions precede Actions", first.RunCount == 1 && second.RunCount == 1);
            Expect("Execution: false Condition creates no execution",
                rejected.RunCount == 0 && groups.Get<TestEventContext>(rejectedRule.Id).Executions.Count == 0);
            Expect("Execution: groups have separate live state",
                !ReferenceEquals(firstGroup.State, secondGroup.State));
            Expect("Execution retains the exact Rule and Context",
                ReferenceEquals(firstGroup.Executions[0].Rule, firstRule) &&
                ReferenceEquals(firstGroup.Executions[0].Context.RuleExecutionGroupState, firstGroup.State) &&
                firstGroup.Executions[0].Context.EventContext.Value == 42);
            first.CompleteAll();
            second.CompleteAll();
            await WaitUntil(() => firstGroup.Executions.Count == 0 && secondGroup.Executions.Count == 0);
        }

        private async Task TestCancellation()
        {
            var action = new CancellableGateAction();
            var ecaEvent = new EcaEvent<TestEventContext>("test.cancel", "Cancel");
            var rule = CreateExecutionRule("cancel.rule", ecaEvent, action);
            var group = new EcaRuleExecutionGroup<TestEventContext>(rule, EcaOverlap.Allow, new EcaExecutionExecutor());
            var factory = new EcaExecutionContextFactory();
            group.Fire(factory.Create(new TestEventContext(1), group.State));
            group.Fire(factory.Create(new TestEventContext(2), group.State));
            var first = group.Executions[0];
            var second = group.Executions[1];
            var cancellation = group.Cancel(first);
            Expect("Cancel invokes hook with the exact execution Context",
                action.CancelCount == 1 && ReferenceEquals(action.CancelledContext, first.Context));
            Expect("Async cleanup keeps Cancelling and cancellation Task incomplete",
                first.Status == EcaRuleExecutionStatus.Cancelling && !cancellation.IsCompleted);
            Expect("Execution token records cancellation", first.CancellationToken.IsCancellationRequested);
            Expect("Cancelling one Allow execution leaves the other Running",
                second.Status == EcaRuleExecutionStatus.Running && !second.CancellationToken.IsCancellationRequested);
            var repeated = group.Cancel(first);
            Expect("Repeated Cancel shares cleanup without invoking hook again",
                ReferenceEquals(cancellation, repeated) && action.CancelCount == 1);
            // Run ends before cleanup: neither final status nor group removal may happen yet.
            action.Complete(first.Context);
            Expect("Run completion waits for cancellation cleanup",
                first.Status == EcaRuleExecutionStatus.Cancelling && group.Executions.Count == 2);
            action.Cleanup.TrySetResult(true);
            await cancellation;
            await WaitUntil(() => group.Executions.Count == 1);
            Expect("Cleanup completion produces final Cancelled",
                first.Status == EcaRuleExecutionStatus.Cancelled &&
                group.State.EcaRuleExecutionTotalFinished == 1);
            var cancelAll = group.CancelAll();
            action.Complete(second.Context);
            await cancelAll;
            await WaitUntil(() => group.Executions.Count == 0);
            Expect("CancelAll cancels remaining execution and balances counters",
                second.Status == EcaRuleExecutionStatus.Cancelled &&
                group.State.EcaRuleExecutionTotalStarted == 2 &&
                group.State.EcaRuleExecutionTotalFinished == 2);

            var plainAction = new ExecutionGateAction<TestEventContext>();
            var plainRule = CreateExecutionRule("cancel.plain", ecaEvent, plainAction);
            var plainGroup = new EcaRuleExecutionGroup<TestEventContext>(plainRule, EcaOverlap.Ignore, new EcaExecutionExecutor());
            plainGroup.Fire(factory.Create(new TestEventContext(3), plainGroup.State));
            var plainExecution = plainGroup.Executions[0];
            await plainGroup.Cancel(plainExecution);
            Expect("Cancellation without a hook is supported",
                plainExecution.Status == EcaRuleExecutionStatus.Cancelled);
            plainAction.CompleteAll();
            await WaitUntil(() => plainGroup.Executions.Count == 0);
            Expect("Late Run completion does not overwrite Cancelled",
                plainExecution.Status == EcaRuleExecutionStatus.Cancelled);
        }

        private async Task TestFailureStatus()
        {
            var ecaEvent = new EcaEvent<TestEventContext>("test.failure.status", "Failure");
            var action = new CancellableGateAction();
            var rule = CreateExecutionRule("failure.status", ecaEvent, action);
            var group = new EcaRuleExecutionGroup<TestEventContext>(rule, EcaOverlap.Ignore, new EcaExecutionExecutor());
            group.Fire(new EcaExecutionContextFactory().Create(new TestEventContext(1), group.State));
            var execution = group.Executions[0];
            var error = new InvalidOperationException("Expected asynchronous failure.");
            action.Fail(execution.Context, error);
            await WaitUntil(() => group.Executions.Count == 0);
            Expect("Faulted Run records Failed and the original exception",
                execution.Status == EcaRuleExecutionStatus.Failed && ReferenceEquals(execution.Exception, error));
            Expect("Ordinary failure does not call cancellation hook", action.CancelCount == 0);
        }

        private void TestRegistryValidation()
        {
            var registry = new EcaRuleRegistry();
            var selector = new EcaRuleSelector(registry);
            var groups = new EcaRuleExecutionRegistry();
            var executor = new EcaExecutionExecutor();
            var ecaEvent = new EcaEvent<TestEventContext>("validation", "Validation");
            var rule = CreateExecutionRule("validation.rule", ecaEvent, new ExecutionGateAction<TestEventContext>());
            groups.Register(rule, EcaOverlap.Allow, executor);
            ExpectThrows("Typed Get rejects incompatible group", () => groups.Get<EcaEventContextEmpty>(rule.Id));
            ExpectThrows("Typed TryGet rejects incompatible group",
                () => groups.TryGet<EcaEventContextEmpty>(rule.Id, out _));
            Expect("TryGet returns false for missing group", !groups.TryGet<TestEventContext>("missing", out _));
            var engine = new EcaExecutionEngine(registry, selector, new EcaRuleChecker(),
                groups, new EcaExecutionContextFactory(), executor);
            ExpectThrows("Registration rejects overlap mismatch", () => engine.Register(rule, EcaOverlap.Ignore));
            Expect("Failed group registration rolls back Rule registration", registry.Rules.Count == 0);
            engine.Register(rule, EcaOverlap.Allow);
            Expect("Registration can succeed after rollback", registry.Rules.Count == 1);
            ExpectThrows("Selector rejects incompatible full Context",
                () => selector.ForEvent<EcaContext<TestEventContext>>(ecaEvent));
            var conflictingEvent = new EcaEvent<EcaEventContextEmpty>("validation", "Conflicting event");
            ExpectThrows("Selector rejects EventId collision with incompatible payload",
                () => selector.ForEvent<EcaContext<EcaEventContextEmpty>>(conflictingEvent));
            Expect("Registry exposes a read-only collection",
                ((ICollection<IEcaRule>)registry.Rules).IsReadOnly);
        }

        private void ExpectThrows(string name, Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { Expect(name, true); return; }
            Expect(name, false);
        }

        private sealed class DeferredExecutor : IEcaExecutionExecutor
        {
            private readonly TaskCompletionSource<bool> _release = new();
            private readonly EcaExecutionExecutor _executor = new();

            public async Task Execute<TEventContext>(EcaRuleExecution<TEventContext> execution)
            {
                await _release.Task;
                await _executor.Execute(execution);
            }

            public Task Cancel<TEventContext>(EcaRuleExecution<TEventContext> execution) => _executor.Cancel(execution);
            public void Release() => _release.TrySetResult(true);
        }

        private sealed class CancellableGateAction :
            IEcaAction<EcaExecutionContext<TestEventContext>>,
            IEcaCancellableAction<EcaExecutionContext<TestEventContext>>
        {
            private readonly Dictionary<EcaExecutionContext<TestEventContext>, TaskCompletionSource<bool>> _runs = new();
            public TaskCompletionSource<bool> Cleanup { get; } = new();
            public int CancelCount { get; private set; }
            public EcaExecutionContext<TestEventContext> CancelledContext { get; private set; }

            public Task Run(EcaExecutionContext<TestEventContext> context)
            {
                var gate = new TaskCompletionSource<bool>();
                _runs.Add(context, gate);
                return gate.Task;
            }

            public Task Cancel(EcaExecutionContext<TestEventContext> context)
            {
                CancelCount++;
                CancelledContext = context;
                return Cleanup.Task;
            }

            public void Complete(EcaExecutionContext<TestEventContext> context) => _runs[context].TrySetResult(true);
            public void Fail(EcaExecutionContext<TestEventContext> context, Exception exception) => _runs[context].TrySetException(exception);
        }
        private async Task WaitUntil(
            Func<bool> condition,
            int maxFrames = 10)
        {
            for (var i = 0; i < maxFrames; i++)
            {
                if (condition())
                    return;

                await Awaitable.NextFrameAsync();
            }

            Debug.LogError(
                "[FAIL] WaitUntil timed out."
            );

            _failed++;
        }

        private void Expect(
            string testName,
            bool condition)
        {
            if (condition)
            {
                _passed++;

                Debug.Log(
                    $"[PASS] {testName}"
                );

                return;
            }

            _failed++;

            Debug.LogError(
                $"[FAIL] {testName}"
            );
        }

        // =====================================================================
        // TEST DATA
        // =====================================================================

        private readonly struct TestEventContext
        {
            public int Value { get; }

            public TestEventContext(int value)
            {
                Value = value;
            }
        }

        private sealed class SharedState
        {
            public bool Value;
        }

        private readonly struct ExecutionRecord
        {
            public long Started { get; }
            public long Finished { get; }

            public ExecutionRecord(
                long started,
                long finished)
            {
                Started = started;
                Finished = finished;
            }
        }

        // =====================================================================
        // BASE CONDITIONS
        // =====================================================================

        private sealed class FlagMustBeFalseCondition
            : IEcaCondition<EcaContext<TestEventContext>>
        {
            private readonly SharedState _state;

            public FlagMustBeFalseCondition(
                SharedState state)
            {
                _state = state;
            }

            public bool Check(
                EcaContext<TestEventContext> context)
            {
                return !_state.Value;
            }
        }

        // =====================================================================
        // BASE ACTIONS
        // =====================================================================

        private sealed class BaseCountingAction<TEventContext>
            : IEcaAction<EcaContext<TEventContext>>
        {
            public int RunCount { get; private set; }
            public int LastValue { get; private set; }

            public Task Run(
                EcaContext<TEventContext> context)
            {
                RunCount++;

                if (context.EventContext
                    is TestEventContext testContext)
                {
                    LastValue = testContext.Value;
                }

                return Task.CompletedTask;
            }
        }

        private sealed class SetFlagAction
            : IEcaAction<EcaContext<TestEventContext>>
        {
            private readonly SharedState _state;

            public SetFlagAction(
                SharedState state)
            {
                _state = state;
            }

            public Task Run(
                EcaContext<TestEventContext> context)
            {
                _state.Value = true;

                return Task.CompletedTask;
            }
        }

        // =====================================================================
        // EXECUTION CONDITIONS
        // =====================================================================

        private sealed class ExecutionRecordingCondition<TEventContext>
            : IEcaCondition<EcaExecutionContext<TEventContext>>
        {
            public List<ExecutionRecord> Records { get; } = new();

            public bool Check(
                EcaExecutionContext<TEventContext> context)
            {
                Records.Add(
                    new ExecutionRecord(
                        context.RuleExecutionGroupState
                            .EcaRuleExecutionTotalStarted,
                        context.RuleExecutionGroupState
                            .EcaRuleExecutionTotalFinished
                    )
                );

                return true;
            }
        }

        // =====================================================================
        // EXECUTION ACTIONS
        // =====================================================================

        private sealed class ExecutionGateAction<TEventContext>
            : IEcaAction<EcaExecutionContext<TEventContext>>
        {
            private readonly List<TaskCompletionSource<bool>> _gates = new();

            public int RunCount { get; private set; }

            public List<ExecutionRecord> Records { get; } = new();

            public List<EcaRuleExecutionGroupState> ExecutionGroupStates { get; } = new();

            public Task Run(
                EcaExecutionContext<TEventContext> context)
            {
                RunCount++;

                ExecutionGroupStates.Add(
                    context.RuleExecutionGroupState
                );

                Records.Add(
                    new ExecutionRecord(
                        context.RuleExecutionGroupState
                            .EcaRuleExecutionTotalStarted,
                        context.RuleExecutionGroupState
                            .EcaRuleExecutionTotalFinished
                    )
                );

                var gate =
                    new TaskCompletionSource<bool>();

                _gates.Add(gate);

                return gate.Task;
            }

            public void CompleteAll()
            {
                for (var i = 0; i < _gates.Count; i++)
                    _gates[i].TrySetResult(true);

                _gates.Clear();
            }
        }

        private sealed class ExecutionThrowingAction<TEventContext>
            : IEcaAction<EcaExecutionContext<TEventContext>>
        {
            public Task Run(
                EcaExecutionContext<TEventContext> context)
            {
                throw new InvalidOperationException(
                    "Intentional smoke-test exception."
                );
            }
        }
    }
}
