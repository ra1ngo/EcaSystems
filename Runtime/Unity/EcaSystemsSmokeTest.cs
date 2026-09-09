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

        private async void Start() => await RunTests();

        public async Task RunTests()
        {
            _passed = 0;
            _failed = 0;
            Debug.Log("=== EcaSystems Smoke Tests ===");

            TestBaseEmptyContext();
            TestBaseEventContext();
            TestBaseConditionFalse();
            TestBaseChecksAllConditionsBeforeActions();
            TestDuplicateRuleId();
            TestBaseContextSplit();
            await TestGenericRegistryAndVariance();
            await TestCommandsRegistry();
            await TestCommandsOrderAndScopes();
            await TestBindAcceptance();

            await TestExecutionIgnore();
            await TestExecutionAllow();
            await TestExecutionFailedAction();
            await TestExecutionConditionOrder();
            await TestRunModes();
            await TestFailureStatus();
            await TestExecutionUnregister();
            TestRegistryValidation();
            TestScopeLifetime();
            await TestScopeIsolation();
            await TestScopeLocalFire();
            await TestScopeRunningDispose();

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

            var rule = new EcaRule<EcaEventContextEmpty, EcaConditionContext<EcaEventContextEmpty>, EcaActionContext<EcaEventContextEmpty>>(
                new EcaRuleConfig<EcaEventContextEmpty, EcaConditionContext<EcaEventContextEmpty>, EcaActionContext<EcaEventContextEmpty>>
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

            var rule = new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
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
                new DelegateEcaCondition<EcaConditionContext<TestEventContext>>(
                    _ => false
                );

            var rule = new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
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
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
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
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
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
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                    {
                        Id = "same.rule.id",
                        Name = "Rule One",
                        Event = testEvent,
                        Action = new BaseCountingAction<TestEventContext>()
                    }
                );

            var rule2 =
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
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

        private async Task TestExecutionIgnore()
        {
            Debug.Log("--- Execution: Ignore ---");

            var ruleRegistry = new EcaRuleRegistry();
            var ruleChecker = new EcaRuleChecker();
            var ruleRunner = new EcaRuleRunner();
            var executionRegistry = new EcaRuleExecutionRegistry();

            var engine = new EcaExecutionEngine(
                ruleRegistry,
                new EcaRuleSelector(ruleRegistry),
                ruleChecker,
                executionRegistry,
                new EcaCommandRunner(new EcaCommandRegistry()),
                ruleRunner
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
                new EcaRule<TestEventContext, IEcaExecutionConditionContext<TestEventContext>, IEcaExecutionActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, IEcaExecutionConditionContext<TestEventContext>, IEcaExecutionActionContext<TestEventContext>>
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
                new EcaRunMode(EcaOverlap.Ignore)
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
            var ruleRunner = new EcaRuleRunner();
            var executionRegistry = new EcaRuleExecutionRegistry();

            var engine = new EcaExecutionEngine(
                ruleRegistry,
                new EcaRuleSelector(ruleRegistry),
                ruleChecker,
                executionRegistry,
                new EcaCommandRunner(new EcaCommandRegistry()),
                ruleRunner
            );

            var testEvent = new EcaEvent<TestEventContext>(
                "test.execution.allow",
                "Execution Allow Event"
            );

            var action =
                new ExecutionGateAction<TestEventContext>();

            var rule =
                new EcaRule<TestEventContext, IEcaExecutionConditionContext<TestEventContext>, IEcaExecutionActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, IEcaExecutionConditionContext<TestEventContext>, IEcaExecutionActionContext<TestEventContext>>
                    {
                        Id = "test.execution.allow.rule",
                        Name = "Execution Allow Rule",
                        Event = testEvent,
                        Action = action
                    }
                );

            engine.Register<TestEventContext>(
                rule,
                new EcaRunMode(EcaOverlap.Allow)
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
            var ruleRunner = new EcaRuleRunner();
            var executionRegistry = new EcaRuleExecutionRegistry();

            var engine = new EcaExecutionEngine(
                ruleRegistry,
                new EcaRuleSelector(ruleRegistry),
                ruleChecker,
                executionRegistry,
                new EcaCommandRunner(new EcaCommandRegistry()),
                ruleRunner
            );

            var testEvent = new EcaEvent<TestEventContext>(
                "test.execution.failed",
                "Execution Failed Event"
            );

            var rule =
                new EcaRule<TestEventContext, IEcaExecutionConditionContext<TestEventContext>, IEcaExecutionActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, IEcaExecutionConditionContext<TestEventContext>, IEcaExecutionActionContext<TestEventContext>>
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
                new EcaRunMode(EcaOverlap.Ignore)
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


        private static EcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> CreateExecutionRule<TEventContext>(
            string id, EcaEvent<TEventContext> ecaEvent,
            IEcaAction<IEcaExecutionActionContext<TEventContext>> action,
            IEcaCondition<IEcaExecutionConditionContext<TEventContext>> condition = null)
        {
            return new EcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>>(
                new EcaRuleConfig<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>>
                {
                    Id = id, Name = id, Event = ecaEvent, Action = action, Condition = condition
                });
        }

        private async Task TestExecutionConditionOrder()
        {
            var registry = new EcaRuleRegistry();
            var groups = new EcaRuleExecutionRegistry();
            var engine = new EcaExecutionEngine(registry, new EcaRuleSelector(registry),
                new EcaRuleChecker(), groups, new EcaCommandRunner(new EcaCommandRegistry()), new EcaRuleRunner());
            var ecaEvent = new EcaEvent<TestEventContext>("test.execution.order", "Order");
            var first = new ExecutionGateAction<TestEventContext>();
            var second = new ExecutionGateAction<TestEventContext>();
            var rejected = new ExecutionGateAction<TestEventContext>();
            var firstRule = CreateExecutionRule("order.first", ecaEvent, first);
            var secondRule = CreateExecutionRule("order.second", ecaEvent, second,
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ => first.RunCount == 0));
            var rejectedRule = CreateExecutionRule("order.false", ecaEvent, rejected,
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ => false));
            engine.Register(firstRule, new EcaRunMode(EcaOverlap.Allow));
            engine.Register(secondRule, new EcaRunMode(EcaOverlap.Allow));
            engine.Register(rejectedRule, new EcaRunMode(EcaOverlap.Allow));
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

        private async Task TestRunModes()
        {
            var ecaEvent = new EcaEvent<TestEventContext>("test.limit", "Limit");
            var commandRunner = new EcaCommandRunner(new EcaCommandRegistry());
            foreach (var limit in new[] { -1, 0, 1, 2 })
            {
                var action = new ExecutionGateAction<TestEventContext>();
                var rule = CreateExecutionRule("limit." + limit, ecaEvent, action);
                var mode = limit == -1 ? new EcaRunMode(EcaOverlap.Allow) : new EcaRunMode(EcaOverlap.Allow, limit);
                var group = new EcaRuleExecutionGroup<TestEventContext>(rule, mode, new EcaRuleRunner());
                for (var i = 0; i < 4; i++)
                {
                    group.Fire(new TestEventContext(i), commandRunner);
                    var execution = group.Executions.Count > 0 ? group.Executions[0] : null;
                    action.CompleteAll();
                    await WaitUntil(() => group.Executions.Count == 0);
                    if (execution != null)
                        Expect("Successful execution is Completed", execution.Status == EcaRuleExecutionStatus.Completed);
                }
                var expected = limit == -1 ? 4 : limit;
                Expect("Limit " + limit + " counts actual starts", action.RunCount == expected &&
                    group.State.EcaRuleExecutionTotalStarted == expected &&
                    group.State.EcaRuleExecutionTotalFinished == expected);
            }

            var gate = new ExecutionGateAction<TestEventContext>();
            var limitedRule = CreateExecutionRule("limit.overlap", ecaEvent, gate);
            foreach (var overlap in new[] { EcaOverlap.Ignore, EcaOverlap.Allow })
            {
                var group = new EcaRuleExecutionGroup<TestEventContext>(limitedRule,
                    new EcaRunMode(overlap, 2), new EcaRuleRunner());
                var eventContext = new TestEventContext(0);
                group.Fire(eventContext, commandRunner);
                group.Fire(eventContext, commandRunner);
                group.Fire(eventContext, commandRunner);
                var active = overlap == EcaOverlap.Ignore ? 1 : 2;
                Expect(overlap + ": active Fire respects overlap and limit",
                    group.Executions.Count == active && group.State.EcaRuleExecutionTotalStarted == active);
                gate.CompleteAll();
                await WaitUntil(() => group.Executions.Count == 0);
                group.Fire(eventContext, commandRunner);
                Expect(overlap + ": ignored Fire does not consume limit", group.State.EcaRuleExecutionTotalStarted == 2);
                gate.CompleteAll();
                await WaitUntil(() => group.Executions.Count == 0);
                group.Fire(eventContext, commandRunner);
                Expect(overlap + ": exhausted limit prevents execution", group.Executions.Count == 0 &&
                    group.State.EcaRuleExecutionTotalFinished == 2);
            }

            var invalid = false;
            try { _ = new EcaRunMode(EcaOverlap.Allow, -2); }
            catch (ArgumentOutOfRangeException) { invalid = true; }
            Expect("Limit below -1 is rejected", invalid);

            var checks = 0;
            var blockedAction = new ExecutionGateAction<TestEventContext>();
            var blockedRule = CreateExecutionRule("limit.zero.conditions", ecaEvent, blockedAction,
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ => { checks++; return true; }));
            var engine = new EcaExecutionEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            engine.Register(blockedRule, new EcaRunMode(EcaOverlap.Allow, 0));
            engine.Fire(ecaEvent, new TestEventContext(0));
            Expect("Conditions run before zero limit is applied", checks == 1 && blockedAction.RunCount == 0);
        }

        private async Task TestFailureStatus()
        {
            var ecaEvent = new EcaEvent<TestEventContext>("test.failure.status", "Failure");
            var action = new ExecutionGateAction<TestEventContext>();
            var rule = CreateExecutionRule("failure.status", ecaEvent, action);
            var group = new EcaRuleExecutionGroup<TestEventContext>(rule, new EcaRunMode(EcaOverlap.Ignore), new EcaRuleRunner());
            group.Fire(new TestEventContext(1), new EcaCommandRunner(new EcaCommandRegistry()));
            var execution = group.Executions[0];
            var error = new InvalidOperationException("Expected asynchronous failure.");
            action.FailAll(error);
            await WaitUntil(() => group.Executions.Count == 0);
            Expect("Faulted Run records Failed and the original exception",
                execution.Status == EcaRuleExecutionStatus.Failed && ReferenceEquals(execution.Exception, error));
            Expect("Failure balances counters", group.State.EcaRuleExecutionTotalStarted == 1 &&
                group.State.EcaRuleExecutionTotalFinished == 1);

            group.Fire(new TestEventContext(2), new EcaCommandRunner(new EcaCommandRegistry()));
            execution = group.Executions[0];
            var interrupted = new OperationCanceledException("User Action failure.");
            action.FailAll(interrupted);
            await WaitUntil(() => group.Executions.Count == 0);
            Expect("OperationCanceledException is an ordinary failure",
                execution.Status == EcaRuleExecutionStatus.Failed && ReferenceEquals(execution.Exception, interrupted));
        }

        private async Task TestExecutionUnregister()
        {
            var rules = new EcaRuleRegistry();
            var groups = new EcaRuleExecutionRegistry();
            var engine = new EcaExecutionEngine(rules, new EcaRuleSelector(rules),
                new EcaRuleChecker(), groups, new EcaCommandRunner(new EcaCommandRegistry()), new EcaRuleRunner());
            var ecaEvent = new EcaEvent<TestEventContext>("test.unregister", "Unregister");
            var oldAction = new ExecutionGateAction<TestEventContext>();
            var oldRule = CreateExecutionRule("unregister.rule", ecaEvent, oldAction);
            var mode = new EcaRunMode(EcaOverlap.Ignore, 1);
            engine.Register(oldRule, mode);
            var oldGroup = groups.Get<TestEventContext>(oldRule.Id);
            engine.Fire(ecaEvent, new TestEventContext(1));
            var oldExecution = oldGroup.Executions[0];

            Expect("Execution unregister succeeds", engine.Unregister(oldRule));
            Expect("Unregister removes Rule and Group immediately", rules.Rules.Count == 0 &&
                !groups.TryGet<TestEventContext>(oldRule.Id, out _));
            Expect("Unregister leaves running Action unfinished", oldGroup.Executions.Count == 1 &&
                oldExecution.Status == EcaRuleExecutionStatus.Running &&
                oldGroup.State.EcaRuleExecutionTotalFinished == 0);
            engine.Fire(ecaEvent, new TestEventContext(2));
            Expect("Unregistered Rule receives no new Fire", oldAction.RunCount == 1);
            Expect("Repeated unregister returns false", !engine.Unregister(oldRule));

            var newAction = new ExecutionGateAction<TestEventContext>();
            var newRule = CreateExecutionRule(oldRule.Id, ecaEvent, newAction);
            engine.Register(newRule, mode);
            var newGroup = groups.Get<TestEventContext>(newRule.Id);
            Expect("Same RuleId immediately gets an independent Group and State",
                !ReferenceEquals(oldGroup, newGroup) && !ReferenceEquals(oldGroup.State, newGroup.State) &&
                newGroup.State.EcaRuleExecutionTotalStarted == 0 &&
                newGroup.State.EcaRuleExecutionTotalFinished == 0);
            Expect("Unregister of old Rule cannot remove replacement", !engine.Unregister(oldRule) &&
                ReferenceEquals(newGroup, groups.Get<TestEventContext>(newRule.Id)));
            engine.Fire(ecaEvent, new TestEventContext(3));
            var newExecution = newGroup.Executions[0];
            Expect("New Limit lifetime permits a run while old Action is still running",
                newAction.RunCount == 1 && oldExecution.Status == EcaRuleExecutionStatus.Running &&
                newExecution.Status == EcaRuleExecutionStatus.Running);

            oldAction.CompleteAll();
            await WaitUntil(() => oldGroup.Executions.Count == 0);
            Expect("Old Action completes naturally after unregister",
                oldExecution.Status == EcaRuleExecutionStatus.Completed &&
                oldGroup.State.EcaRuleExecutionTotalStarted == 1 &&
                oldGroup.State.EcaRuleExecutionTotalFinished == 1);
            Expect("Old completion leaves new Group registered and counters independent",
                ReferenceEquals(newGroup, groups.Get<TestEventContext>(newRule.Id)) &&
                newGroup.Executions.Count == 1 && newGroup.State.EcaRuleExecutionTotalStarted == 1 &&
                newGroup.State.EcaRuleExecutionTotalFinished == 0);
            newAction.CompleteAll();
            await WaitUntil(() => newGroup.Executions.Count == 0);
            engine.Fire(ecaEvent, new TestEventContext(4));
            Expect("Replacement obeys its own Limit after completion", newAction.RunCount == 1 &&
                newExecution.Status == EcaRuleExecutionStatus.Completed &&
                newGroup.State.EcaRuleExecutionTotalFinished == 1);
            Expect("Idle Group is removed on unregister", engine.Unregister(newRule) &&
                !groups.TryGet<TestEventContext>(newRule.Id, out _));
            engine.Register(newRule, mode);
            Expect("The same Rule instance can be registered with fresh state",
                !ReferenceEquals(newGroup, groups.Get<TestEventContext>(newRule.Id)) &&
                groups.Get<TestEventContext>(newRule.Id).State.EcaRuleExecutionTotalStarted == 0);
            engine.Unregister(newRule);

            var nullRejected = false;
            try { engine.Unregister<TestEventContext>(null); }
            catch (ArgumentNullException) { nullRejected = true; }
            Expect("Execution unregister rejects null", nullRejected);
        }

        private void TestRegistryValidation()
        {
            var registry = new EcaRuleRegistry();
            var selector = new EcaRuleSelector(registry);
            var groups = new EcaRuleExecutionRegistry();
            var ruleRunner = new EcaRuleRunner();
            var ecaEvent = new EcaEvent<TestEventContext>("validation", "Validation");
            var rule = CreateExecutionRule("validation.rule", ecaEvent, new ExecutionGateAction<TestEventContext>());
            groups.Register(rule, new EcaRunMode(EcaOverlap.Allow), ruleRunner);
            var original = groups.Get<TestEventContext>(rule.Id);
            groups.Register(rule, new EcaRunMode(EcaOverlap.Allow), ruleRunner);
            Expect("Equivalent RunMode preserves the group", ReferenceEquals(original, groups.Get<TestEventContext>(rule.Id)));
            ExpectThrows("Registration rejects limit mismatch",
                () => groups.Register(rule, new EcaRunMode(EcaOverlap.Allow, 1), ruleRunner));
            ExpectThrows("Registration rejects another Rule with same id",
                () => groups.Register(CreateExecutionRule(rule.Id, ecaEvent, new ExecutionGateAction<TestEventContext>()),
                    new EcaRunMode(EcaOverlap.Allow), ruleRunner));
            ExpectThrows("Typed Get rejects incompatible group", () => groups.Get<EcaEventContextEmpty>(rule.Id));
            ExpectThrows("Typed TryGet rejects incompatible group",
                () => groups.TryGet<EcaEventContextEmpty>(rule.Id, out _));
            Expect("TryGet returns false for missing group", !groups.TryGet<TestEventContext>("missing", out _));
            var engine = new EcaExecutionEngine(registry, selector, new EcaRuleChecker(),
                groups, new EcaCommandRunner(new EcaCommandRegistry()), ruleRunner);
            ExpectThrows("Registration rejects overlap mismatch", () => engine.Register(rule, new EcaRunMode(EcaOverlap.Ignore)));
            Expect("Failed group registration rolls back Rule registration", registry.Rules.Count == 0);
            engine.Register(rule, new EcaRunMode(EcaOverlap.Allow));
            Expect("Registration can succeed after rollback", registry.Rules.Count == 1);
            ExpectThrows("Selector rejects incompatible full Context",
                () => selector.ForEvent<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(ecaEvent));
            var conflictingEvent = new EcaEvent<EcaEventContextEmpty>("validation", "Conflicting event");
            ExpectThrows("Selector rejects EventId collision with incompatible payload",
                () => selector.ForEvent<EcaEventContextEmpty, EcaConditionContext<EcaEventContextEmpty>, EcaActionContext<EcaEventContextEmpty>>(conflictingEvent));
            Expect("Registry exposes a read-only collection",
                ((ICollection<IEcaRule>)registry.Rules).IsReadOnly);
        }

        private void TestScopeLifetime()
        {
            var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            Expect("Scope engine starts empty", engine.ScopeCount == 0);
            var root = engine.CreateScope("scope-1");
            var child = root.CreateScope("child");
            var grandchild = child.CreateScope();
            var sibling = root.CreateScope();
            var other = engine.CreateScope("other");
            Expect("Root and child identity", root.ScopeId == "scope-1" && root.ParentScopeId == null &&
                child.ParentScopeId == root.ScopeId && grandchild.ParentScopeId == child.ScopeId);
            Expect("Auto ids skip explicit ids and differ", grandchild.ScopeId == "scope-2" &&
                sibling.ScopeId == "scope-3");
            Expect("Scope count and lookup", engine.ScopeCount == 5 &&
                engine.TryGetScope(child.ScopeId, out var found) && ReferenceEquals(child, found));
            ExpectThrows("Duplicate root id rejected", () => engine.CreateScope("child"));
            ExpectThrows("Duplicate child id rejected", () => root.CreateScope("other"));
            ExpectException<ArgumentException>("Empty scope id rejected", () => engine.CreateScope(""));
            ExpectException<ArgumentException>("Whitespace child id rejected", () => root.CreateScope(" \t"));
            Expect("Invalid creation leaves registry intact", engine.ScopeCount == 5);

            child.Dispose();
            child.Dispose();
            Expect("Child disposal cascades without affecting parent or sibling",
                child.IsDisposed && grandchild.IsDisposed && !root.IsDisposed && !sibling.IsDisposed &&
                engine.ScopeCount == 3 && !engine.TryGetScope("child", out _) &&
                !engine.TryGetScope(grandchild.ScopeId, out _));
            var replacement = root.CreateScope("child");
            child.Dispose();
            Expect("Stale disposal leaves reused id intact", engine.TryGetScope("child", out found) &&
                ReferenceEquals(found, replacement));
            var emptyEvent = new EcaEvent<EcaEventContextEmpty>("scope.empty", "Empty");
            ExpectException<ObjectDisposedException>("Disposed Register rejected",
                () => child.Register<TestEventContext>(null, null));
            ExpectException<ObjectDisposedException>("Disposed Unregister rejected",
                () => child.Unregister<TestEventContext>(null));
            ExpectException<ObjectDisposedException>("Disposed empty Fire rejected", () => child.Fire(emptyEvent));
            ExpectException<ObjectDisposedException>("Disposed typed Fire rejected",
                () => child.Fire(emptyEvent, EcaEventContextEmpty.Value));
            ExpectException<ObjectDisposedException>("Disposed child creation rejected", () => child.CreateScope());
            var nested = replacement.CreateScope();
            root.Dispose();
            Expect("Parent disposal removes all descendants", root.IsDisposed && sibling.IsDisposed &&
                replacement.IsDisposed && nested.IsDisposed && engine.ScopeCount == 1 &&
                !engine.TryGetScope(root.ScopeId, out _) && !engine.TryGetScope(sibling.ScopeId, out _) &&
                !engine.TryGetScope(replacement.ScopeId, out _) && !engine.TryGetScope(nested.ScopeId, out _));
            var otherChild = other.CreateScope();
            var anotherRoot = engine.CreateScope();
            engine.Dispose();
            engine.Dispose();
            Expect("Engine disposal clears roots and descendants", other.IsDisposed && otherChild.IsDisposed &&
                anotherRoot.IsDisposed && engine.ScopeCount == 0 && !engine.TryGetScope("other", out _));
            ExpectException<ObjectDisposedException>("Disposed engine creation rejected", () => engine.CreateScope());
            ExpectException<ObjectDisposedException>("Engine-disposed scope rejects Fire", () => other.Fire(emptyEvent));
        }

        private async Task TestScopeIsolation()
        {
            foreach (var overlap in new[] { EcaOverlap.Ignore, EcaOverlap.Allow })
            {
                using var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
                var a = engine.CreateScope();
                var b = engine.CreateScope();
                var evt = new EcaEvent<TestEventContext>("scope.shared", "Shared");
                var action = new ExecutionGateAction<TestEventContext>();
                var rule = CreateExecutionRule("scope.shared.rule", evt, action);
                a.Register(rule, new EcaRunMode(overlap, 1));
                b.Register(rule, new EcaRunMode(overlap, 1));
                a.Fire(evt, new TestEventContext(1));
                a.Fire(evt, new TestEventContext(2));
                Expect(overlap + ": Fire A is local and Limit is one", action.RunCount == 1);
                b.Fire(evt, new TestEventContext(3));
                Expect(overlap + ": same Rule has independent state and Limit in B", action.RunCount == 2 &&
                    !ReferenceEquals(action.ExecutionGroupStates[0], action.ExecutionGroupStates[1]) &&
                    action.Records[0].Started == 1 && action.Records[1].Started == 1);
                action.CompleteAll();
                await WaitUntil(() => action.ExecutionGroupStates[1].EcaRuleExecutionTotalFinished == 1);
                a.Fire(evt, new TestEventContext(4));
                b.Fire(evt, new TestEventContext(5));
                Expect(overlap + ": both limits remain exhausted", action.RunCount == 2);
                Expect("Unregister A is independent", a.Unregister(rule) && !a.Unregister(rule));
                a.Register(rule, new EcaRunMode(overlap));
                a.Fire(evt, new TestEventContext(6));
                b.Fire(evt, new TestEventContext(7));
                Expect("Re-register A resets only its lifetime", action.RunCount == 3);
                action.CompleteAll();
                await WaitUntil(() => action.ExecutionGroupStates[2].EcaRuleExecutionTotalFinished == 1);
                b.Unregister(rule);
                b.Register(rule, new EcaRunMode(overlap));
                a.Fire(evt, new TestEventContext(8));
                b.Fire(evt, new TestEventContext(9));
                a.Fire(evt, new TestEventContext(10));
                b.Fire(evt, new TestEventContext(11));
                var expected = overlap == EcaOverlap.Ignore ? 5 : 7;
                Expect(overlap + ": active overlap is independent across scopes", action.RunCount == expected);
                action.CompleteAll();
                await WaitUntil(() => action.ExecutionGroupStates[expected - 1].EcaRuleExecutionTotalFinished ==
                    (overlap == EcaOverlap.Ignore ? 1 : 2));
            }
        }

        private async Task TestScopeLocalFire()
        {
            using var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            var parent = engine.CreateScope();
            var child = parent.CreateScope();
            var evt = new EcaEvent<EcaEventContextEmpty>("scope.local", "Local");
            var parentAction = new ExecutionGateAction<EcaEventContextEmpty>();
            var childAction = new ExecutionGateAction<EcaEventContextEmpty>();
            parent.Register(CreateExecutionRule("scope.parent", evt, parentAction), new EcaRunMode(EcaOverlap.Allow));
            child.Register(CreateExecutionRule("scope.child", evt, childAction), new EcaRunMode(EcaOverlap.Allow));
            child.Fire(evt);
            Expect("Child Fire does not reach parent", childAction.RunCount == 1 && parentAction.RunCount == 0);
            parent.Fire(evt);
            Expect("Parent Fire does not reach child", childAction.RunCount == 1 && parentAction.RunCount == 1);
            parentAction.CompleteAll();
            childAction.CompleteAll();
            await WaitUntil(() => parentAction.ExecutionGroupStates[0].EcaRuleExecutionTotalFinished == 1 &&
                childAction.ExecutionGroupStates[0].EcaRuleExecutionTotalFinished == 1);
        }

        private async Task TestScopeRunningDispose()
        {
            using var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            var parent = engine.CreateScope();
            var scope = parent.CreateScope("running");
            var evt = new EcaEvent<TestEventContext>("scope.running", "Running");
            var action = new ExecutionGateAction<TestEventContext>();
            var rule = CreateExecutionRule("scope.running.rule", evt, action);
            scope.Register(rule, new EcaRunMode(EcaOverlap.Ignore, 1));
            scope.Fire(evt, new TestEventContext(0));
            var oldState = action.ExecutionGroupStates[0];
            parent.Dispose();
            Expect("Dispose immediately removes running scope without waiting", scope.IsDisposed &&
                engine.ScopeCount == 0 && !engine.TryGetScope("running", out _) &&
                oldState.EcaRuleExecutionTotalFinished == 0);
            var replacement = engine.CreateScope("running");
            replacement.Register(rule, new EcaRunMode(EcaOverlap.Ignore, 1));
            action.CompleteAll();
            await WaitUntil(() => oldState.EcaRuleExecutionTotalFinished == 1);
            replacement.Fire(evt, new TestEventContext(1));
            Expect("Old Action completion preserves replacement and its Limit", action.RunCount == 2 &&
                engine.TryGetScope("running", out var found) && ReferenceEquals(found, replacement) &&
                !ReferenceEquals(oldState, action.ExecutionGroupStates[1]) &&
                action.ExecutionGroupStates[1].EcaRuleExecutionTotalFinished == 0);
            engine.Dispose();
            action.CompleteAll();
            await WaitUntil(() => action.ExecutionGroupStates[1].EcaRuleExecutionTotalFinished == 1);
            Expect("Action finishes naturally after engine Dispose", replacement.IsDisposed && engine.ScopeCount == 0);
        }

        private void TestBaseContextSplit()
        {
            var evt = new EcaEvent<TestEventContext>("split", "Split");
            var payload = new TestEventContext(73);
            var action = new BaseCountingAction<TestEventContext>();
            var seen = 0;
            var rule = new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                {
                    Id = "split", Name = "Split", Event = evt, Action = action,
                    Condition = new DelegateEcaCondition<EcaConditionContext<TestEventContext>>(context =>
                    {
                        seen = context.EventContext.Value;
                        return true;
                    })
                });
            var engine = new EcaEngine();
            engine.Register(rule);
            engine.Fire(evt, payload);
            Expect("Разные Base contexts получают один payload", seen == 73 && action.LastValue == 73);
            Expect("Condition не предоставляет Commands",
                !typeof(IEcaCommandsActionContext).IsAssignableFrom(typeof(EcaConditionContext<TestEventContext>)) &&
                !typeof(IEcaCommandsActionContext).IsAssignableFrom(typeof(EcaExecutionConditionContext<TestEventContext>)) &&
                typeof(EcaExecutionConditionContext<TestEventContext>).GetProperty("Commands") == null &&
                typeof(IEcaExecutionConditionContext<TestEventContext>).GetProperty("Commands") == null);
            ExpectException<ArgumentException>("Rule проверяет явный тип payload", () =>
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                    {
                        Id = "wrong", Name = "Wrong", Event = new EcaEvent<int>("wrong", "Wrong"), Action = action
                    }));
        }

        private async Task TestGenericRegistryAndVariance()
        {
            var evt = new EcaEvent<TestEventContext>("custom.context", "Custom context");
            var action = new BaseCountingAction<TestEventContext>();
            var rule = new EcaRule<TestEventContext, CustomConditionContext, CustomActionContext>(
                new EcaRuleConfig<TestEventContext, CustomConditionContext, CustomActionContext>
                {
                    Id = "custom.rule", Name = "Custom rule", Event = evt, Action = action,
                    Condition = new DelegateEcaCondition<CustomConditionContext>(context => context.EventContext.Value == 17)
                });
            var registry = new EcaRuleRegistry();
            registry.Register(rule);
            var selector = new EcaRuleSelector(registry);
            var selected = selector.ForEvent<TestEventContext, CustomConditionContext, CustomActionContext>(evt);
            Expect("Общий Registry хранит Rule с пользовательской парой контекстов",
                selected.Count == 1 && ReferenceEquals(selected[0], rule));
            Expect("Общий Checker проверяет пользовательский ConditionContext",
                new EcaRuleChecker().Check(rule, new CustomConditionContext(new TestEventContext(17))));
            await new EcaRuleRunner().Run(rule, new CustomActionContext(new TestEventContext(17)));
            Expect("Общий Runner выполняет пользовательский ActionContext", action.LastValue == 17);
            ExpectThrows("Selector отклоняет другую пару role contexts", () =>
                selector.ForEvent<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(evt));
            Expect("Selector проверяет Event.Id", selector.ForEvent<TestEventContext, CustomConditionContext, CustomActionContext>(
                new EcaEvent<TestEventContext>("other", "Other")).Count == 0);
            ExpectThrows("Selector не расширяет payload до object", () =>
                selector.ForEvent<object, EcaConditionContext<object>, EcaActionContext<object>>(evt));
            Expect("Общий Registry удаляет пользовательскую Rule", registry.Unregister(rule));

            Expect("Все role-интерфейсы invariant по payload",
                !typeof(IEcaConditionContext<object>).IsAssignableFrom(typeof(IEcaConditionContext<string>)) &&
                !typeof(IEcaActionContext<object>).IsAssignableFrom(typeof(IEcaActionContext<string>)) &&
                !typeof(IEcaCommandsActionContext<object>).IsAssignableFrom(typeof(IEcaCommandsActionContext<string>)) &&
                !typeof(IEcaExecutionConditionContext<object>).IsAssignableFrom(typeof(IEcaExecutionConditionContext<string>)) &&
                !typeof(IEcaExecutionActionContext<object>).IsAssignableFrom(typeof(IEcaExecutionActionContext<string>)));
            Expect("Read-only IEcaContext сохраняет covariance",
                typeof(IEcaContext<object>).IsAssignableFrom(typeof(IEcaContext<string>)));
        }

        private sealed class CustomConditionContext : EcaConditionContext<TestEventContext>
        {
            public CustomConditionContext(TestEventContext context) : base(context) { }
        }

        private sealed class CustomActionContext : EcaActionContext<TestEventContext>
        {
            public CustomActionContext(TestEventContext context) : base(context) { }
        }

        private async Task TestBindAcceptance()
        {
            foreach (var overlap in new[] { EcaOverlap.Ignore, EcaOverlap.Allow })
            foreach (var limit in new[] { 0, 2 })
            {
                var checks = 0;
                var runner = new RecordingCommandRunner(new EcaCommandRunner(new EcaCommandRegistry()), new List<string>());
                var rules = new EcaRuleRegistry();
                var groups = new EcaRuleExecutionRegistry();
                var engine = new EcaExecutionEngine(rules, new EcaRuleSelector(rules), new EcaRuleChecker(),
                    groups, runner, new EcaRuleRunner());
                var evt = new EcaEvent<TestEventContext>("bind.acceptance", "Bind acceptance");
                var action = new ExecutionGateAction<TestEventContext>();
                var rule = CreateExecutionRule("bind.acceptance", evt, action,
                    new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ => { checks++; return true; }));
                engine.Register(rule, new EcaRunMode(overlap, limit));
                var group = groups.Get<TestEventContext>(rule.Id);
                runner.OnBind = context =>
                {
                    var typed = (IEcaExecutionActionContext<TestEventContext>)context;
                    Expect("Runner получает подготовленные payload и GroupState",
                        typed.EventContext.Value == 42 && ReferenceEquals(typed.RuleExecutionGroupState, group.State));
                    ExpectThrows("До завершения Bind чтение Commands явно отклоняется", () => { _ = typed.Commands; });
                };
                engine.Fire(evt, new TestEventContext(42));
                var first = limit == 0 ? 0 : 1;
                Expect(overlap + ": первый Fire привязывает только принятый execution",
                    checks == 1 && runner.BindCount == first && action.RunCount == first &&
                    group.Executions.Count == first && group.State.EcaRuleExecutionTotalStarted == first);
                if (first == 1)
                    Expect("Опубликованный execution уже имеет Commands", group.Executions[0].Context.Commands != null);
                engine.Fire(evt, new TestEventContext(42));
                var active = limit == 0 ? 0 : overlap == EcaOverlap.Ignore ? 1 : 2;
                Expect(overlap + ": повторный Fire проверяет Condition без лишнего Bind",
                    checks == 2 && runner.BindCount == active && action.RunCount == active &&
                    group.Executions.Count == active && group.State.EcaRuleExecutionTotalStarted == active);
                action.CompleteAll();
                await WaitUntil(() => group.Executions.Count == 0);
                engine.Fire(evt, new TestEventContext(42));
                var total = limit == 0 ? 0 : 2;
                Expect(overlap + ": после завершения Ignore снова разрешает Bind, исчерпанный Limit блокирует",
                    checks == 3 && runner.BindCount == total && action.RunCount == total &&
                    group.State.EcaRuleExecutionTotalStarted == total);
                action.CompleteAll();
                await WaitUntil(() => group.Executions.Count == 0);
                engine.Fire(evt, new TestEventContext(42));
                Expect(overlap + ": Bind ровно один раз на фактический запуск",
                    checks == 4 && runner.BindCount == total && group.Executions.Count == 0 &&
                    group.State.EcaRuleExecutionTotalStarted == total && group.State.EcaRuleExecutionTotalFinished == total);
            }
        }

        private async Task TestCommandsRegistry()
        {
            var registry = new EcaCommandRegistry();
            var runner = new EcaCommandRunner(registry);
            var received = new List<IEcaActionContext>();
            var command = new TestCommand<IEcaActionContext, int>("record", (context, value) =>
            {
                received.Add(context);
                return Task.CompletedTask;
            });
            registry.Register(command);
            ExpectThrows("Дубликат string Command ID отклонён", () => registry.Register(
                new TestCommand<IEcaActionContext, string>("record", (_, __) => Task.CompletedTask)));
            foreach (var id in new[] { null, "", " \t" })
                ExpectException<ArgumentException>("Пустой Command ID отклонён", () => registry.Register(
                    new TestCommand<IEcaActionContext, int>(id, (_, __) => Task.CompletedTask)));
            ExpectException<ArgumentNullException>("Bind null отклонён", () => runner.Bind(null));
            var contextA = new EcaActionContext<int>(1);
            var contextB = new EcaActionContext<string>("B");
            var boundA = runner.Bind(contextA);
            var boundB = runner.Bind(contextB);
            await boundA.Run("record", 1);
            await boundB.Run("record", 2);
            await boundA.Run("record", 3);
            Expect("Каждый bound API сохраняет свой current context", received.Count == 3 &&
                ReferenceEquals(received[0], contextA) && ReferenceEquals(received[1], contextB) &&
                ReferenceEquals(received[2], contextA));
            ExpectCommandError<InvalidOperationException>("Неизвестный ID", "missing", () => boundA.Run("missing", 1));
            ExpectCommandError<ArgumentException>("Неверный тип args", "record", () => boundA.Run("record", "wrong"));
            ExpectCommandError<ArgumentException>("Несовместимый null args", "record", () => boundA.Run<string>("record", null));
            registry.Register(new TestCommand<IEcaActionContext<string>, string>("text", (_, __) => Task.CompletedTask));
            ExpectCommandError<InvalidOperationException>("Несовместимый ActionContext", "text", () => boundA.Run("text", "value"));
            await boundB.Run<string>("text", null);
            Expect("Допустимый null reference args передан", true);
            registry.Register(new TestCommand<IEcaActionContext, int?>("nullable", (_, __) => Task.CompletedTask));
            await boundA.Run<int?>("nullable", null);
            await boundA.Run<int?>("nullable", 5);
            Expect("Nullable args допускают значение и null", true);
            Expect("Unregister удаляет Command", registry.Unregister("record") && !registry.Unregister("record"));
            ExpectCommandError<InvalidOperationException>("Старый bound API видит Unregister", "record", () => boundA.Run("record", 1));
            registry.Register(command);
            await boundA.Run("record", 4);
            Expect("ID доступен для повторной регистрации", received.Count == 4);
            registry.Register(new TestCommand<IEcaActionContext, int>("null-task", (_, __) => null));
            ExpectCommandError<InvalidOperationException>("Null Task диагностируется", "null-task", () => boundA.Run("null-task", 1));
        }

        private async Task TestCommandsOrderAndScopes()
        {
            var log = new List<string>();
            var registry = new EcaCommandRegistry();
            var runner = new RecordingCommandRunner(new EcaCommandRunner(registry), log);
            var received = new List<IEcaExecutionActionContext<TestEventContext>>();
            registry.Register(new TestCommand<IEcaExecutionActionContext<TestEventContext>, string>("record", (context, label) =>
            {
                received.Add(context);
                log.Add(label);
                return Task.CompletedTask;
            }));
            var evt = new EcaEvent<TestEventContext>("commands.order", "Order");
            using var scopes = new EcaScopeEngine(runner);
            var a = scopes.CreateScope();
            var b = scopes.CreateScope();
            var actionA = new CommandsTestAction("A", log);
            var actionB = new CommandsTestAction("B", log);
            var ruleA = CreateExecutionRule("A", evt, actionA,
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(context =>
                {
                    log.Add("condition A");
                    Expect("Condition получает payload до bind", context.EventContext.Value == 42 && runner.BindCount == 0);
                    return true;
                }));
            var ruleB = CreateExecutionRule("B", evt, actionB,
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ =>
                {
                    log.Add("condition B");
                    return true;
                }));
            var rejected = CreateExecutionRule("rejected", evt, new CommandsTestAction("rejected", log),
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ =>
                {
                    log.Add("condition rejected");
                    return false;
                }));
            a.Register(ruleA, new EcaRunMode(EcaOverlap.Allow, 1));
            a.Register(ruleB, new EcaRunMode(EcaOverlap.Allow, 1));
            a.Register(rejected, new EcaRunMode(EcaOverlap.Allow));
            a.Fire(evt, new TestEventContext(42));
            Expect("Все Conditions до любого bind, Action и Command", string.Join(",", log) ==
                "condition A,condition B,condition rejected,bind,action A,A1,A2,bind,action B,B1,B2");
            Expect("False Condition не вызывает bind", runner.BindCount == 2);
            Expect("Command автоматически получает точный ActionContext и payload", received.Count == 4 &&
                ReferenceEquals(received[0], actionA.Contexts[0]) && ReferenceEquals(received[1], actionA.Contexts[0]) &&
                ReferenceEquals(received[2], actionB.Contexts[0]) && received[0].EventContext.Value == 42);
            b.Register(ruleB, new EcaRunMode(EcaOverlap.Allow, 1));
            b.Fire(evt, new TestEventContext(99));
            Expect("Общая Command работает во втором Scope", received.Count == 6 && received[4].EventContext.Value == 99);
            Expect("Одна Rule имеет независимые GroupState и Limit", actionB.Contexts.Count == 2 &&
                !ReferenceEquals(actionB.Contexts[0].RuleExecutionGroupState, actionB.Contexts[1].RuleExecutionGroupState) &&
                actionB.Contexts[0].RuleExecutionGroupState.EcaRuleExecutionTotalStarted == 1 &&
                actionB.Contexts[1].RuleExecutionGroupState.EcaRuleExecutionTotalStarted == 1);
            a.Unregister(ruleB);
            b.Fire(evt, new TestEventContext(100));
            Expect("Unregister в A не сбрасывает Limit в B", received.Count == 6 && b.Unregister(ruleB));

            // Следующая Command должна дождаться завершения предыдущей, включая async-паузу.
            var gate = new TaskCompletionSource<bool>();
            registry.Register(new TestCommand<IEcaActionContext, int>("gate", (_, __) => gate.Task));
            var completed = false;
            var bound = runner.Bind(new EcaActionContext<int>(0));
            async Task Sequence()
            {
                await bound.Run("gate", 0);
                await bound.Run("nullable", 0);
                completed = true;
            }
            registry.Register(new TestCommand<IEcaActionContext, int>("nullable", (_, __) => Task.CompletedTask));
            var sequence = Sequence();
            Expect("Последовательность ожидает async Command", !completed);
            gate.SetResult(true);
            await sequence;
            Expect("Следующая Command запускается после завершения предыдущей", completed);
        }

        private void ExpectCommandError<TException>(string name, string commandId, Action action)
            where TException : Exception
        {
            try { action(); }
            catch (TException error) { Expect(name, error.Message.Contains(commandId)); return; }
            Expect(name, false);
        }

        private sealed class TestCommand<TContext, TArgs> : IEcaCommand<TContext, TArgs>
            where TContext : IEcaActionContext
        {
            private readonly Func<TContext, TArgs, Task> _run;
            public string Id { get; }
            public TestCommand(string id, Func<TContext, TArgs, Task> run) { Id = id; _run = run; }
            public Task Run(TContext context, TArgs args) => _run(context, args);
        }

        private sealed class RecordingCommandRunner : IEcaCommandRunner
        {
            private readonly IEcaCommandRunner _runner;
            private readonly List<string> _log;
            public int BindCount { get; private set; }
            public Action<IEcaActionContext> OnBind { get; set; }
            public RecordingCommandRunner(IEcaCommandRunner runner, List<string> log) { _runner = runner; _log = log; }
            public IEcaCommands Bind(IEcaActionContext context)
            {
                BindCount++;
                _log.Add("bind");
                OnBind?.Invoke(context);
                return _runner.Bind(context);
            }
        }

        private sealed class CommandsTestAction : IEcaAction<IEcaExecutionActionContext<TestEventContext>>
        {
            private readonly string _label;
            private readonly List<string> _log;
            public List<IEcaExecutionActionContext<TestEventContext>> Contexts { get; } = new();
            public CommandsTestAction(string label, List<string> log) { _label = label; _log = log; }
            public async Task Run(IEcaExecutionActionContext<TestEventContext> context)
            {
                Contexts.Add(context);
                _log.Add("action " + _label);
                await context.Commands.Run("record", _label + "1");
                await context.Commands.Run("record", _label + "2");
            }
        }

        private void ExpectException<TException>(string name, Action action) where TException : Exception
        {
            try { action(); }
            catch (TException) { Expect(name, true); return; }
            Expect(name, false);
        }

        private void ExpectThrows(string name, Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { Expect(name, true); return; }
            Expect(name, false);
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
            : IEcaCondition<EcaConditionContext<TestEventContext>>
        {
            private readonly SharedState _state;

            public FlagMustBeFalseCondition(
                SharedState state)
            {
                _state = state;
            }

            public bool Check(
                EcaConditionContext<TestEventContext> context)
            {
                return !_state.Value;
            }
        }

        // =====================================================================
        // BASE ACTIONS
        // =====================================================================

        private sealed class BaseCountingAction<TEventContext>
            : IEcaAction<EcaActionContext<TEventContext>>
        {
            public int RunCount { get; private set; }
            public int LastValue { get; private set; }

            public Task Run(
                EcaActionContext<TEventContext> context)
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
            : IEcaAction<EcaActionContext<TestEventContext>>
        {
            private readonly SharedState _state;

            public SetFlagAction(
                SharedState state)
            {
                _state = state;
            }

            public Task Run(
                EcaActionContext<TestEventContext> context)
            {
                _state.Value = true;

                return Task.CompletedTask;
            }
        }

        // =====================================================================
        // EXECUTION CONDITIONS
        // =====================================================================

        private sealed class ExecutionRecordingCondition<TEventContext>
            : IEcaCondition<IEcaExecutionConditionContext<TEventContext>>
        {
            public List<ExecutionRecord> Records { get; } = new();

            public bool Check(
                IEcaExecutionConditionContext<TEventContext> context)
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
            : IEcaAction<IEcaExecutionActionContext<TEventContext>>
        {
            private readonly List<TaskCompletionSource<bool>> _gates = new();

            public int RunCount { get; private set; }

            public List<ExecutionRecord> Records { get; } = new();

            public List<EcaRuleExecutionGroupState> ExecutionGroupStates { get; } = new();

            public Task Run(
                IEcaExecutionActionContext<TEventContext> context)
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

            public void FailAll(Exception exception)
            {
                foreach (var gate in _gates) gate.TrySetException(exception);
                _gates.Clear();
            }

            public void CompleteAll()
            {
                for (var i = 0; i < _gates.Count; i++)
                    _gates[i].TrySetResult(true);

                _gates.Clear();
            }
        }

        private sealed class ExecutionThrowingAction<TEventContext>
            : IEcaAction<IEcaExecutionActionContext<TEventContext>>
        {
            public Task Run(
                IEcaExecutionActionContext<TEventContext> context)
            {
                throw new InvalidOperationException(
                    "Intentional smoke-test exception."
                );
            }
        }
    }
}
