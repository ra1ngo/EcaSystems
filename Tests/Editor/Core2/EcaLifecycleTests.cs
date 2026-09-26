using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaLifecycleTests : ExecutionTestFixture
    {
        private EcaScopeLifecycle _lifecycle;
        private EcaScopeRuntime _scopes;
        private readonly List<string> _calls = new();
        private Probe _a, _b, _c;
        private EcaExecutionMode Mode => new(EcaExecutionModeOverlap.Allow);

        [SetUp]
        public void SetupLifecycle()
        {
            _calls.Clear();
            _lifecycle = new EcaScopeLifecycle();
            _a = new Probe("a", _calls); _b = new Probe("b", _calls); _c = new Probe("c", _calls);
            _lifecycle.Register(_a); _lifecycle.Register(_b); _lifecycle.Register(_c);
            _scopes = new EcaScopeRuntime(Events, new EcaBaseConditionChecker(), new EcaBaseActionRunner(), _lifecycle);
        }
        [TearDown]
        public void CleanupLifecycle()
        {
            _a.Fail = _b.Fail = _c.Fail = null;
            _a.Observe = _b.Observe = _c.Observe = null;
            _scopes.Dispose();
        }
        private Rule<int, EcaScopeRuleState<int>> MakeRule(string id = "rule", Func<EcaScopeRuleState<int>, Task> run = null) => new()
        {
            Id = id, Event = Event,
            Action = new ExecutionTestSupport.Action<EcaScopeRuleState<int>, IEcaActionContext>
                { Handler = (state, context) => run?.Invoke(state) ?? Task.CompletedTask }
        };

        [Test]
        public void MultipleListenersConnectFailureCompensatesInReverseAndRemovesFailedScope()
        {
            _c.Fail = "+scope:root";
            EcaScope failed = null;
            _c.Observe = (op, scope) => failed = scope;
            Assert.Throws<InvalidOperationException>(() => _scopes.CreateScope("root"));
            Assert.That(_calls, Is.EqualTo(new[] { "a+scope:root", "b+scope:root", "c+scope:root", "b-scope:root", "a-scope:root" }));
            Assert.That(_scopes.ScopeCount, Is.Zero);
            Assert.That(failed.IsDisposed, Is.True);
            Assert.That(_a.Bound, Is.Empty); Assert.That(_b.Bound, Is.Empty);
            _c.Fail = null; _c.Observe = null;
            Assert.That(_scopes.CreateScope("root"), Is.Not.SameAs(failed));
        }

        [Test]
        public void RuleRegisterFailureRollsBackListenersAndCoreMembership()
        {
            var scope = _scopes.CreateScope("root");
            var rule = MakeRule();
            _calls.Clear(); _c.Fail = "+rule:root:rule";
            Assert.Throws<InvalidOperationException>(() => scope.Register(rule, Mode));
            Assert.That(_calls, Is.EqualTo(new[] { "a+rule:root:rule", "b+rule:root:rule", "c+rule:root:rule", "b-rule:root:rule", "a-rule:root:rule" }));
            Assert.That(scope.GetRulesSnapshot(), Is.Empty);
            Assert.That(scope.TryGetGroup(rule.Id, out _), Is.False);
            Assert.That(_a.Bound[scope], Is.Empty); Assert.That(_b.Bound[scope], Is.Empty);
            _c.Fail = null; scope.Register(rule, Mode);
            Assert.That(scope.GetRulesSnapshot().Single(), Is.SameAs(rule));
        }

        [Test]
        public void RuleUnregisterFailureRestoresBindingBeforeCoreRemovalAndKeepsExactGroup()
        {
            var scope = _scopes.CreateScope("root"); var rule = MakeRule(); scope.Register(rule, Mode);
            var group = scope.GetGroup(rule.Id);
            _calls.Clear(); _b.Fail = "-rule:root:rule";
            Assert.Throws<InvalidOperationException>(() => scope.Unregister(rule));
            Assert.That(_calls, Is.EqualTo(new[] { "a-rule:root:rule", "b-rule:root:rule", "a+rule:root:rule" }));
            Assert.That(scope.GetGroup(rule.Id), Is.SameAs(group));
            foreach (var probe in new[] { _a, _b, _c }) Assert.That(probe.Bound[scope], Does.Contain(rule));
            _b.Fail = null; Assert.That(scope.Unregister(rule), Is.True);
            _calls.Clear(); Assert.That(scope.Unregister(rule), Is.False); Assert.That(_calls, Is.Empty);
        }

        [Test]
        public void SameRuleInstanceInDifferentScopesHasIndependentMembership()
        {
            var a = _scopes.CreateScope("one"); var b = _scopes.CreateScope("two"); var rule = MakeRule();
            a.Register(rule, Mode); b.Register(rule, Mode); a.Unregister(rule);
            foreach (var probe in new[] { _a, _b, _c })
            {
                Assert.That(probe.Bound[a], Is.Empty);
                Assert.That(probe.Bound[b], Does.Contain(rule));
            }
        }

        [TestCase("leaf")]
        [TestCase("child")]
        [TestCase("root")]
        public void AtomicSubtreePrepareFailureKeepsAllInstancesRulesAndBindingsAlive(string failScope)
        {
            var root = _scopes.CreateScope("root"); var child = root.CreateScope("child"); var leaf = child.CreateScope("leaf");
            var rule = MakeRule(); foreach (var scope in new[] { root, child, leaf }) scope.Register(rule, Mode);
            var group = child.GetGroup(rule.Id);
            _b.Observe = (operation, scope) =>
            {
                Assert.That(_scopes.ScopeCount, Is.EqualTo(3));
                Assert.That(root.IsDisposed || child.IsDisposed || leaf.IsDisposed, Is.False);
                Assert.That(child.GetGroup(rule.Id), Is.SameAs(group));
            };
            _b.Fail = "-scope:" + failScope;
            Assert.Throws<InvalidOperationException>(() => root.Dispose());
            foreach (var scope in new[] { root, child, leaf })
            {
                Assert.That(scope.IsDisposed, Is.False);
                Assert.That(_scopes.TryGetScope(scope.ScopeId, out var found), Is.True);
                Assert.That(found, Is.SameAs(scope));
                foreach (var probe in new[] { _a, _b, _c }) Assert.That(probe.Bound[scope], Does.Contain(rule));
            }
            _b.Fail = null; _b.Observe = null; _calls.Clear(); root.Dispose();
            Assert.That(_calls, Is.EqualTo(new[] { "a-scope:leaf", "b-scope:leaf", "c-scope:leaf", "a-scope:child", "b-scope:child", "c-scope:child", "a-scope:root", "b-scope:root", "c-scope:root" }));
            Assert.That(root.IsDisposed && child.IsDisposed && leaf.IsDisposed, Is.True);
            Assert.That(_scopes.ScopeCount, Is.Zero);
            _calls.Clear(); root.Dispose(); Assert.That(_calls, Is.Empty);
        }

        [Test]
        public void DisposeOrderingIsDeterministicRegardlessOfCreationOrder()
        {
            var root = _scopes.CreateScope("root"); root.CreateScope("a"); root.CreateScope("z");
            _calls.Clear(); root.Dispose();
            Assert.That(_calls.Where(c => c.StartsWith("a-scope:")), Is.EqualTo(new[] { "a-scope:z", "a-scope:a", "a-scope:root" }));
        }

        [Test]
        public void LifecycleCannotReenterTopologyButCanFireImmediately()
        {
            var scope = _scopes.CreateScope("root"); var calls = 0;
            scope.Register(MakeRule(run: _ => { calls++; return Task.CompletedTask; }), Mode);
            _b.Observe = (operation, target) =>
            {
                target.Fire(Event, 1);
                Assert.Throws<InvalidOperationException>(() => target.CreateScope("nested"));
            };
            scope.Dispose();
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ActiveScopeIdIsGloballyUniqueNotSiblingLocal()
        {
            var gameplay = _scopes.CreateScope("gameplay"); var menu = _scopes.CreateScope("menu");
            var combat = gameplay.CreateScope("combat");
            Assert.Throws<InvalidOperationException>(() => menu.CreateScope("combat"));
            combat.Dispose(); var replacement = menu.CreateScope("combat");
            Assert.That(replacement, Is.Not.SameAs(combat));
            Assert.That(replacement.ParentScopeId, Is.EqualTo("menu"));
        }

        [Test]
        public void MultipleSystemConnectorsRollbackWithinCoordinator()
        {
            var registry = new EcaSystemLifecycleConnectorRegistry();
            var one = new Probe("one", _calls); var two = new Probe("two", _calls) { Fail = "+scope:root" };
            registry.Register("one", one); registry.Register("two", two);
            var coordinator = new EcaSystemLifecycleCoordinator(registry, _scopes);
            _lifecycle.Register(coordinator);
            Assert.Throws<InvalidOperationException>(() => _scopes.CreateScope("root"));
            Assert.That(one.Bound, Is.Empty); Assert.That(two.Bound, Is.Empty);
            Assert.That(_scopes.ScopeCount, Is.Zero);
            Assert.That(_a.Bound, Is.Empty);
            two.Fail = null;
            var scope = _scopes.CreateScope("root"); var rule = MakeRule(); scope.Register(rule, Mode);
            two.Fail = "-scope:root";
            Assert.Throws<InvalidOperationException>(() => scope.Dispose());
            Assert.That(one.Bound[scope], Does.Contain(rule));
            Assert.That(two.Bound[scope], Does.Contain(rule));
            two.Fail = null;
        }

        [Test]
        public void LateSystemSyncUsesCurrentTopologyAndRuleSnapshotsParentFirst()
        {
            using var runtime = new EcaSystemsRuntime();
            var events = new EcaBaseEventRegistry(); events.Register(Event);
            runtime.ConnectSystem(new EcaSystem("source", new EcaSystemNamespace("source"), events, new EcaCommandRegistry(), new EcaStateRegistry()));
            var root = runtime.CreateScope("root"); var child = root.CreateScope("child");
            var rule = runtime.CreateRule<int>("rule", Event.Id, (s, c, cmd) => Task.CompletedTask);
            root.Register(rule, Mode); child.Register(rule, Mode);
            var unused = runtime.CreateRule<int>("unregistered", Event.Id, (s, c, cmd) => Task.CompletedTask);
            var probe = new Probe("late", _calls); var system = SystemWith("late", probe);
            _calls.Clear(); runtime.ConnectSystem(system);
            Assert.That(_calls, Is.EqualTo(new[] { "late+scope:root", "late+rule:root:rule", "late+scope:child", "late+rule:child:rule" }));
            _calls.Clear(); runtime.DisconnectSystem(system);
            Assert.That(_calls, Is.EqualTo(new[] { "late-scope:child", "late-scope:root" }));
            Assert.That(probe.Bound, Is.Empty); Assert.That(root.IsDisposed, Is.False);
        }

        [TestCase("+scope:child")]
        [TestCase("+rule:child:rule")]
        public void FailedLateConnectRollsBackBindingsAndSystemExports(string failure)
        {
            using var runtime = new EcaSystemsRuntime();
            var events = new EcaBaseEventRegistry(); events.Register(Event);
            runtime.ConnectSystem(new EcaSystem("source", new EcaSystemNamespace("source"), events, new EcaCommandRegistry(), new EcaStateRegistry()));
            var root = runtime.CreateScope("root"); var child = root.CreateScope("child");
            var rule = runtime.CreateRule<int>("rule", Event.Id, (s, c, cmd) => Task.CompletedTask);
            root.Register(rule, Mode); child.Register(rule, Mode);
            var probe = new Probe("late", _calls) { Fail = failure }; var system = SystemWith("late", probe);
            Assert.Throws<InvalidOperationException>(() => runtime.ConnectSystem(system));
            Assert.That(probe.Bound, Is.Empty); Assert.That(root.IsDisposed || child.IsDisposed, Is.False);
            probe.Fail = null; runtime.ConnectSystem(system); // all registry identity reservations rolled back
            Assert.That(probe.Bound[child], Does.Contain(rule));
            probe.Fail = "-scope:root";
            Assert.Throws<InvalidOperationException>(() => runtime.DisconnectSystem(system));
            Assert.That(probe.Bound[child], Does.Contain(rule));
            Assert.That(probe.Bound[root], Does.Contain(rule));
            probe.Fail = null; runtime.DisconnectSystem(system);
        }

        [Test]
        public void RuntimeDisposePrepareFailureLeavesRuntimeUsableAndForestAlive()
        {
            using var runtime = new EcaSystemsRuntime();
            var probe = new Probe("system", _calls); runtime.ConnectSystem(SystemWith("system", probe));
            var a = runtime.CreateScope("a"); var z = runtime.CreateScope("z"); probe.Fail = "-scope:a";
            Assert.Throws<InvalidOperationException>(() => runtime.Dispose());
            Assert.That(a.IsDisposed || z.IsDisposed, Is.False);
            Assert.That(probe.Bound.Keys, Is.EquivalentTo(new[] { a, z }));
            probe.Fail = null; runtime.CreateScope("more"); runtime.Dispose();
            Assert.That(a.IsDisposed && z.IsDisposed, Is.True);
        }

        [Test]
        public void ExportDisconnectFailureRestoresAlreadyDisconnectedLifecycleBindings()
        {
            using var runtime = new EcaSystemsRuntime();
            var probe = new Probe("system", _calls);
            var local = new EcaBaseEventRegistry(); local.Register(Event);
            var system = new EcaSystem("system", new EcaSystemNamespace("system"), local,
                new EcaCommandRegistry(), new EcaStateRegistry(), lifecycleConnector: probe);
            runtime.ConnectSystem(system);
            var root = runtime.CreateScope("root");
            var rule = runtime.CreateRule<int>("rule", Event.Id, (state, context, commands) => Task.CompletedTask);
            root.Register(rule, Mode);
            var impostor = new BaseTestSupport.Event<int> { Id = Event.Id };
            local.Unregister(Event.Id); local.Register(impostor);
            Assert.Throws<InvalidOperationException>(() => runtime.DisconnectSystem(system));
            Assert.That(probe.Bound[root], Does.Contain(rule));
            Assert.That(root.IsDisposed, Is.False);
            local.Unregister(Event.Id); local.Register(Event);
            runtime.DisconnectSystem(system);
            Assert.That(probe.Bound, Is.Empty);
        }

        [Test]
        public void CompensationFailureIsAggregatedAndRemainingListenersStillRollback()
        {
            _c.Fail = "+scope:root"; _b.Fail = "-scope:root";
            var error = Assert.Throws<AggregateException>(() => _scopes.CreateScope("root"));
            Assert.That(error.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(_calls.Last(), Is.EqualTo("a-scope:root"));
            Assert.That(_scopes.ScopeCount, Is.Zero); Assert.That(_a.Bound, Is.Empty);
            // Failed third-party compensation cannot be guaranteed by Core.
            _b.Bound.Clear();
        }

        private static EcaSystem SystemWith(string id, IEcaSystemLifecycleConnector connector) =>
            new(id, new EcaSystemNamespace(id), new EcaBaseEventRegistry(), new EcaCommandRegistry(), new EcaStateRegistry(), lifecycleConnector: connector);

        internal sealed class Probe : IEcaScopeLifecycleListener, IEcaSystemLifecycleConnector
        {
            private readonly string _name;
            private readonly List<string> _calls;
            internal string Fail;
            internal System.Action<string, EcaScope> Observe;
            internal readonly Dictionary<EcaScope, HashSet<IEcaRule>> Bound = new();
            internal Probe(string name, List<string> calls) { _name = name; _calls = calls; }
            private void Before(string operation, EcaScope scope)
            {
                _calls.Add(_name + operation); Observe?.Invoke(operation, scope);
                if (operation == Fail) throw new InvalidOperationException("injected " + operation);
            }
            public void ConnectScope(EcaScope scope) { Before("+scope:" + scope.ScopeId, scope); Bound.Add(scope, new()); }
            public void DisconnectScope(EcaScope scope) { Before("-scope:" + scope.ScopeId, scope); Assert.That(Bound.Remove(scope), Is.True); }
            public void ConnectRule(EcaScope scope, IEcaRule rule) { Before("+rule:" + scope.ScopeId + ":" + rule.Id, scope); Assert.That(Bound[scope].Add(rule), Is.True); }
            public void DisconnectRule(EcaScope scope, IEcaRule rule) { Before("-rule:" + scope.ScopeId + ":" + rule.Id, scope); Assert.That(Bound[scope].Remove(rule), Is.True); }
        }
    }
}
