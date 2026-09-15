using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaScopeTests : ExecutionTestFixture
    {
        private sealed class ScopeC : IEcaScopeConditionContext { public int Value => 11; }
        private sealed class ScopeA : IEcaScopeActionContext { public int Value => 22; }
        private sealed class RichState : IEcaScopeRuleState<int>
        {
            public int EventState { get; }
            public EcaExecutionGroupState ExecutionGroupState { get; }
            public EcaScopeState ScopeState { get; }
            public string Extra { get; }

            public RichState(IEcaExecutionRuleState<int> state, EcaScopeState scope, string extra)
            {
                EventState = state.EventState;
                ExecutionGroupState = state.ExecutionGroupState;
                ScopeState = scope;
                Extra = extra;
            }
        }

        private EcaScopeRuntime _owner;
        private EcaScope _scope;
        private readonly ScopeC _condition = new();
        private readonly ScopeA _action = new();
        private EcaExecutionMode Allow => new(EcaExecutionModeOverlap.Allow);

        [SetUp]
        public void SetUpScope()
        {
            _owner = new EcaScopeRuntime(Events);
            _scope = _owner.CreateScope("A");
        }

        [TearDown]
        public void DisposeScopes() => _owner.Dispose();

        private Rule<int, R, ScopeC, ScopeA> MakeRule<R>(string id = "rule",
            Func<R, ScopeC, bool> check = null, Func<R, ScopeA, Task> run = null) where R : IEcaScopeRuleState<int>
        {
            return new Rule<int, R, ScopeC, ScopeA>
            {
                Id = id, Event = Event,
                Condition = check == null ? null : new Condition<R, ScopeC> { Handler = check },
                Action = new ExecutionTestSupport.Action<R, ScopeA> { Handler = run ?? ((s, c) => Task.CompletedTask) }
            };
        }

        private void Fire(int value = 42) => _scope.Fire<int, EcaScopeRuleState<int>, ScopeC, ScopeA>(
            Event, value, _condition, _action);

        [Test]
        public void ScopeState_PreservesId() => Assert.That(new EcaScopeState("scope.A").ScopeId, Is.EqualTo("scope.A"));

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void ScopeState_RejectsMissingId(string id)
        {
            Assert.Throws<ArgumentException>(() => new EcaScopeState(id));
        }

        [Test]
        public void RuleState_PreservesReferencesAndVerticalContracts()
        {
            var payload = new object();
            var group = new EcaExecutionGroupState();
            var state = new EcaScopeRuleState<object>(payload, group, _scope.State);
            IEcaExecutionRuleState<object> execution = state;
            IEcaRuleState<object> basic = state;
            Assert.That(basic.EventState, Is.SameAs(payload));
            Assert.That(execution.ExecutionGroupState, Is.SameAs(group));
            Assert.That(state.ScopeState, Is.SameAs(_scope.State));
            IEcaScopeRuleState<object> covariant = new EcaScopeRuleState<string>("text", group, _scope.State);
            Assert.That(covariant.EventState, Is.EqualTo("text"));
            Assert.Throws<ArgumentNullException>(() => new EcaScopeRuleState<int>(0, null, _scope.State));
            Assert.Throws<ArgumentNullException>(() => new EcaScopeRuleState<int>(0, group, null));
        }

        [Test]
        public void Constructor_RejectsMissingDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaScopeRuntime(null));
        }

        [Test]
        public void Register_DefaultStateUsesExistingExecutionGroupAndDuplicateSemantics()
        {
            var rule = MakeRule<EcaScopeRuleState<int>>();
            _scope.Register(rule, Allow);
            var group = _scope.GetGroup(rule.Id);
            Assert.That(group, Is.TypeOf<EcaExecutionGroup<int, EcaScopeRuleState<int>, ScopeC, ScopeA>>());
            Assert.That(_owner.TryGetScope("A", out var scope), Is.True);
            Assert.That(scope, Is.SameAs(_scope));
            Assert.That(group.Rule, Is.SameAs(rule));
            Assert.That(_scope.TryGetGroup(rule.Id, out var found), Is.True);
            Assert.That(found, Is.SameAs(group));
            Assert.That(_scope.TryGetGroup("missing", out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => _scope.GetGroup("missing"));
            Assert.Throws<InvalidOperationException>(() => _scope.Register(rule, Allow));
            Assert.Throws<InvalidOperationException>(() => _scope.Register(MakeRule<EcaScopeRuleState<int>>(), Allow));
            Assert.That(group.State.TotalStarted, Is.Zero);
            Fire();
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public void Register_RejectsNullExtensionBeforeRegistration()
        {
            Assert.Throws<ArgumentNullException>(() => _scope.Register(MakeRule<RichState>(), Allow,
                (Func<IEcaExecutionRuleState<int>, EcaScopeState, RichState>)null));
            _scope.Fire<int, RichState, ScopeC, ScopeA>(Event, 1, _condition, _action);
            Assert.That(_scope.TryGetGroup("rule", out _), Is.False);
        }

        [Test]
        public void Register_InvalidModeRetainsExecutionRollback()
        {
            Assert.Throws<ArgumentNullException>(() => _scope.Register(MakeRule<EcaScopeRuleState<int>>(), null));
            Fire();
            Assert.That(_scope.TryGetGroup("rule", out _), Is.False);
        }

        [Test]
        public void Extension_ReceivesFreshExecutionStatesAndForwardsRichStateAndContexts()
        {
            var input = new List<IEcaExecutionRuleState<int>>();
            var scopes = new List<EcaScopeState>();
            var output = new List<RichState>();
            var counters = new List<long>();
            RichState checkedState = null, actionState = null;
            ScopeC checkedContext = null;
            ScopeA actionContext = null;
            var rule = MakeRule<RichState>(check: (s, c) => { checkedState = s; checkedContext = c; return true; },
                run: (s, c) => { actionState = s; actionContext = c; return Task.CompletedTask; });
            _scope.Register(rule, Allow, (executionState, scopeState) =>
            {
                input.Add(executionState);
                scopes.Add(scopeState);
                counters.Add(executionState.ExecutionGroupState.TotalStarted);
                var enriched = new RichState(executionState, scopeState, "custom");
                output.Add(enriched);
                return enriched;
            });
            _scope.Fire<int, RichState, ScopeC, ScopeA>(Event, 17, _condition, _action);
            Assert.That(input, Has.Count.EqualTo(2));
            Assert.That(input[0], Is.TypeOf<EcaExecutionRuleState<int>>());
            Assert.That(input[0], Is.Not.SameAs(input[1]));
            Assert.That(input[0].EventState, Is.EqualTo(17));
            Assert.That(input[1].EventState, Is.EqualTo(17));
            Assert.That(input[0].ExecutionGroupState, Is.SameAs(_scope.GetGroup("rule").State));
            Assert.That(input[1].ExecutionGroupState, Is.SameAs(input[0].ExecutionGroupState));
            Assert.That(scopes, Is.EqualTo(new[] { _scope.State, _scope.State }));
            Assert.That(counters, Is.EqualTo(new long[] { 0, 1 }));
            Assert.That(checkedState, Is.SameAs(output[0]));
            Assert.That(actionState, Is.SameAs(output[1]));
            Assert.That(actionState, Is.Not.SameAs(checkedState));
            Assert.That(actionState.Extra, Is.EqualTo("custom"));
            Assert.That(actionState.ScopeState, Is.SameAs(_scope.State));
            Assert.That(checkedContext, Is.SameAs(_condition));
            Assert.That(actionContext, Is.SameAs(_action));
            Assert.That(checkedContext.Value, Is.EqualTo(11));
            Assert.That(actionContext.Value, Is.EqualTo(22));
            Assert.That(_scope.GetGroup("rule").State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public void Fire_DefaultStatesShareScopeAcrossRulesAndKeepExecutionBarrier()
        {
            var trace = new List<string>();
            var states = new List<EcaScopeRuleState<int>>();
            foreach (var id in new[] { "A", "B", "C" })
            {
                _scope.Register(MakeRule<EcaScopeRuleState<int>>(id,
                    (s, c) => { trace.Add("Condition " + id); states.Add(s); return id != "B"; },
                    (s, c) => { trace.Add("Action " + id); states.Add(s); return Task.CompletedTask; }), Allow);
            }
            Fire();
            Assert.That(trace, Is.EqualTo(new[] { "Condition A", "Condition B", "Condition C", "Action A", "Action C" }));
            foreach (var state in states)
            {
                Assert.That(state.EventState, Is.EqualTo(42));
                Assert.That(state.ScopeState, Is.SameAs(_scope.State));
            }
            Assert.That(states[0], Is.Not.SameAs(states[3]));
            Assert.That(states[0].ExecutionGroupState, Is.SameAs(states[3].ExecutionGroupState));
            Assert.That(states[0].ExecutionGroupState, Is.Not.SameAs(states[2].ExecutionGroupState));
            Assert.That(_scope.GetGroup("B").State.TotalStarted, Is.Zero);
        }

        [TestCase(EcaExecutionModeOverlap.Ignore, -1, 1, 2)]
        [TestCase(EcaExecutionModeOverlap.Allow, -1, 2, 3)]
        [TestCase(EcaExecutionModeOverlap.Allow, 1, 1, 1)]
        [TestCase(EcaExecutionModeOverlap.Allow, 0, 0, 0)]
        public async Task Fire_DelegatesOverlapAndLifetimeLimit(
            EcaExecutionModeOverlap overlap, int limit, int active, int total)
        {
            var gate = NewGate();
            _scope.Register(MakeRule<EcaScopeRuleState<int>>(run: (s, c) => gate.Task), new EcaExecutionMode(overlap, limit));
            Fire();
            Fire();
            var group = _scope.GetGroup("rule");
            Assert.That(group.Executions, Has.Count.EqualTo(active));
            Assert.That(group.State.TotalStarted, Is.EqualTo(active));
            gate.Complete();
            await WaitUntil(() => group.Executions.Count == 0);
            Fire();
            Assert.That(group.State.TotalStarted, Is.EqualTo(total));
            Assert.That(group.State.TotalFinished, Is.EqualTo(total));
        }

        [Test]
        public async Task Isolation_SharedEventAndRuleHaveIndependentGroupsAndLimits()
        {
            var other = _owner.CreateScope("B");
            var seen = new List<EcaScopeState>();
            var gate = NewGate();
            var rule = MakeRule<EcaScopeRuleState<int>>(run: (s, c) => { seen.Add(s.ScopeState); return gate.Task; });
            var mode = new EcaExecutionMode(EcaExecutionModeOverlap.Ignore, 1);
            _scope.Register(rule, mode);
            other.Register(rule, mode);
            Fire();
            Assert.That(seen, Is.EqualTo(new[] { _scope.State }));
            Assert.That(other.GetGroup("rule").State.TotalStarted, Is.Zero);
            other.Fire<int, EcaScopeRuleState<int>, ScopeC, ScopeA>(Event, 1, _condition, _action);
            Assert.That(seen, Is.EqualTo(new[] { _scope.State, other.State }));
            Assert.That(_scope.GetGroup("rule"), Is.Not.SameAs(other.GetGroup("rule")));
            Assert.That(_scope.GetGroup("rule").State, Is.Not.SameAs(other.GetGroup("rule").State));
            Assert.That(_scope.GetGroup("rule").Executions, Has.Count.EqualTo(1));
            Assert.That(other.GetGroup("rule").Executions, Has.Count.EqualTo(1));
            gate.Complete();
            await WaitUntil(() => other.GetGroup("rule").State.TotalFinished == 1 && _scope.GetGroup("rule").State.TotalFinished == 1);
        }

        [Test]
        public void ReentrantFire_PreservesScopeAndExecutionAdmission()
        {
            var seen = new List<EcaScopeState>();
            var payloads = new List<int>();
            _scope.Register(MakeRule<EcaScopeRuleState<int>>(run: (s, c) =>
            {
                seen.Add(s.ScopeState);
                payloads.Add(s.EventState);
                if (s.EventState < 3) Fire(s.EventState + 1);
                return Task.CompletedTask;
            }), new EcaExecutionMode(EcaExecutionModeOverlap.Allow, 2));
            Fire(1);
            Assert.That(payloads, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(seen, Is.EqualTo(new[] { _scope.State, _scope.State }));
            Assert.That(_scope.GetGroup("rule").State.TotalStarted, Is.EqualTo(2));
            Assert.That(_scope.GetGroup("rule").State.TotalFinished, Is.EqualTo(2));
        }

        [Test]
        public async Task Unregister_LeavesActiveExecutionWithOriginalScopeState()
        {
            var gate = NewGate();
            EcaScopeRuleState<int> seen = null;
            EcaScopeState afterAwait = null;
            var rule = MakeRule<EcaScopeRuleState<int>>(run: async (s, c) =>
            {
                seen = s;
                await gate.Task;
                afterAwait = s.ScopeState;
            });
            _scope.Register(rule, Allow);
            Fire();
            var group = _scope.GetGroup("rule");
            var execution = group.Executions[0];
            Assert.That(_scope.Unregister(MakeRule<EcaScopeRuleState<int>>()), Is.False);
            Assert.That(_scope.GetGroup("rule"), Is.SameAs(group));
            Assert.That(_scope.Unregister(rule), Is.True);
            Assert.That(_scope.Unregister(rule), Is.False);
            Assert.That(_scope.TryGetGroup("rule", out _), Is.False);
            Fire();
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Running));
            gate.Complete();
            await WaitUntil(() => group.Executions.Count == 0);
            Assert.That(seen.ScopeState, Is.SameAs(_scope.State));
            Assert.That(afterAwait, Is.SameAs(_scope.State));
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Completed));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExtensionFailure_PreservesConditionAndActionErrorSemantics(bool conditionFailure)
        {
            var error = new InvalidOperationException("extension");
            EcaExecution failed = null;
            var rule = MakeRule<RichState>(check: conditionFailure ? (Func<RichState, ScopeC, bool>)((s, c) => true) : null);
            _scope.Register(rule, Allow, (previous, scope) =>
            {
                if (!conditionFailure) failed = _scope.GetGroup("rule").Executions[0];
                throw error;
            });
            if (conditionFailure)
                Assert.That(Assert.Throws<InvalidOperationException>(() =>
                    _scope.Fire<int, RichState, ScopeC, ScopeA>(Event, 1, _condition, _action)), Is.SameAs(error));
            else
            {
                _scope.Fire<int, RichState, ScopeC, ScopeA>(Event, 1, _condition, _action);
                Assert.That(failed.Status, Is.EqualTo(EcaExecutionStatus.Failed));
                Assert.That(failed.Exception, Is.SameAs(error));
            }
            Assert.That(_scope.GetGroup("rule").State.TotalFinished, Is.EqualTo(conditionFailure ? 0 : 1));
        }

        [Test]
        public void Fire_UsesCallerStateAndBaseBarrierWithoutScopeExtensionOrExecution()
        {
            var trace = new List<string>();
            var seen = new List<EcaScopeRuleState<int>>();
            var callerScope = new EcaScopeState("caller");
            var callerGroup = new EcaExecutionGroupState();
            foreach (var id in new[] { "A", "B" })
            {
                _scope.Register(MakeRule<EcaScopeRuleState<int>>(id,
                    (s, c) => { trace.Add("Condition " + id); seen.Add(s); return id == "A"; },
                    (s, c) => { trace.Add("Action " + id); seen.Add(s); return Task.CompletedTask; }),
                    new EcaExecutionMode(EcaExecutionModeOverlap.Ignore, 0),
                    (previous, scope) => throw new Exception("Must use caller createState"));
            }
            _scope.Fire<int, EcaScopeRuleState<int>, ScopeC, ScopeA>(Event, 17,
                (rule, value) => new EcaScopeRuleState<int>(value, callerGroup, callerScope), _condition, _action);
            Assert.That(trace, Is.EqualTo(new[] { "Condition A", "Condition B", "Action A" }));
            Assert.That(seen[0], Is.SameAs(seen[2]));
            foreach (var state in seen)
            {
                Assert.That(state.EventState, Is.EqualTo(17));
                Assert.That(state.ScopeState, Is.SameAs(callerScope));
                Assert.That(state.ExecutionGroupState, Is.SameAs(callerGroup));
            }
            foreach (var id in new[] { "A", "B" })
            {
                Assert.That(_scope.GetGroup(id).Executions, Is.Empty);
                Assert.That(_scope.GetGroup(id).State.TotalStarted, Is.Zero);
                Assert.That(_scope.GetGroup(id).State.TotalFinished, Is.Zero);
            }
        }
    }
}
