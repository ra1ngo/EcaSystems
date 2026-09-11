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
    public sealed class EcaRuleExecutionGroupTests : AsyncTestFixture
    {
        [Test]
        public async Task Fire_Limits_CountOnlyActualStarts()
        {
            var ecaEvent = new EcaEvent<TestEventContext>("test.limit", "Limit");
            var commandRunner = new EcaCommandRunner(new EcaCommandRegistry());
            foreach (var limit in new[] { -1, 0, 1, 2 })
            {
                var action = Track(new ExecutionGateAction<TestEventContext>());
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
                        Assert.That(execution.Status == EcaRuleExecutionStatus.Completed, Is.True, "Successful execution is Completed");
                }
                var expected = limit == -1 ? 4 : limit;
                Assert.That(action.RunCount == expected &&
                    group.State.EcaRuleExecutionTotalStarted == expected &&
                    group.State.EcaRuleExecutionTotalFinished == expected, Is.True, "Limit " + limit + " counts actual starts");
            }

            var gate = Track(new ExecutionGateAction<TestEventContext>());
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
                Assert.That(group.Executions.Count == active && group.State.EcaRuleExecutionTotalStarted == active, Is.True, overlap + ": active Fire respects overlap and limit");
                gate.CompleteAll();
                await WaitUntil(() => group.Executions.Count == 0);
                group.Fire(eventContext, commandRunner);
                Assert.That(group.State.EcaRuleExecutionTotalStarted == 2, Is.True, overlap + ": ignored Fire does not consume limit");
                gate.CompleteAll();
                await WaitUntil(() => group.Executions.Count == 0);
                group.Fire(eventContext, commandRunner);
                Assert.That(group.Executions.Count == 0 &&
                    group.State.EcaRuleExecutionTotalFinished == 2, Is.True, overlap + ": exhausted limit prevents execution");
            }

            var invalid = false;
            try { _ = new EcaRunMode(EcaOverlap.Allow, -2); }
            catch (ArgumentOutOfRangeException) { invalid = true; }
            Assert.That(invalid, Is.True, "Limit below -1 is rejected");

            var checks = 0;
            var blockedAction = Track(new ExecutionGateAction<TestEventContext>());
            var blockedRule = CreateExecutionRule("limit.zero.conditions", ecaEvent, blockedAction,
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ => { checks++; return true; }));
            var engine = new EcaExecutionEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            engine.Register(blockedRule, new EcaRunMode(EcaOverlap.Allow, 0));
            engine.Fire(ecaEvent, new TestEventContext(0));
            Assert.That(checks == 1 && blockedAction.RunCount == 0, Is.True, "Conditions run before zero limit is applied");
        }

        [Test]
        public async Task Run_FaultedAction_RecordsOriginalFailure()
        {
            var ecaEvent = new EcaEvent<TestEventContext>("test.failure.status", "Failure");
            var action = Track(new ExecutionGateAction<TestEventContext>());
            var rule = CreateExecutionRule("failure.status", ecaEvent, action);
            var group = new EcaRuleExecutionGroup<TestEventContext>(rule, new EcaRunMode(EcaOverlap.Ignore), new EcaRuleRunner());
            group.Fire(new TestEventContext(1), new EcaCommandRunner(new EcaCommandRegistry()));
            var execution = group.Executions[0];
            var error = new InvalidOperationException("Expected asynchronous failure.");
            action.FailAll(error);
            await WaitUntil(() => group.Executions.Count == 0);
            Assert.That(execution.Status == EcaRuleExecutionStatus.Failed && ReferenceEquals(execution.Exception, error), Is.True, "Faulted Run records Failed and the original exception");
            Assert.That(group.State.EcaRuleExecutionTotalStarted == 1 &&
                group.State.EcaRuleExecutionTotalFinished == 1, Is.True, "Failure balances counters");

            group.Fire(new TestEventContext(2), new EcaCommandRunner(new EcaCommandRegistry()));
            execution = group.Executions[0];
            var interrupted = new OperationCanceledException("User Action failure.");
            action.FailAll(interrupted);
            await WaitUntil(() => group.Executions.Count == 0);
            Assert.That(execution.Status == EcaRuleExecutionStatus.Failed && ReferenceEquals(execution.Exception, interrupted), Is.True, "OperationCanceledException is an ordinary failure");
        }

        [Test]
        public void Register_ValidatesGroupsAndRollsBackRuleOnFailure()
        {
            var registry = new EcaRuleRegistry();
            var selector = new EcaRuleSelector(registry);
            var groups = new EcaRuleExecutionRegistry();
            var ruleRunner = new EcaRuleRunner();
            var ecaEvent = new EcaEvent<TestEventContext>("validation", "Validation");
            var rule = CreateExecutionRule("validation.rule", ecaEvent, Track(new ExecutionGateAction<TestEventContext>()));
            groups.Register(rule, new EcaRunMode(EcaOverlap.Allow), ruleRunner);
            var original = groups.Get<TestEventContext>(rule.Id);
            groups.Register(rule, new EcaRunMode(EcaOverlap.Allow), ruleRunner);
            Assert.That(ReferenceEquals(original, groups.Get<TestEventContext>(rule.Id)), Is.True, "Equivalent RunMode preserves the group");
            Assert.Catch<InvalidOperationException>(() => groups.Register(rule, new EcaRunMode(EcaOverlap.Allow, 1), ruleRunner), "Registration rejects limit mismatch");
            Assert.Catch<InvalidOperationException>(() => groups.Register(CreateExecutionRule(rule.Id, ecaEvent, Track(new ExecutionGateAction<TestEventContext>())),
                    new EcaRunMode(EcaOverlap.Allow), ruleRunner), "Registration rejects another Rule with same id");
            Assert.Catch<InvalidOperationException>(() => groups.Get<EcaEventContextEmpty>(rule.Id), "Typed Get rejects incompatible group");
            Assert.Catch<InvalidOperationException>(() => groups.TryGet<EcaEventContextEmpty>(rule.Id, out _), "Typed TryGet rejects incompatible group");
            Assert.That(!groups.TryGet<TestEventContext>("missing", out _), Is.True, "TryGet returns false for missing group");
            var engine = new EcaExecutionEngine(registry, selector, new EcaRuleChecker(),
                groups, new EcaCommandRunner(new EcaCommandRegistry()), ruleRunner);
            Assert.Catch<InvalidOperationException>(() => engine.Register(rule, new EcaRunMode(EcaOverlap.Ignore)), "Registration rejects overlap mismatch");
            Assert.That(registry.Rules.Count == 0, Is.True, "Failed group registration rolls back Rule registration");
            engine.Register(rule, new EcaRunMode(EcaOverlap.Allow));
            Assert.That(registry.Rules.Count == 1, Is.True, "Registration can succeed after rollback");
            Assert.Catch<InvalidOperationException>(() => selector.ForEvent<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(ecaEvent), "Selector rejects incompatible full Context");
            var conflictingEvent = new EcaEvent<EcaEventContextEmpty>("validation", "Conflicting event");
            Assert.Catch<InvalidOperationException>(() => selector.ForEvent<EcaEventContextEmpty, EcaConditionContext<EcaEventContextEmpty>, EcaActionContext<EcaEventContextEmpty>>(conflictingEvent), "Selector rejects EventId collision with incompatible payload");
            Assert.That(((ICollection<IEcaRule>)registry.Rules).IsReadOnly, Is.True, "Registry exposes a read-only collection");
        }
    }
}
