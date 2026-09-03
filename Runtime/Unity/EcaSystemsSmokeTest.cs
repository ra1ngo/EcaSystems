using System;
using System.Collections.Generic;
using System.Threading;
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

            await TestRuleRuntimePending();
            await TestRuleRuntimeIgnore();
            await TestRuleRuntimeAllow();
            await TestRuleRuntimeFailedAction();

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
                        Condition =
                            new FlagMustBeFalseCondition(sharedState),
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
                        Action =
                            new BaseCountingAction<TestEventContext>()
                    }
                );

            var rule2 =
                new EcaRule<EcaContext<TestEventContext>>(
                    new EcaRuleConfig<EcaContext<TestEventContext>>
                    {
                        Id = "same.rule.id",
                        Name = "Rule Two",
                        Event = testEvent,
                        Action =
                            new BaseCountingAction<TestEventContext>()
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
        // RULE RUNTIME
        // =====================================================================

        private async Task TestRuleRuntimePending()
        {
            Debug.Log("--- RuleRuntime: Pending ---");

            var runtime = new EcaRuleRuntime(
                "test.runtime.pending.rule",
                EcaOverlap.Ignore
            );

            var created =
                runtime.TryCreateExecution(
                    out var execution
                );

            Expect(
                "TryCreateExecution creates Execution",
                created
            );

            Expect(
                "New Execution starts as Pending",
                execution.Status ==
                EcaRuleExecutionStatus.Pending
            );

            Expect(
                "Pending Execution already belongs to Runtime",
                runtime.Executions.Count == 1
            );

            var secondCreated =
                runtime.TryCreateExecution(out _);

            Expect(
                "Ignore rejects another Execution while Pending exists",
                !secondCreated
            );

            await runtime.Run(
                execution,
                _ => Task.CompletedTask
            );

            Expect(
                "Execution is removed after completion",
                runtime.Executions.Count == 0
            );

            Expect(
                "Runtime counters become 1 / 1",
                runtime.State.EcaRuleExecutionTotalStarted == 1 &&
                runtime.State.EcaRuleExecutionTotalFinished == 1
            );
        }

        private async Task TestRuleRuntimeIgnore()
        {
            Debug.Log("--- RuleRuntime: Ignore ---");

            var ruleRegistry = new EcaRuleRegistry();
            var ruleChecker = new EcaRuleChecker();
            var ruleRunner = new EcaRuleRunner();
            var runtimeRegistry = new EcaRuleRuntimeRegistry();

            var engine = new EcaRuleRuntimeEngine(
                ruleRegistry,
                ruleChecker,
                ruleRunner,
                runtimeRegistry
            );

            var testEvent = new EcaEvent<TestEventContext>(
                "test.runtime.ignore",
                "Runtime Ignore Event"
            );

            var condition =
                new RuntimeRecordingCondition<TestEventContext>();

            var action =
                new RuntimeGateAction<TestEventContext>();

            var rule =
                new EcaRule<EcaRuntimeContext<TestEventContext>>(
                    new EcaRuleConfig<
                        EcaRuntimeContext<TestEventContext>
                    >
                    {
                        Id = "test.runtime.ignore.rule",
                        Name = "Runtime Ignore Rule",
                        Event = testEvent,
                        Condition = condition,
                        Action = action
                    }
                );

            engine.Register<TestEventContext>(
                rule,
                EcaOverlap.Ignore
            );

            var runtime = runtimeRegistry.Get(rule.Id);

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
                runtime.Executions.Count == 1 &&
                runtime.Executions[0].Status ==
                EcaRuleExecutionStatus.Running
            );

            Expect(
                "Ignore: live RuntimeState is 1 / 0",
                runtime.State.EcaRuleExecutionTotalStarted == 1 &&
                runtime.State.EcaRuleExecutionTotalFinished == 0
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
                runtime.Executions.Count == 1
            );

            // Complete execution #1.
            action.CompleteAll();

            await WaitUntil(
                () =>
                    runtime.State
                        .EcaRuleExecutionTotalFinished == 1
            );

            Expect(
                "Ignore: after completion state is 1 / 1",
                runtime.State.EcaRuleExecutionTotalStarted == 1 &&
                runtime.State.EcaRuleExecutionTotalFinished == 1
            );

            Expect(
                "Ignore: completed Execution removed",
                runtime.Executions.Count == 0
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
                    runtime.State
                        .EcaRuleExecutionTotalFinished == 2
            );

            Expect(
                "Ignore: final state is 2 / 2",
                runtime.State.EcaRuleExecutionTotalStarted == 2 &&
                runtime.State.EcaRuleExecutionTotalFinished == 2
            );
        }

        private async Task TestRuleRuntimeAllow()
        {
            Debug.Log("--- RuleRuntime: Allow ---");

            var ruleRegistry = new EcaRuleRegistry();
            var ruleChecker = new EcaRuleChecker();
            var ruleRunner = new EcaRuleRunner();
            var runtimeRegistry = new EcaRuleRuntimeRegistry();

            var engine = new EcaRuleRuntimeEngine(
                ruleRegistry,
                ruleChecker,
                ruleRunner,
                runtimeRegistry
            );

            var testEvent = new EcaEvent<TestEventContext>(
                "test.runtime.allow",
                "Runtime Allow Event"
            );

            var action =
                new RuntimeGateAction<TestEventContext>();

            var rule =
                new EcaRule<EcaRuntimeContext<TestEventContext>>(
                    new EcaRuleConfig<
                        EcaRuntimeContext<TestEventContext>
                    >
                    {
                        Id = "test.runtime.allow.rule",
                        Name = "Runtime Allow Rule",
                        Event = testEvent,
                        Action = action
                    }
                );

            engine.Register<TestEventContext>(
                rule,
                EcaOverlap.Allow
            );

            var runtime = runtimeRegistry.Get(rule.Id);

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
                runtime.Executions.Count == 2
            );

            Expect(
                "Allow: RuntimeState is 2 / 0",
                runtime.State.EcaRuleExecutionTotalStarted == 2 &&
                runtime.State.EcaRuleExecutionTotalFinished == 0
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
                "Allow: RuntimeState passed to Action is live",
                action.RuntimeStates[0]
                    .EcaRuleExecutionTotalStarted == 2
            );

            action.CompleteAll();

            await WaitUntil(
                () =>
                    runtime.State
                        .EcaRuleExecutionTotalFinished == 2
            );

            Expect(
                "Allow: all Executions removed after completion",
                runtime.Executions.Count == 0
            );

            Expect(
                "Allow: final state is 2 / 2",
                runtime.State.EcaRuleExecutionTotalStarted == 2 &&
                runtime.State.EcaRuleExecutionTotalFinished == 2
            );
        }

        private async Task TestRuleRuntimeFailedAction()
        {
            Debug.Log("--- RuleRuntime: Failed Action ---");

            var ruleRegistry = new EcaRuleRegistry();
            var ruleChecker = new EcaRuleChecker();
            var ruleRunner = new EcaRuleRunner();
            var runtimeRegistry = new EcaRuleRuntimeRegistry();

            var engine = new EcaRuleRuntimeEngine(
                ruleRegistry,
                ruleChecker,
                ruleRunner,
                runtimeRegistry
            );

            var testEvent = new EcaEvent<TestEventContext>(
                "test.runtime.failed",
                "Runtime Failed Event"
            );

            var rule =
                new EcaRule<EcaRuntimeContext<TestEventContext>>(
                    new EcaRuleConfig<
                        EcaRuntimeContext<TestEventContext>
                    >
                    {
                        Id = "test.runtime.failed.rule",
                        Name = "Runtime Failed Rule",
                        Event = testEvent,
                        Action =
                            new RuntimeThrowingAction<
                                TestEventContext
                            >()
                    }
                );

            engine.Register<TestEventContext>(
                rule,
                EcaOverlap.Ignore
            );

            var runtime = runtimeRegistry.Get(rule.Id);

            engine.Fire(
                testEvent,
                new TestEventContext(1)
            );

            await WaitUntil(
                () =>
                    runtime.State
                        .EcaRuleExecutionTotalFinished == 1
            );

            Expect(
                "Failed Action still increments Started",
                runtime.State.EcaRuleExecutionTotalStarted == 1
            );

            Expect(
                "Failed Action increments Finished",
                runtime.State.EcaRuleExecutionTotalFinished == 1
            );

            Expect(
                "Failed Execution is removed from Runtime",
                runtime.Executions.Count == 0
            );
        }

        // =====================================================================
        // TEST HELPERS
        // =====================================================================

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
                Debug.Log($"[PASS] {testName}");
                return;
            }

            _failed++;
            Debug.LogError($"[FAIL] {testName}");
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

        private readonly struct RuntimeRecord
        {
            public long Started { get; }
            public long Finished { get; }

            public RuntimeRecord(
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
                EcaContext<TEventContext> context,
                CancellationToken cancellationToken)
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
                EcaContext<TestEventContext> context,
                CancellationToken cancellationToken)
            {
                _state.Value = true;

                return Task.CompletedTask;
            }
        }

        // =====================================================================
        // RUNTIME CONDITIONS
        // =====================================================================

        private sealed class RuntimeRecordingCondition<TEventContext>
            : IEcaCondition<EcaRuntimeContext<TEventContext>>
        {
            public List<RuntimeRecord> Records { get; } = new();

            public bool Check(
                EcaRuntimeContext<TEventContext> context)
            {
                Records.Add(
                    new RuntimeRecord(
                        context.RuleRuntimeState
                            .EcaRuleExecutionTotalStarted,
                        context.RuleRuntimeState
                            .EcaRuleExecutionTotalFinished
                    )
                );

                return true;
            }
        }

        // =====================================================================
        // RUNTIME ACTIONS
        // =====================================================================

        private sealed class RuntimeGateAction<TEventContext>
            : IEcaAction<EcaRuntimeContext<TEventContext>>
        {
            private readonly List<TaskCompletionSource<bool>>
                _gates = new();

            public int RunCount { get; private set; }

            public List<RuntimeRecord> Records { get; } = new();

            public List<EcaRuleRuntimeState> RuntimeStates { get; } = new();

            public Task Run(
                EcaRuntimeContext<TEventContext> context,
                CancellationToken cancellationToken)
            {
                RunCount++;

                RuntimeStates.Add(
                    context.RuleRuntimeState
                );

                Records.Add(
                    new RuntimeRecord(
                        context.RuleRuntimeState
                            .EcaRuleExecutionTotalStarted,
                        context.RuleRuntimeState
                            .EcaRuleExecutionTotalFinished
                    )
                );

                var gate =
                    new TaskCompletionSource<bool>();

                cancellationToken.Register(
                    () => gate.TrySetCanceled()
                );

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

        private sealed class RuntimeThrowingAction<TEventContext>
            : IEcaAction<EcaRuntimeContext<TEventContext>>
        {
            public Task Run(
                EcaRuntimeContext<TEventContext> context,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException(
                    "Intentional smoke-test exception."
                );
            }
        }
    }
}