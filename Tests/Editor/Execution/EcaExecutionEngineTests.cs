using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;
using NUnit.Framework;
using EcaSystems.Tests.Support;
using static EcaSystems.Tests.Support.ExecutionRules;
using static EcaSystems.Tests.Support.AsyncAssert;

namespace EcaSystems.Tests
{
    [TestFixture]
    public sealed class EcaExecutionEngineTests : AsyncTestFixture
    {
        [Test]
        public async Task Fire_OverlapIgnore_TracksOnlyAcceptedExecutions()
        {


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
                Track(new ExecutionGateAction<TestEventContext>());

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

            Assert.That(condition.Records.Count == 1 &&
                condition.Records[0].Started == 0 &&
                condition.Records[0].Finished == 0, Is.True, "Ignore: first Condition sees 0 / 0");

            Assert.That(action.RunCount == 1, Is.True, "Ignore: first Action starts");

            Assert.That(action.Records[0].Started == 1 &&
                action.Records[0].Finished == 0, Is.True, "Ignore: Action sees 1 / 0");

            Assert.That(group.Executions.Count == 1 &&
                group.Executions[0].Status ==
                EcaRuleExecutionStatus.Running, Is.True, "Ignore: one Running Execution exists");

            Assert.That(group.State.EcaRuleExecutionTotalStarted == 1 &&
                group.State.EcaRuleExecutionTotalFinished == 0, Is.True, "Ignore: live ExecutionGroupState is 1 / 0");

            // Fire #2 while #1 is still running.
            engine.Fire(
                testEvent,
                new TestEventContext(2)
            );

            Assert.That(condition.Records.Count == 2, Is.True, "Ignore: Condition still runs on second Fire");

            Assert.That(condition.Records[1].Started == 1 &&
                condition.Records[1].Finished == 0, Is.True, "Ignore: second Condition sees 1 / 0");

            Assert.That(action.RunCount == 1, Is.True, "Ignore: second Action does not start");

            Assert.That(group.Executions.Count == 1, Is.True, "Ignore: still one Execution");

            // Complete execution #1.
            action.CompleteAll();

            await WaitUntil(
                () =>
                    group.State
                        .EcaRuleExecutionTotalFinished == 1
            );

            Assert.That(group.State.EcaRuleExecutionTotalStarted == 1 &&
                group.State.EcaRuleExecutionTotalFinished == 1, Is.True, "Ignore: after completion state is 1 / 1");

            Assert.That(group.Executions.Count == 0, Is.True, "Ignore: completed Execution removed");

            // Fire #3 after previous execution finished.
            engine.Fire(
                testEvent,
                new TestEventContext(3)
            );

            Assert.That(condition.Records.Count == 3 &&
                condition.Records[2].Started == 1 &&
                condition.Records[2].Finished == 1, Is.True, "Ignore: third Condition sees previous 1 / 1");

            Assert.That(action.RunCount == 2, Is.True, "Ignore: Rule starts again");

            Assert.That(action.Records[1].Started == 2 &&
                action.Records[1].Finished == 1, Is.True, "Ignore: second Action sees 2 / 1");

            action.CompleteAll();

            await WaitUntil(
                () =>
                    group.State
                        .EcaRuleExecutionTotalFinished == 2
            );

            Assert.That(group.State.EcaRuleExecutionTotalStarted == 2 &&
                group.State.EcaRuleExecutionTotalFinished == 2, Is.True, "Ignore: final state is 2 / 2");
        }

