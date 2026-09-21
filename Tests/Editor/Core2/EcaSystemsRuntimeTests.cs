using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaSystemsRuntimeTests
    {
        private EcaSystemsRuntime _runtime;
        private BaseTestSupport.Event<int> _event;
        private EcaSystem _system;
        private EcaExecutionMode Allow => new(EcaExecutionModeOverlap.Allow);

        [SetUp]
        public void SetUp()
        {
            _runtime = new EcaSystemsRuntime();
            _event = new BaseTestSupport.Event<int>();
            var events = new EcaBaseEventRegistry();
            events.Register(_event);
            _system = new EcaSystem("system", new EcaSystemNamespace("ns"), events, new EcaCommandRegistry(), new EcaStateRegistry());
        }

        [TearDown]
        public void TearDown() => _runtime.Dispose();

        private Rule<int, EcaScopeRuleState<int>> Rule(Func<EcaScopeRuleState<int>, Task> run) =>
            new()
            {
                Id = "rule", Event = _event,
                Action = new ExecutionTestSupport.Action<EcaScopeRuleState<int>, IEcaActionContext>
                { Handler = (state, context) => run(state) }
            };

        [Test]
        public void CreateScope_ReturnsRealScopeWithoutAutomaticRoot()
        {
            var first = _runtime.CreateScope();
            Assert.That(first, Is.TypeOf<EcaScope>());
            Assert.That(first.ScopeId, Is.EqualTo("scope-1"));
            Assert.That(first.ParentScopeId, Is.Null);
            Assert.That(first.EventEmitter, Is.Not.Null);
            var root = _runtime.CreateScope("root");
            var child = root.CreateScope("child");
            Assert.That(child.ParentScopeId, Is.EqualTo("root"));
            Assert.Throws<InvalidOperationException>(() => _runtime.CreateScope("root"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ConnectSystem_WorksBeforeOrAfterScopeCreation(bool scopeFirst)
        {
            var scope = scopeFirst ? _runtime.CreateScope("root") : null;
            if (scopeFirst)
                Assert.Throws<InvalidOperationException>(() => scope.EventEmitter.Fire(_event, 1));
            _runtime.ConnectSystem(_system);
            scope ??= _runtime.CreateScope("root");
            EcaScopeRuleState<int> received = null;
            scope.Register(Rule(state => { received = state; return Task.CompletedTask; }), Allow);
            scope.EventEmitter.Fire(_event, 42);
            Assert.That(received.EventState, Is.EqualTo(42));
            Assert.That(received.ScopeState, Is.SameAs(scope.State));
            Assert.That(received.ExecutionGroupState, Is.SameAs(scope.GetGroup("rule").State));
            Assert.That(scope.GetGroup("rule").State.TotalFinished, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => scope.EventEmitter.Fire(new BaseTestSupport.Event<int>(), 0));
        }

        [Test]
        public void DisconnectReconnect_UsesLiveRegistryAndPreservesLocalRuleAndGroup()
        {
            var scope = _runtime.CreateScope();
            _runtime.ConnectSystem(_system);
            var calls = 0;
            var rule = Rule(state => { calls++; return Task.CompletedTask; });
            scope.Register(rule, Allow);
            var group = scope.GetGroup(rule.Id);
            scope.EventEmitter.Fire(_event, 1);
            _runtime.DisconnectSystem(_system);
            Assert.That(scope.GetGroup(rule.Id), Is.SameAs(group));
            Assert.Throws<InvalidOperationException>(() => scope.EventEmitter.Fire(_event, 2));
            Assert.That(calls, Is.EqualTo(1));
            _runtime.ConnectSystem(_system);
            scope.EventEmitter.Fire(_event, 3);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(group.State.TotalStarted, Is.EqualTo(2));
            Assert.That(group.State.TotalFinished, Is.EqualTo(2));
        }

        [Test]
        public void ConnectorValidation_IsDelegatedWithoutChangingExistingConnection()
        {
            Assert.Throws<ArgumentNullException>(() => _runtime.ConnectSystem(null));
            Assert.Throws<ArgumentNullException>(() => _runtime.DisconnectSystem(null));
            Assert.Throws<InvalidOperationException>(() => _runtime.DisconnectSystem(_system));
            _runtime.ConnectSystem(_system);
            Assert.Throws<InvalidOperationException>(() => _runtime.ConnectSystem(_system));
            var foreign = new EcaSystem(_system.Id, _system.Namespace, _system.Events, _system.Commands, new EcaStateRegistry());
            Assert.Throws<InvalidOperationException>(() => _runtime.DisconnectSystem(foreign));
            var scope = _runtime.CreateScope();
            Assert.DoesNotThrow(() => scope.EventEmitter.Fire(_event, 1));
            _runtime.DisconnectSystem(_system);
            Assert.Throws<InvalidOperationException>(() => scope.EventEmitter.Fire(_event, 1));
        }

        [Test]
        public void ScopesShareExportsButKeepLocalGroupsAndEmitters()
        {
            _runtime.ConnectSystem(_system);
            var first = _runtime.CreateScope("first");
            var second = _runtime.CreateScope("second");
            var seen = new List<string>();
            var rule = Rule(state => { seen.Add(state.ScopeState.ScopeId); return Task.CompletedTask; });
            first.Register(rule, Allow);
            second.Register(rule, Allow);
            Assert.That(first.EventEmitter, Is.Not.SameAs(second.EventEmitter));
            Assert.That(first.GetGroup(rule.Id), Is.Not.SameAs(second.GetGroup(rule.Id)));
            first.EventEmitter.Fire(_event, 1);
            Assert.That(seen, Is.EqualTo(new[] { "first" }));
            Assert.That(second.GetGroup(rule.Id).State.TotalStarted, Is.Zero);
            second.EventEmitter.Fire(_event, 2);
            Assert.That(seen, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void DifferentRuntimeDoesNotShareGlobalExportsOrScopeIds()
        {
            using var other = new EcaSystemsRuntime();
            _runtime.ConnectSystem(_system);
            var first = _runtime.CreateScope("root");
            var second = other.CreateScope("root");
            Assert.DoesNotThrow(() => first.EventEmitter.Fire(_event, 1));
            Assert.Throws<InvalidOperationException>(() => second.EventEmitter.Fire(_event, 1));
            other.ConnectSystem(_system);
            _runtime.Dispose();
            Assert.DoesNotThrow(() => second.EventEmitter.Fire(_event, 1));
        }

        [Test]
        public void Dispose_ClosesRootsChildrenAndOperationsBeforeEventValidation()
        {
            _runtime.ConnectSystem(_system);
            var root = _runtime.CreateScope("root");
            var child = root.CreateScope("child");
            var otherRoot = _runtime.CreateScope("other");
            var emitter = child.EventEmitter;
            _runtime.Dispose();
            foreach (var scope in new[] { root, child, otherRoot })
                Assert.That(scope.IsDisposed, Is.True);
            Assert.Throws<ObjectDisposedException>(() => emitter.Fire<int>(null, 0));
            Assert.Throws<ObjectDisposedException>(() => emitter.Fire(_event, 0));
            Assert.Throws<ObjectDisposedException>(() => _runtime.ConnectSystem(null));
            Assert.Throws<ObjectDisposedException>(() => _runtime.DisconnectSystem(null));
            Assert.Throws<ObjectDisposedException>(() => _runtime.CreateScope());
            Assert.DoesNotThrow(() => _runtime.Dispose());
        }

        [Test]
        public async Task Dispose_DoesNotCancelRunningAction()
        {
            _runtime.ConnectSystem(_system);
            var scope = _runtime.CreateScope();
            var gate = new TaskCompletionSource<bool>();
            scope.Register(Rule(state => gate.Task), Allow);
            scope.EventEmitter.Fire(_event, 0);
            var group = scope.GetGroup("rule");
            var execution = group.Executions[0];
            try
            {
                _runtime.Dispose();
                Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Running));
                Assert.That(group.State.TotalFinished, Is.Zero);
                Assert.That(gate.Task.IsCompleted, Is.False);
            }
            finally { gate.TrySetResult(true); }
            await WaitUntil(() => group.Executions.Count == 0);
            Assert.That(execution.Status, Is.EqualTo(EcaExecutionStatus.Completed));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_SnapshotsSystemsClosesScopesFirstAndAggregatesAllCleanupFailures()
        {
            var root = _runtime.CreateScope();
            var child = root.CreateScope();
            var probes = new List<ProbeEvents>();
            var failures = new[] { new InvalidOperationException("first"), new InvalidOperationException("last") };
            for (var i = 0; i < 3; i++)
            {
                var probe = new ProbeEvents();
                probes.Add(probe);
                _runtime.ConnectSystem(new EcaSystem("s" + i, new EcaSystemNamespace("ns" + i), probe, new EcaCommandRegistry(), new EcaStateRegistry()));
            }
            for (var i = 0; i < probes.Count; i++)
            {
                var index = i;
                probes[i].OnRead = () =>
                {
                    Assert.That(root.IsDisposed && child.IsDisposed, Is.True);
                    Assert.Throws<ObjectDisposedException>(() => _runtime.CreateScope());
                    Assert.Throws<ObjectDisposedException>(() => _runtime.ConnectSystem(_system));
                    Assert.Throws<ObjectDisposedException>(() => _runtime.DisconnectSystem(_system));
                    if (index != 1) throw failures[index == 0 ? 0 : 1];
                };
                probes[i].Reads = 0;
            }
            var error = Assert.Throws<AggregateException>(() => _runtime.Dispose());
            Assert.That(error.InnerExceptions, Is.EquivalentTo(failures));
            for (var i = 0; i < probes.Count; i++) Assert.That(probes[i].Reads, Is.EqualTo(i == 1 ? 2 : 1));
            Assert.DoesNotThrow(() => _runtime.Dispose());
            for (var i = 0; i < probes.Count; i++) Assert.That(probes[i].Reads, Is.EqualTo(i == 1 ? 2 : 1));
        }

        // Passive descriptor permits custom local registries; no Runtime injection API is needed.
        private sealed class ProbeEvents : IEcaEventRegistry
        {
            private readonly EcaBaseEventRegistry _inner = new();
            internal System.Action OnRead;
            internal int Reads;
            public IReadOnlyCollection<IEcaEvent> Events
            {
                get { Reads++; OnRead?.Invoke(); return _inner.Events; }
            }
            public void Register(IEcaEvent item) => _inner.Register(item);
            public bool Unregister(string id) => _inner.Unregister(id);
            public bool Contains(string id) => _inner.Contains(id);
            public IEcaEvent Resolve(string id) => _inner.Resolve(id);
            public bool CheckRegistered(IEcaEvent item) => _inner.CheckRegistered(item);
        }
    }
}
