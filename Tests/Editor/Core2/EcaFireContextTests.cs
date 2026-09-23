using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaFireContextTests
    {
        private EcaBaseEventRegistry _events;
        private BaseTestSupport.Event<int> _event;
        private EcaScopeRuntime _owner;
        private EcaScope _scope;

        private sealed class Conditions : IEcaConditionContext { }
        private sealed class OtherConditions : IEcaConditionContext { }
        private sealed class Actions : IEcaActionContext { }
        private sealed class OtherActions : IEcaActionContext { }
        private sealed class RichState : IEcaScopeRuleState<int>
        {
            public string RuleId { get; set; } = "rule";
            public int EventState { get; }
            public EcaExecutionGroupState ExecutionGroupState { get; }
            public EcaScopeState ScopeState { get; }
            internal RichState(IEcaExecutionRuleState<int> state, EcaScopeState scope)
            {
                RuleId = state.RuleId;
                EventState = state.EventState;
                ExecutionGroupState = state.ExecutionGroupState;
                ScopeState = scope;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _events = new EcaBaseEventRegistry();
            _event = new BaseTestSupport.Event<int>();
            _events.Register(_event);
            _owner = new EcaScopeRuntime(_events, new EcaBaseConditionChecker(), new EcaBaseActionRunner());
            _scope = _owner.CreateScope("root");
        }

        [TearDown]
        public void TearDown() => _owner.Dispose();

        private Rule<int, R> Rule<R>(string id,
            Func<R, IEcaConditionContext, bool> check,
            Func<R, IEcaActionContext, Task> run) where R : IEcaRuleState<int> =>
            new()
            {
                Id = id, Event = _event,
                Condition = check == null ? null : new Condition<R, IEcaConditionContext> { Handler = check },
                Action = new ExecutionTestSupport.Action<R, IEcaActionContext> { Handler = run }
            };

        private void Fire(bool emitter, IEcaConditionContext c, IEcaActionContext a)
        {
            if (emitter) _scope.EventEmitter.Fire(_event, 7, c, a);
            else _scope.Fire(_event, 7, c, a);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContextInstancesAndTypesMayChangeWithoutChangingRule(bool emitter)
        {
            var checkedContexts = new List<IEcaConditionContext>();
            var actionContexts = new List<IEcaActionContext>();
            IEcaRule<int, EcaScopeRuleState<int>> rule = Rule<EcaScopeRuleState<int>>("rule",
                (s, c) => { checkedContexts.Add(c); return true; },
                (s, a) => { actionContexts.Add(a); return Task.CompletedTask; });
            _scope.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            var c1 = new Conditions(); var c2 = new OtherConditions();
            var a1 = new Actions(); var a2 = new OtherActions();
            Fire(emitter, c1, a1);
            Fire(emitter, c2, a2);
            Assert.That(checkedContexts, Has.Count.EqualTo(2));
            Assert.That(actionContexts, Has.Count.EqualTo(2));
            Assert.That(checkedContexts[0], Is.SameAs(c1));
            Assert.That(checkedContexts[1], Is.SameAs(c2));
            Assert.That(actionContexts[0], Is.SameAs(a1));
            Assert.That(actionContexts[1], Is.SameAs(a2));
            Assert.That(_scope.GetGroup("rule").State.TotalFinished, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OmittedContextsArriveAsNull(bool emitter)
        {
            var checks = 0; var runs = 0;
            _scope.Register(Rule<EcaScopeRuleState<int>>("rule",
                (s, c) => { Assert.That(c, Is.Null); checks++; return true; },
                (s, a) => { Assert.That(a, Is.Null); runs++; return Task.CompletedTask; }),
                new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            if (emitter) _scope.EventEmitter.Fire(_event, 0);
            else _scope.Fire(_event, 0);
            Assert.That(checks, Is.EqualTo(1));
            Assert.That(runs, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ContextsAreIndependentlyOptional(bool conditionOnly)
        {
            var c = conditionOnly ? new Conditions() : null;
            var a = conditionOnly ? null : new Actions();
            IEcaConditionContext seenC = null;
            IEcaActionContext seenA = null;
            var runs = 0;
            _scope.Register(Rule<EcaScopeRuleState<int>>("rule",
                (s, context) => { seenC = context; return true; },
                (s, context) => { seenA = context; runs++; return Task.CompletedTask; }),
                new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            _scope.EventEmitter.Fire(_event, 0, c, a);
            Assert.That(seenC, Is.SameAs(c));
            Assert.That(seenA, Is.SameAs(a));
            Assert.That(runs, Is.EqualTo(1));
        }

        [Test]
        public void DifferentRuleStatesShareOneEventOnlyFireAndConditionBarrier()
        {
            var trace = new List<string>();
            var c = new Conditions(); var a = new Actions();
            var richStates = new List<RichState>();
            _scope.Register(Rule<EcaScopeRuleState<int>>("default",
                (s, context) => { Assert.That(context, Is.SameAs(c)); trace.Add("check default"); return true; },
                (s, context) => { Assert.That(context, Is.SameAs(a)); trace.Add("run default"); return Task.CompletedTask; }),
                new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            _scope.Register(Rule<RichState>("rich",
                (s, context) => { Assert.That(context, Is.SameAs(c)); richStates.Add(s); trace.Add("check rich"); return true; },
                (s, context) => { Assert.That(context, Is.SameAs(a)); richStates.Add(s); trace.Add("run rich"); return Task.CompletedTask; }),
                new EcaExecutionMode(EcaExecutionModeOverlap.Allow), (state, scope) => new RichState(state, scope));
            _scope.EventEmitter.Fire(_event, 19, c, a);
            Assert.That(trace, Is.EqualTo(new[] { "check default", "check rich", "run default", "run rich" }));
            Assert.That(richStates, Has.Count.EqualTo(2));
            Assert.That(richStates[0], Is.Not.SameAs(richStates[1]));
            foreach (var state in richStates)
            {
                Assert.That(state.EventState, Is.EqualTo(19));
                Assert.That(state.ScopeState, Is.SameAs(_scope.State));
                Assert.That(state.ExecutionGroupState, Is.SameAs(_scope.GetGroup("rich").State));
            }
        }

        [Test]
        public void EachScopeOwnsDifferentEmitterAndRoutesOnlyLocally()
        {
            var child = _scope.CreateScope("child");
            var sibling = _owner.CreateScope("sibling");
            var seen = new List<string>();
            var shared = Rule<EcaScopeRuleState<int>>("shared", null,
                (s, a) => { seen.Add(s.ScopeState.ScopeId); return Task.CompletedTask; });
            foreach (var scope in new[] { _scope, child, sibling })
                scope.Register(shared, new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            Assert.That(_scope.EventEmitter, Is.Not.SameAs(child.EventEmitter));
            Assert.That(child.EventEmitter, Is.Not.SameAs(sibling.EventEmitter));
            _scope.EventEmitter.Fire(_event, 0);
            child.EventEmitter.Fire(_event, 0);
            sibling.EventEmitter.Fire(_event, 0);
            Assert.That(seen, Is.EqualTo(new[] { "root", "child", "sibling" }));
            foreach (var scope in new[] { _scope, child, sibling })
                Assert.That(scope.GetGroup("shared").State.TotalStarted, Is.EqualTo(1));
        }

        [Test]
        public void StaleEmitterNeverTargetsReplacementAndKeepsDisposedFailurePriority()
        {
            var oldEmitter = _scope.EventEmitter;
            _scope.Dispose();
            var replacement = _owner.CreateScope("root");
            var calls = 0;
            replacement.Register(Rule<EcaScopeRuleState<int>>("rule", null,
                (s, a) => { calls++; return Task.CompletedTask; }), new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            Assert.That(oldEmitter, Is.Not.SameAs(replacement.EventEmitter));
            var direct = Assert.Throws<ObjectDisposedException>(() => _scope.Fire(_event, 0));
            var emitted = Assert.Throws<ObjectDisposedException>(() => oldEmitter.Fire(_event, 0));
            Assert.That(emitted.ObjectName, Is.EqualTo(direct.ObjectName));
            Assert.Throws<ObjectDisposedException>(() => oldEmitter.Fire<int>(null, 0));
            _events.Unregister(_event.Id);
            Assert.Throws<ObjectDisposedException>(() => oldEmitter.Fire(_event, 0));
            _events.Register(_event);
            Assert.That(calls, Is.Zero);
            replacement.EventEmitter.Fire(_event, 0);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void DirectAndEmitterFireHaveSameLocalOrderContextsAndCounters()
        {
            var trace = new List<string>();
            var c = new Conditions(); var a = new Actions();
            foreach (var id in new[] { "first", "second" })
                _scope.Register(Rule<EcaScopeRuleState<int>>(id,
                    (s, context) => { Assert.That(context, Is.SameAs(c)); trace.Add("check " + id + s.EventState); return true; },
                    (s, context) => { Assert.That(context, Is.SameAs(a)); trace.Add("run " + id + s.EventState); return Task.CompletedTask; }),
                    new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            Fire(false, c, a);
            var direct = trace.ToArray();
            trace.Clear();
            Fire(true, c, a);
            Assert.That(trace, Is.EqualTo(direct));
            Assert.That(trace, Is.EqualTo(new[] { "check first7", "check second7", "run first7", "run second7" }));
            foreach (var id in new[] { "first", "second" })
            {
                Assert.That(_scope.GetGroup(id).State.TotalStarted, Is.EqualTo(2));
                Assert.That(_scope.GetGroup(id).State.TotalFinished, Is.EqualTo(2));
            }
        }

        [TestCase(EcaExecutionModeOverlap.Allow, 2, 2)]
        [TestCase(EcaExecutionModeOverlap.Allow, 1, 1)]
        [TestCase(EcaExecutionModeOverlap.Ignore, 2, 1)]
        public void ReentrantEmitterFireIsImmediateAndHonorsAdmission(EcaExecutionModeOverlap overlap, int limit, int expectedRuns)
        {
            var trace = new List<string>();
            _scope.Register(Rule<EcaScopeRuleState<int>>("rule",
                (s, c) => { trace.Add("check " + s.EventState); return true; },
                (s, a) =>
                {
                    trace.Add("start " + s.EventState);
                    if (s.EventState == 1) _scope.EventEmitter.Fire(_event, 2);
                    trace.Add("end " + s.EventState);
                    return Task.CompletedTask;
                }), new EcaExecutionMode(overlap, limit));
            _scope.EventEmitter.Fire(_event, 1);
            Assert.That(trace, Is.EqualTo(expectedRuns == 2
                ? new[] { "check 1", "start 1", "check 2", "start 2", "end 2", "end 1" }
                : new[] { "check 1", "start 1", "check 2", "end 1" }));
            Assert.That(_scope.GetGroup("rule").State.TotalStarted, Is.EqualTo(expectedRuns));
            Assert.That(_scope.GetGroup("rule").State.TotalFinished, Is.EqualTo(expectedRuns));
        }

        [Test]
        public void NestedFireFromConditionConsumesLimitBeforeOuterRun()
        {
            var values = new List<int>();
            _scope.Register(Rule<EcaScopeRuleState<int>>("rule",
                (s, c) => { if (s.EventState == 1) _scope.EventEmitter.Fire(_event, 2); return true; },
                (s, a) => { values.Add(s.EventState); return Task.CompletedTask; }),
                new EcaExecutionMode(EcaExecutionModeOverlap.Allow, 1));
            _scope.EventEmitter.Fire(_event, 1);
            Assert.That(values, Is.EqualTo(new[] { 2 }));
            Assert.That(_scope.GetGroup("rule").State.TotalStarted, Is.EqualTo(1));
            Assert.That(_scope.GetGroup("rule").State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public async Task RecursiveDisposeClosesEmitterWithoutCancellingRunningActionOrItsContext()
        {
            var child = _scope.CreateScope("child");
            var emitter = child.EventEmitter;
            var gate = new TaskCompletionSource<bool>();
            var context = new Actions();
            IEcaActionContext afterAwait = null;
            child.Register(Rule<EcaScopeRuleState<int>>("rule", null,
                async (s, a) => { await gate.Task; afterAwait = a; }), new EcaExecutionMode(EcaExecutionModeOverlap.Allow));
            emitter.Fire(_event, 0, actionContext: context);
            var group = child.GetGroup("rule");
            var execution = group.Executions[0];
            try
            {
                _scope.Dispose();
                Assert.Throws<ObjectDisposedException>(() => emitter.Fire(_event, 0));
                Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Running));
            }
            finally { gate.TrySetResult(true); }
            await WaitUntil(() => group.Executions.Count == 0);
            Assert.That(afterAwait, Is.SameAs(context));
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Completed));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public void EventOnlyFireAndEmitterValidateLiveCanonicalEventAndMetadata()
        {
            var directNull = Assert.Throws<ArgumentNullException>(() => _scope.Fire<int>(null, 0));
            var emitterNull = Assert.Throws<ArgumentNullException>(() => _scope.EventEmitter.Fire<int>(null, 0));
            Assert.That(emitterNull.ParamName, Is.EqualTo(directNull.ParamName));
            var foreign = new BaseTestSupport.Event<int> { Id = _event.Id };
            Assert.Throws<InvalidOperationException>(() => _scope.Fire(foreign, 0));
            Assert.Throws<InvalidOperationException>(() => _scope.EventEmitter.Fire(foreign, 0));
            _event.EventStateType = typeof(string);
            Assert.Throws<ArgumentException>(() => _scope.Fire(_event, 0));
            Assert.Throws<ArgumentException>(() => _scope.EventEmitter.Fire(_event, 0));
            _event.EventStateType = typeof(int);
            _events.Unregister(_event.Id);
            Assert.Throws<InvalidOperationException>(() => _scope.Fire(_event, 0));
            Assert.Throws<InvalidOperationException>(() => _scope.EventEmitter.Fire(_event, 0));
            _events.Register(_event);
            Assert.DoesNotThrow(() => _scope.EventEmitter.Fire(_event, 0));
        }

        [Test]
        public void ForceFire_BaseCallerStateForwardsNullWithoutCreatingContexts()
        {
            var rules = new EcaBaseRuleRegistry(_events);
            var runtime = new EcaBaseRuntime(rules, new EcaBaseActionRunner(), new EcaBaseConditionChecker());
            var state = new BaseTestSupport.State { RuleId = "base", EventState = 12 };
            var calls = 0;
            runtime.Register(Rule<BaseTestSupport.State>("base",
                (s, c) => { Assert.That(s, Is.SameAs(state)); Assert.That(c, Is.Null); return true; },
                (s, a) => { Assert.That(s, Is.SameAs(state)); Assert.That(a, Is.Null); calls++; return Task.CompletedTask; }));
            runtime.ForceFire<int, BaseTestSupport.State>(_event, 12, (r, e) => state);
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