        [Test]
        public async Task Fire_OverlapAllow_SharesLiveStateAcrossConcurrentExecutions()
        {


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
                Track(new ExecutionGateAction<TestEventContext>());

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

            Assert.That(action.RunCount == 2, Is.True, "Allow: both Actions start");

            Assert.That(group.Executions.Count == 2, Is.True, "Allow: two Executions exist simultaneously");

            Assert.That(group.State.EcaRuleExecutionTotalStarted == 2 &&
                group.State.EcaRuleExecutionTotalFinished == 0, Is.True, "Allow: ExecutionGroupState is 2 / 0");

            Assert.That(action.Records[0].Started == 1 &&
                action.Records[0].Finished == 0, Is.True, "Allow: first Action initially saw 1 / 0");

            Assert.That(action.Records[1].Started == 2 &&
                action.Records[1].Finished == 0, Is.True, "Allow: second Action initially saw 2 / 0");

            // The first Action stores a reference to the same live state.
            Assert.That(action.ExecutionGroupStates[0]
                    .EcaRuleExecutionTotalStarted == 2, Is.True, "Allow: ExecutionGroupState passed to Action is live");

            action.CompleteAll();

            await WaitUntil(
                () =>
                    group.State
                        .EcaRuleExecutionTotalFinished == 2
            );

            Assert.That(group.Executions.Count == 0, Is.True, "Allow: all Executions removed after completion");

            Assert.That(group.State.EcaRuleExecutionTotalStarted == 2 &&
                group.State.EcaRuleExecutionTotalFinished == 2, Is.True, "Allow: final state is 2 / 2");
        }

        [Test]
        public async Task Fire_ThrowingAction_BalancesCountersAndRemovesExecution()
        {


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

            Assert.That(group.State.EcaRuleExecutionTotalStarted == 1, Is.True, "Failed Action still increments Started");

            Assert.That(group.State.EcaRuleExecutionTotalFinished == 1, Is.True, "Failed Action increments Finished");

            Assert.That(group.Executions.Count == 0, Is.True, "Failed Execution is removed from ExecutionGroup");
        }

        [Test]
        public async Task Fire_ChecksAllConditionsBeforeAnyAction()
        {
            var registry = new EcaRuleRegistry();
            var groups = new EcaRuleExecutionRegistry();
            var engine = new EcaExecutionEngine(registry, new EcaRuleSelector(registry),
                new EcaRuleChecker(), groups, new EcaCommandRunner(new EcaCommandRegistry()), new EcaRuleRunner());
            var ecaEvent = new EcaEvent<TestEventContext>("test.execution.order", "Order");
            var first = Track(new ExecutionGateAction<TestEventContext>());
            var second = Track(new ExecutionGateAction<TestEventContext>());
            var rejected = Track(new ExecutionGateAction<TestEventContext>());
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
            Assert.That(first.RunCount == 1 && second.RunCount == 1, Is.True, "Execution: all Conditions precede Actions");
            Assert.That(rejected.RunCount == 0 && groups.Get<TestEventContext>(rejectedRule.Id).Executions.Count == 0, Is.True, "Execution: false Condition creates no execution");
            Assert.That(!ReferenceEquals(firstGroup.State, secondGroup.State), Is.True, "Execution: groups have separate live state");
            Assert.That(ReferenceEquals(firstGroup.Executions[0].Rule, firstRule) &&
                ReferenceEquals(firstGroup.Executions[0].Context.RuleExecutionGroupState, firstGroup.State) &&
                firstGroup.Executions[0].Context.EventContext.Value == 42, Is.True, "Execution retains the exact Rule and Context");
            first.CompleteAll();
            second.CompleteAll();
            await WaitUntil(() => firstGroup.Executions.Count == 0 && secondGroup.Executions.Count == 0);
        }

