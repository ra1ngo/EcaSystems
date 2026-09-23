using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaStateTests
    {
        private sealed class ExternalState : IDisposable
        {
            public int Reads;
            public bool Disposed;
            public void Dispose() => Disposed = true;
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void StateIdsAreRequiredForEveryOperation(string id)
        {
            var registry = new EcaStateRegistry();
            var resolver = new EcaStateResolver(registry);
            Assert.Throws<ArgumentException>(() => registry.Register<object>(id, _ => new object()));
            Assert.Throws<ArgumentException>(() => registry.Contains(id));
            Assert.Throws<ArgumentException>(() => registry.Unregister(id));
            Assert.Throws<ArgumentException>(() => resolver.Resolve<object>(id, new BaseTestSupport.State()));
        }

        [Test]
        public void StateIdentityIsIdAndDeclaredTypeIsAnExactContract()
        {
            var registry = new EcaStateRegistry();
            var resolver = new EcaStateResolver(registry);
            var first = new ExternalState();
            var second = new ExternalState();
            var calls = 0;
            registry.Register<IDisposable>("world.variables", _ => { calls++; return first; });
            registry.Register<IDisposable>("player.variables", _ => second);
            registry.Register<int>("number", _ => 42);
            Assert.Throws<InvalidOperationException>(() => registry.Register<int>("world.variables", _ => 0));
            var state = new BaseTestSupport.State();
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<ExternalState>("world.variables", state));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<object>("world.variables", state));
            Assert.That(calls, Is.Zero);
            Assert.That(resolver.Resolve<IDisposable>("world.variables", state), Is.SameAs(first));
            Assert.That(resolver.Resolve<IDisposable>("player.variables", state), Is.SameAs(second));
            Assert.That(resolver.Resolve<int>("number", state), Is.EqualTo(42));
            Assert.That(registry.Unregister("world.variables"), Is.True);
            Assert.That(resolver.Resolve<IDisposable>("player.variables", state), Is.SameAs(second));
        }

        [Test]
        public void RegistryAndResolverValidateInputsAndUseExactTypeIdentity()
        {
            var registry = new EcaStateRegistry();
            IEcaStateResolver resolver = new EcaStateResolver(registry);
            var state = new BaseTestSupport.State { RuleId = "r" };
            Assert.Throws<ArgumentNullException>(() => new EcaStateResolver(null));
            Assert.Throws<ArgumentNullException>(() => registry.Register<object>("global", null));

            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<object>("global", state));
            Assert.That(registry.Unregister("global"), Is.False);
            registry.Register<string>("text", _ => "value");
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve<string>("text", null));
            Assert.Throws<InvalidOperationException>(() => registry.Register<string>("text", _ => "duplicate"));
            Assert.That(registry.Contains("text"), Is.True);
            Assert.That(registry.Contains("global"), Is.False);
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<object>("global", state));
            Assert.That(resolver.Resolve<string>("text", state), Is.EqualTo("value"));
            Assert.That(registry.Unregister("text"), Is.True);
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<string>("text", state));
        }

        [Test]
        public void StableResolverForwardsExactStateOnEveryCallWithoutCachingExternalResults()
        {
            var registry = new EcaStateRegistry();
            var global = new object();
            registry.Register<object>("global", _ => global);
            var seen = new List<IEcaRuleState>();
            registry.Register<string>("text", state => { seen.Add(state); return state.RuleId; });
            var resolver = new EcaStateResolver(registry);
            var first = new BaseTestSupport.State { RuleId = "first" };
            var second = new BaseTestSupport.State { RuleId = "second" };
            Assert.That(resolver.Resolve<object>("global", first), Is.SameAs(global));
            Assert.That(resolver.Resolve<object>("global", second), Is.SameAs(global));
            Assert.That(resolver.Resolve<string>("text", first), Is.EqualTo("first"));
            Assert.That(resolver.Resolve<string>("text", second), Is.EqualTo("second"));
            first.RuleId = "changed";
            Assert.That(resolver.Resolve<string>("text", first), Is.EqualTo("changed"));
            Assert.That(seen, Is.EqualTo(new IEcaRuleState[] { first, second, first }));
            registry.Unregister("text");
            registry.Register<string>("text", _ => null);
            Assert.That(resolver.Resolve<string>("text", first), Is.Null);
            registry.Unregister("text");
            var error = new InvalidOperationException("external");
            registry.Register<string>("text", _ => throw error);
            Assert.That(Assert.Throws<InvalidOperationException>(() => resolver.Resolve<string>("text", first)), Is.SameAs(error));
        }

        [Test]
        public void ConditionAndActionInitializationIsAtomicAndOneTime()
        {
            var condition = new ReadingCondition();
            var action = new ReadingAction();
            var commands = new EcaCommandRunner(new EcaCommandRegistry());
            var resolver = new EcaStateResolver(new EcaStateRegistry());
            Assert.Throws<InvalidOperationException>(() => _ = condition.Resolver);
            Assert.Throws<InvalidOperationException>(() => _ = action.Resolver);
            Assert.Throws<InvalidOperationException>(() => _ = action.CommandCapability);
            Assert.Throws<ArgumentNullException>(() => condition.Initialize(null));
            Assert.Throws<ArgumentNullException>(() => action.Initialize(null, resolver));
            Assert.Throws<ArgumentNullException>(() => action.Initialize(commands, null));
            Assert.Throws<InvalidOperationException>(() => _ = action.CommandCapability);
            Assert.Throws<InvalidOperationException>(() => _ = action.Resolver);
            condition.Initialize(resolver);
            action.Initialize(commands, resolver);
            Assert.That(condition.Resolver, Is.SameAs(resolver));
            Assert.That(action.Resolver, Is.SameAs(resolver));
            Assert.That(action.CommandCapability, Is.SameAs(commands));
            Assert.Throws<InvalidOperationException>(() => condition.Initialize(resolver));
            Assert.Throws<InvalidOperationException>(() => action.Initialize(commands, resolver));
        }

        [Test]
        public void RuntimeConnectDisconnectAndReconnectShareCapabilitiesButExternalSystemOwnsGroupState()
        {
            var external = new Dictionary<(string, string), ExternalState>();
            var observed = new List<IEcaRuleState>();
            var states = new EcaStateRegistry();
            states.Register<ExternalState>("external", state =>
            {
                observed.Add(state);
                var scope = (IEcaScopeRuleState<int>)state;
                var key = (scope.ScopeState.ScopeId, state.RuleId);
                if (!external.TryGetValue(key, out var value)) external.Add(key, value = new ExternalState());
                return value;
            });
            using var runtime = new EcaSystemsRuntime();
            var events = new EcaBaseEventRegistry();
            var evt = new BaseTestSupport.Event<int>();
            events.Register(evt);
            // Event and State exports can belong to independent Systems.
            runtime.ConnectSystem(new EcaSystem("events", new EcaSystemNamespace("events"), events, new EcaCommandRegistry(), new EcaStateRegistry()));
            var stateSystem = new EcaSystem("state", new EcaSystemNamespace("state"), new EcaBaseEventRegistry(), new EcaCommandRegistry(), states);
            var rule = runtime.CreateRule<int, ReadingCondition, ReadingAction>("rule-A", evt.Id);
            var otherRule = runtime.CreateRule<int, ReadingAction>("rule-B", evt.Id);
            var condition = (ReadingCondition)rule.Condition;
            var action = (ReadingAction)rule.Action;
            Assert.That(condition.Resolver, Is.SameAs(action.Resolver));
            Assert.That(((ReadingAction)otherRule.Action).Resolver, Is.SameAs(action.Resolver));
            Assert.That(((ReadingAction)otherRule.Action).CommandCapability, Is.SameAs(action.CommandCapability));
            var first = runtime.CreateScope("first");
            var second = first.CreateScope("second");
            var allow = new EcaExecutionMode(EcaExecutionModeOverlap.Allow);
            first.Register(rule, allow);
            second.Register(rule, allow);
            first.Register(otherRule, allow);
            Assert.Throws<InvalidOperationException>(() => first.EventEmitter.Fire(evt, 0));
            runtime.ConnectSystem(stateSystem);
            first.EventEmitter.Fire(evt, 1);
            second.EventEmitter.Fire(evt, 2);
            Assert.That(external.Count, Is.EqualTo(3));
            Assert.That(external[("first", "rule-A")].Reads, Is.EqualTo(2));
            Assert.That(external[("second", "rule-A")].Reads, Is.EqualTo(2));
            Assert.That(external[("first", "rule-B")].Reads, Is.EqualTo(1));
            Assert.That(observed[0], Is.SameAs(condition.Seen[0]));
            Assert.That(observed[1], Is.SameAs(action.Seen[0]));
            Assert.That(first.GetGroup(rule.Id), Is.Not.SameAs(second.GetGroup(rule.Id)));
            runtime.DisconnectSystem(stateSystem);
            Assert.Throws<InvalidOperationException>(() => action.Resolver.Resolve<ExternalState>("external", observed[0]));
            Assert.That(states.Contains("external"), Is.True);
            runtime.ConnectSystem(stateSystem);
            Assert.That(action.Resolver.Resolve<ExternalState>("external", observed[0]), Is.SameAs(external[("first", "rule-A")]));
            runtime.Dispose();
            Assert.Throws<InvalidOperationException>(() => action.Resolver.Resolve<ExternalState>("external", observed[0]));
            foreach (var value in external.Values) Assert.That(value.Disposed, Is.False);
        }

        [Test]
        public async Task ForceFireKeepsBaseBarrierAndSkipsExecutionWhileEmitterHonorsMode()
        {
            using var runtime = new EcaSystemsRuntime();
            var evt = new BaseTestSupport.Event<int>();
            var events = new EcaBaseEventRegistry(); events.Register(evt);
            runtime.ConnectSystem(new EcaSystem("s", new EcaSystemNamespace("s"), events, new EcaCommandRegistry(), new EcaStateRegistry()));
            var trace = new List<string>();
            var received = new List<EcaScopeRuleState<int>>();
            var created = new List<EcaScopeRuleState<int>>();
            var gate = new TaskCompletionSource<bool>();
            var scope = runtime.CreateScope("scope");
            foreach (var id in new[] { "A", "B" })
            {
                var rule = runtime.CreateRule<int>(id, evt.Id,
                    condition: (s, c) => { trace.Add("check " + s.RuleId); return true; },
                    action: (s, c, commands) => { trace.Add("action " + s.RuleId); received.Add(s); return gate.Task; });
                scope.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow, 0));
            }
            try
            {
                scope.EventEmitter.Fire(evt, 1);
                Assert.That(trace, Is.EqualTo(new[] { "check A", "check B" }));
                trace.Clear();
                scope.ForceFire<int, EcaScopeRuleState<int>>(evt, 2, (rule, payload) =>
                {
                    var state = new EcaScopeRuleState<int>(rule.Id, payload, new EcaExecutionGroupState(), scope.State);
                    created.Add(state); return state;
                });
                Assert.That(trace, Is.EqualTo(new[] { "check A", "check B", "action A", "action B" }));
                Assert.That(received, Is.EqualTo(created));
                Assert.That(gate.Task.IsCompleted, Is.False);
                foreach (var id in new[] { "A", "B" })
                {
                    Assert.That(scope.GetGroup(id).State.TotalStarted, Is.Zero);
                    Assert.That(scope.GetGroup(id).State.TotalFinished, Is.Zero);
                    Assert.That(scope.GetGroup(id).Executions, Is.Empty);
                }
            }
            finally { gate.TrySetResult(true); }
            await gate.Task;
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void StandardStatesRequireRuleIdentity(string id)
        {
            var group = new EcaExecutionGroupState();
            Assert.Throws<ArgumentException>(() => new EcaExecutionRuleState<int>(id, 0, group));
            Assert.Throws<ArgumentException>(() => new EcaScopeRuleState<int>(id, 0, group, new EcaScopeState("s")));
        }

        public sealed class ReadingCondition : AEcaCondition<EcaScopeRuleState<int>>
        {
            public override string Id => "read-condition";
            public IEcaStateResolver Resolver => State;
            public readonly List<EcaScopeRuleState<int>> Seen = new();
            public override bool Check(EcaScopeRuleState<int> state, IEcaConditionContext context)
            {
                var external = State.Resolve<ExternalState>("external", state);
                Seen.Add(state); external.Reads++; return true;
            }
        }
        public sealed class ReadingAction : AEcaAction<EcaScopeRuleState<int>>
        {
            public override string Id => "read-action";
            public IEcaStateResolver Resolver => State;
            public IEcaCommands CommandCapability => Commands;
            public readonly List<EcaScopeRuleState<int>> Seen = new();
            public override Task Run(EcaScopeRuleState<int> state, IEcaActionContext context)
            {
                var external = State.Resolve<ExternalState>("external", state);
                Seen.Add(state); external.Reads++; return Task.CompletedTask;
            }
        }
    }
}
