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
    public sealed class EcaScopeExecutionTests : AsyncTestFixture
    {
        [Test]
        public async Task Fire_SharedRule_KeepsScopeStateLimitsAndRegistriesIndependent()
        {
            foreach (var overlap in new[] { EcaOverlap.Ignore, EcaOverlap.Allow })
            {
                using var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
                var a = engine.CreateScope();
                var b = engine.CreateScope();
                var evt = new EcaEvent<TestEventContext>("scope.shared", "Shared");
                var action = Track(new ExecutionGateAction<TestEventContext>());
                var rule = CreateExecutionRule("scope.shared.rule", evt, action);
                a.Register(rule, new EcaRunMode(overlap, 1));
                b.Register(rule, new EcaRunMode(overlap, 1));
                a.Fire(evt, new TestEventContext(1));
                a.Fire(evt, new TestEventContext(2));
                Assert.That(action.RunCount == 1, Is.True, overlap + ": Fire A is local and Limit is one");
                b.Fire(evt, new TestEventContext(3));
                Assert.That(action.RunCount == 2 &&
                    !ReferenceEquals(action.ExecutionGroupStates[0], action.ExecutionGroupStates[1]) &&
                    action.Records[0].Started == 1 && action.Records[1].Started == 1, Is.True, overlap + ": same Rule has independent state and Limit in B");
                action.CompleteAll();
                await WaitUntil(() => action.ExecutionGroupStates[1].EcaRuleExecutionTotalFinished == 1);
                a.Fire(evt, new TestEventContext(4));
                b.Fire(evt, new TestEventContext(5));
                Assert.That(action.RunCount == 2, Is.True, overlap + ": both limits remain exhausted");
                Assert.That(a.Unregister(rule) && !a.Unregister(rule), Is.True, "Unregister A is independent");
                a.Register(rule, new EcaRunMode(overlap));
                a.Fire(evt, new TestEventContext(6));
                b.Fire(evt, new TestEventContext(7));
                Assert.That(action.RunCount == 3, Is.True, "Re-register A resets only its lifetime");
                action.CompleteAll();
                await WaitUntil(() => action.ExecutionGroupStates[2].EcaRuleExecutionTotalFinished == 1);
                b.Unregister(rule);
                b.Register(rule, new EcaRunMode(overlap));
                a.Fire(evt, new TestEventContext(8));
                b.Fire(evt, new TestEventContext(9));
                a.Fire(evt, new TestEventContext(10));
                b.Fire(evt, new TestEventContext(11));
                var expected = overlap == EcaOverlap.Ignore ? 5 : 7;
                Assert.That(action.RunCount == expected, Is.True, overlap + ": active overlap is independent across scopes");
                action.CompleteAll();
                await WaitUntil(() => action.ExecutionGroupStates[expected - 1].EcaRuleExecutionTotalFinished ==
                    (overlap == EcaOverlap.Ignore ? 1 : 2));
            }
        }

        [Test]
        public async Task Fire_RemainsLocalToParentOrChild()
        {
            using var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            var parent = engine.CreateScope();
            var child = parent.CreateScope();
            var evt = new EcaEvent<EcaEventContextEmpty>("scope.local", "Local");
            var parentAction = Track(new ExecutionGateAction<EcaEventContextEmpty>());
            var childAction = Track(new ExecutionGateAction<EcaEventContextEmpty>());
            parent.Register(CreateExecutionRule("scope.parent", evt, parentAction), new EcaRunMode(EcaOverlap.Allow));
            child.Register(CreateExecutionRule("scope.child", evt, childAction), new EcaRunMode(EcaOverlap.Allow));
            child.Fire(evt);
            Assert.That(childAction.RunCount == 1 && parentAction.RunCount == 0, Is.True, "Child Fire does not reach parent");
            parent.Fire(evt);
            Assert.That(childAction.RunCount == 1 && parentAction.RunCount == 1, Is.True, "Parent Fire does not reach child");
            parentAction.CompleteAll();
            childAction.CompleteAll();
            await WaitUntil(() => parentAction.ExecutionGroupStates[0].EcaRuleExecutionTotalFinished == 1 &&
                childAction.ExecutionGroupStates[0].EcaRuleExecutionTotalFinished == 1);
        }

        [Test]
        public async Task Dispose_RunningAction_CompletesWithoutAffectingReplacement()
        {
            using var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            var parent = engine.CreateScope();
            var scope = parent.CreateScope("running");
            var evt = new EcaEvent<TestEventContext>("scope.running", "Running");
            var action = Track(new ExecutionGateAction<TestEventContext>());
            var rule = CreateExecutionRule("scope.running.rule", evt, action);
            scope.Register(rule, new EcaRunMode(EcaOverlap.Ignore, 1));
            scope.Fire(evt, new TestEventContext(0));
            var oldState = action.ExecutionGroupStates[0];
            parent.Dispose();
            Assert.That(scope.IsDisposed &&
                engine.ScopeCount == 0 && !engine.TryGetScope("running", out _) &&
                oldState.EcaRuleExecutionTotalFinished == 0, Is.True, "Dispose immediately removes running scope without waiting");
            var replacement = engine.CreateScope("running");
            replacement.Register(rule, new EcaRunMode(EcaOverlap.Ignore, 1));
            action.CompleteAll();
            await WaitUntil(() => oldState.EcaRuleExecutionTotalFinished == 1);
            replacement.Fire(evt, new TestEventContext(1));
            Assert.That(action.RunCount == 2 &&
                engine.TryGetScope("running", out var found) && ReferenceEquals(found, replacement) &&
                !ReferenceEquals(oldState, action.ExecutionGroupStates[1]) &&
                action.ExecutionGroupStates[1].EcaRuleExecutionTotalFinished == 0, Is.True, "Old Action completion preserves replacement and its Limit");
            engine.Dispose();
            action.CompleteAll();
            await WaitUntil(() => action.ExecutionGroupStates[1].EcaRuleExecutionTotalFinished == 1);
            Assert.That(replacement.IsDisposed && engine.ScopeCount == 0, Is.True, "Action finishes naturally after engine Dispose");
        }
    }
}