        [Test]
        public async Task Unregister_RunningAction_SurvivesIndependentReplacement()
        {
            var rules = new EcaRuleRegistry();
            var groups = new EcaRuleExecutionRegistry();
            var engine = new EcaExecutionEngine(rules, new EcaRuleSelector(rules),
                new EcaRuleChecker(), groups, new EcaCommandRunner(new EcaCommandRegistry()), new EcaRuleRunner());
            var ecaEvent = new EcaEvent<TestEventContext>("test.unregister", "Unregister");
            var oldAction = Track(new ExecutionGateAction<TestEventContext>());
            var oldRule = CreateExecutionRule("unregister.rule", ecaEvent, oldAction);
            var mode = new EcaRunMode(EcaOverlap.Ignore, 1);
            engine.Register(oldRule, mode);
            var oldGroup = groups.Get<TestEventContext>(oldRule.Id);
            engine.Fire(ecaEvent, new TestEventContext(1));
            var oldExecution = oldGroup.Executions[0];

            Assert.That(engine.Unregister(oldRule), Is.True, "Execution unregister succeeds");
            Assert.That(rules.Rules.Count == 0 &&
                !groups.TryGet<TestEventContext>(oldRule.Id, out _), Is.True, "Unregister removes Rule and Group immediately");
            Assert.That(oldGroup.Executions.Count == 1 &&
                oldExecution.Status == EcaRuleExecutionStatus.Running &&
                oldGroup.State.EcaRuleExecutionTotalFinished == 0, Is.True, "Unregister leaves running Action unfinished");
            engine.Fire(ecaEvent, new TestEventContext(2));
            Assert.That(oldAction.RunCount == 1, Is.True, "Unregistered Rule receives no new Fire");
            Assert.That(!engine.Unregister(oldRule), Is.True, "Repeated unregister returns false");

            var newAction = Track(new ExecutionGateAction<TestEventContext>());
            var newRule = CreateExecutionRule(oldRule.Id, ecaEvent, newAction);
            engine.Register(newRule, mode);
            var newGroup = groups.Get<TestEventContext>(newRule.Id);
            Assert.That(!ReferenceEquals(oldGroup, newGroup) && !ReferenceEquals(oldGroup.State, newGroup.State) &&
                newGroup.State.EcaRuleExecutionTotalStarted == 0 &&
                newGroup.State.EcaRuleExecutionTotalFinished == 0, Is.True, "Same RuleId immediately gets an independent Group and State");
            Assert.That(!engine.Unregister(oldRule) &&
                ReferenceEquals(newGroup, groups.Get<TestEventContext>(newRule.Id)), Is.True, "Unregister of old Rule cannot remove replacement");
            engine.Fire(ecaEvent, new TestEventContext(3));
            var newExecution = newGroup.Executions[0];
            Assert.That(newAction.RunCount == 1 && oldExecution.Status == EcaRuleExecutionStatus.Running &&
                newExecution.Status == EcaRuleExecutionStatus.Running, Is.True, "New Limit lifetime permits a run while old Action is still running");

            oldAction.CompleteAll();
            await WaitUntil(() => oldGroup.Executions.Count == 0);
            Assert.That(oldExecution.Status == EcaRuleExecutionStatus.Completed &&
                oldGroup.State.EcaRuleExecutionTotalStarted == 1 &&
                oldGroup.State.EcaRuleExecutionTotalFinished == 1, Is.True, "Old Action completes naturally after unregister");
            Assert.That(ReferenceEquals(newGroup, groups.Get<TestEventContext>(newRule.Id)) &&
                newGroup.Executions.Count == 1 && newGroup.State.EcaRuleExecutionTotalStarted == 1 &&
                newGroup.State.EcaRuleExecutionTotalFinished == 0, Is.True, "Old completion leaves new Group registered and counters independent");
            newAction.CompleteAll();
            await WaitUntil(() => newGroup.Executions.Count == 0);
            engine.Fire(ecaEvent, new TestEventContext(4));
            Assert.That(newAction.RunCount == 1 &&
                newExecution.Status == EcaRuleExecutionStatus.Completed &&
                newGroup.State.EcaRuleExecutionTotalFinished == 1, Is.True, "Replacement obeys its own Limit after completion");
            Assert.That(engine.Unregister(newRule) &&
                !groups.TryGet<TestEventContext>(newRule.Id, out _), Is.True, "Idle Group is removed on unregister");
            engine.Register(newRule, mode);
            Assert.That(!ReferenceEquals(newGroup, groups.Get<TestEventContext>(newRule.Id)) &&
                groups.Get<TestEventContext>(newRule.Id).State.EcaRuleExecutionTotalStarted == 0, Is.True, "The same Rule instance can be registered with fresh state");
            engine.Unregister(newRule);

            var nullRejected = false;
            try { engine.Unregister<TestEventContext>(null); }
            catch (ArgumentNullException) { nullRejected = true; }
            Assert.That(nullRejected, Is.True, "Execution unregister rejects null");
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
    }
}
