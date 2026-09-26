using System;
using System.Collections.Generic;
using EcaSystems.Core2;
using NUnit.Framework;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaSystemConnectorTests
    {
        private EcaSystemRegistry _systems;
        private EcaSystemNamespaceRegistry _namespaces;
        private FaultEvents _events;
        private EcaCommandRegistry _commands;
        private EcaStateRegistry _states;
        private readonly object _externalState = new();
        private EcaSystemConnector _connector;
        private EcaSystem _system;

        [SetUp]
        public void SetUp()
        {
            _systems = new EcaSystemRegistry();
            _namespaces = new EcaSystemNamespaceRegistry();
            _events = new FaultEvents();
            _commands = new EcaCommandRegistry();
            _states = new EcaStateRegistry();
            _connector = new EcaSystemConnector(_systems, _namespaces, _events, _commands, _states);
            var events = new EcaBaseEventRegistry();
            events.Register(new BaseTestSupport.Event<int> { Id = "first" });
            events.Register(new BaseTestSupport.Event<string> { Id = "second" });
            var commands = new EcaCommandRegistry();
            commands.Register(new CommandTestSupport.Command<CommandTestSupport.Context, int>());
            _system = new EcaSystem("system", new EcaSystemNamespace("ns"), events, commands, new EcaStateRegistry());
            _system.States.Register<object>("global", _ => _externalState);
        }

        [Test]
        public void PayloadConnectDisconnectReconnectPreservesOriginalDelegateWithoutCallingIt()
        {
            var calls = 0;
            Func<IEcaRuleState, object, object> callback = (state, payload) => { calls++; return payload; };
            _system.States.Register<object>("payload", callback);
            _connector.Connect(_system);
            Assert.That(_states.ResolveWithPayload<object>("payload"), Is.SameAs(callback));
            _connector.Disconnect(_system);
            Assert.That(_states.Contains("payload"), Is.False);
            _connector.Connect(_system);
            Assert.That(_states.ResolveWithPayload<object>("payload"), Is.SameAs(callback));
            Assert.That(calls, Is.Zero);
            var payload = new object();
            Assert.That(new EcaStateResolver(_states).Resolve<object>("payload", new BaseTestSupport.State(), payload), Is.SameAs(payload));
            Assert.That(calls, Is.EqualTo(1));
            _connector.Disconnect(_system);
            Assert.That(calls, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PayloadDisconnectRejectsForeignShapeOrDelegateBeforeMutation(bool changeShape)
        {
            _system.States.Register<object>("payload", (state, payload) => throw new Exception("Must not call"));
            _connector.Connect(_system);
            _states.Unregister("payload");
            if (changeShape) _states.Register<object>("payload", state => throw new Exception("Must not call"));
            else _states.Register<object>("payload", (state, payload) => throw new Exception("Must not call"));
            Assert.Throws<InvalidOperationException>(() => _connector.Disconnect(_system));
            Assert.That(_systems.CheckRegistered(_system), Is.True);
            Assert.That(_events.Events.Count, Is.EqualTo(2));
            Assert.That(_commands.Commands.Count, Is.EqualTo(1));
            Assert.That(_states.Contains("global"), Is.True);
            Assert.That(_states.Contains("payload"), Is.True);
        }

        [Test]
        public void PayloadDisconnectRollbackRestoresOriginalDelegateAndShape()
        {
            Func<IEcaRuleState, object, object> callback = (state, payload) => throw new Exception("Must not call");
            _system.States.Register<object>("payload", callback);
            _connector.Connect(_system);
            _events.FailUnregisterId = "second";
            Assert.Throws<InvalidOperationException>(() => _connector.Disconnect(_system));
            AssertConnected();
            Assert.That(_states.ResolveWithPayload<object>("payload"), Is.SameAs(callback));
            Assert.Throws<InvalidOperationException>(() => _states.Resolve<object>("payload"));
        }

        [Test]
        public void PayloadConnectRollbackRemovesOnlyCompletedExports()
        {
            Func<IEcaRuleState, object, object> callback = (state, payload) => throw new Exception("Must not call");
            _system.States.Register<object>("payload", callback);
            _states.Register<object>("unrelated", callback);
            _events.OnRegister = () => { _events.OnRegister = null; _systems.Register(_system); };
            Assert.Throws<InvalidOperationException>(() => _connector.Connect(_system));
            Assert.That(_states.Contains("payload"), Is.False);
            Assert.That(_states.Contains("global"), Is.False);
            Assert.That(_states.ResolveWithPayload<object>("unrelated"), Is.SameAs(callback));
            Assert.That(_system.States.ResolveWithPayload<object>("payload"), Is.SameAs(callback));
            Assert.That(_events.Events, Is.Empty);
            Assert.That(_commands.Commands, Is.Empty);
            Assert.That(_namespaces.Namespaces, Is.Empty);
        }

        [Test]
        public void StateConflictWithDifferentDeclaredTypeIsRejectedBeforeMutation()
        {
            _states.Register<int>("global", _ => 42);
            Assert.Throws<InvalidOperationException>(() => _connector.Connect(_system));
            Assert.That(_events.Events, Is.Empty);
            Assert.That(_commands.Commands, Is.Empty);
            Assert.That(_systems.Systems, Is.Empty);
            Assert.That(_namespaces.Namespaces, Is.Empty);
            Assert.That(new EcaStateResolver(_states).Resolve<int>("global", new BaseTestSupport.State()), Is.EqualTo(42));
        }

        [TestCase("different-id")]
        [TestCase("different-type")]
        [TestCase("different-delegate")]
        public void DisconnectRequiresIdTypeAndExactDelegateWithoutInvokingResolver(string replacement)
        {
            _system.States.Unregister("global");
            Func<IEcaRuleState, string> callback = _ => throw new Exception("Must not resolve during composition");
            _system.States.Register<string>("global", callback);
            _connector.Connect(_system);
            _states.Unregister("global");
            if (replacement == "different-id") _states.Register<string>("other", callback);
            // Covariance permits the exact same delegate instance with a different declared contract.
            if (replacement == "different-type") _states.Register<object>("global", callback);
            if (replacement == "different-delegate") _states.Register<string>("global", _ => throw new Exception());
            Assert.Throws<InvalidOperationException>(() => _connector.Disconnect(_system));
            Assert.That(_systems.CheckRegistered(_system), Is.True);
            Assert.That(_events.Events.Count, Is.EqualTo(2));
            Assert.That(_commands.Commands.Count, Is.EqualTo(1));
            Assert.That(_namespaces.CheckRegistered(_system.Namespace), Is.True);
            _states.Unregister("global");
            _states.Register<string>("global", callback);
            _connector.Disconnect(_system);
            Assert.That(_states.Contains("global"), Is.False);
            Assert.That(_system.States.Contains("global"), Is.True);
        }

        [Test]
        public void ConnectDisconnect_KeepExactLocalReferences_AndUnrelatedRegistrations()
        {
            var localEvents = _system.Events;
            var localCommands = _system.Commands;
            var localStates = _system.States;
            var otherEvent = new BaseTestSupport.Event<int> { Id = "other" };
            _events.Register(otherEvent);
            Assert.That(_events.CheckRegistered(localEvents.Resolve("first")), Is.False);
            _connector.Connect(_system);
            Assert.That(_system.Events, Is.SameAs(localEvents));
            Assert.That(_system.Commands, Is.SameAs(localCommands));
            Assert.That(_system.States, Is.SameAs(localStates));
            AssertConnected();
            Assert.Throws<InvalidOperationException>(() => _connector.Connect(_system));
            _connector.Disconnect(_system);
            Assert.That(_events.CheckRegistered(otherEvent), Is.True);
            Assert.That(_events.CheckRegistered(localEvents.Resolve("first")), Is.False);
            Assert.That(_states.Contains("global"), Is.False);
            Assert.That(_commands.Commands, Is.Empty);
            Assert.That(_systems.Systems, Is.Empty);
            Assert.That(_namespaces.Namespaces, Is.Empty);
            Assert.That(localEvents.Events.Count, Is.EqualTo(2));
            Assert.That(localCommands.Commands.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => _connector.Disconnect(_system));
        }

        [TestCase("event")]
        [TestCase("command")]
        [TestCase("namespace")]
        [TestCase("state")]
        public void Disconnect_RejectsForeignCanonicalReplacementBeforeMutation(string kind)
        {
            _connector.Connect(_system);
            if (kind == "event")
            {
                _events.Unregister("first");
                _events.Register(new BaseTestSupport.Event<int> { Id = "first" });
            }
            if (kind == "command")
            {
                _commands.Unregister("command");
                _commands.Register(new CommandTestSupport.Command<CommandTestSupport.Context, int>());
            }
            if (kind == "namespace")
            {
                _namespaces.Unregister("ns");
                _namespaces.Register(new EcaSystemNamespace("ns"));
            }
            if (kind == "state")
            {
                _states.Unregister("global");
                _states.Register<object>("global", _ => new object());
            }
            var stateRegistration = _states.Resolve<object>("global");
            var e = _events.Resolve("first");
            var c = _commands.Resolve("command");
            var n = _namespaces.Resolve("ns");
            Assert.Throws<InvalidOperationException>(() => _connector.Disconnect(_system));
            Assert.That(_systems.CheckRegistered(_system), Is.True);
            Assert.That(_states.Resolve<object>("global"), Is.SameAs(stateRegistration));
            Assert.That(_events.Resolve("first"), Is.SameAs(e));
            Assert.That(_commands.Resolve("command"), Is.SameAs(c));
            Assert.That(_namespaces.Resolve("ns"), Is.SameAs(n));
        }

        [Test]
        public void ConnectFailure_RollsBackOnlyCompletedChanges()
        {
            var other = new BaseTestSupport.Event<int> { Id = "other" };
            _events.Register(other);
            _events.FailRegisterId = "second";
            Assert.Throws<InvalidOperationException>(() => _connector.Connect(_system));
            Assert.That(_events.Events, Is.EqualTo(new[] { other }));
            Assert.That(_states.Contains("global"), Is.False);
            Assert.That(_commands.Commands, Is.Empty);
            Assert.That(_systems.Systems, Is.Empty);
            Assert.That(_namespaces.Namespaces, Is.Empty);
            Assert.That(_system.Events.Events.Count, Is.EqualTo(2));
        }

        [Test]
        public void DisconnectFailure_RestoresExactInstances()
        {
            _connector.Connect(_system);
            var other = new BaseTestSupport.Event<int> { Id = "other" };
            _events.Register(other);
            _events.FailUnregisterId = "second";
            Assert.Throws<InvalidOperationException>(() => _connector.Disconnect(_system));
            AssertConnected();
            Assert.That(_events.CheckRegistered(other), Is.True);
        }

        [Test]
        public void Descriptor_RequiresLocalRegistriesAndNamespace()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaSystem("system", null, _system.Events, _system.Commands, new EcaStateRegistry()));
            Assert.Throws<ArgumentNullException>(() => new EcaSystem("system", _system.Namespace, null, _system.Commands, new EcaStateRegistry()));
            Assert.Throws<ArgumentNullException>(() => new EcaSystem("system", _system.Namespace, _system.Events, null, new EcaStateRegistry()));
            Assert.Throws<ArgumentNullException>(() => new EcaSystem("system", _system.Namespace, _system.Events, _system.Commands, null));
            Assert.Throws<ArgumentNullException>(() => new EcaSystemConnector(_systems, _namespaces, _events, _commands, null));
        }

        [Test]
        public void ConnectFailureAfterStateRegistration_RollsBackStateAndOtherCompletedExports()
        {
            _states.Register<int>("number", _ => 42);
            // Inject a last-step collision after prevalidation, without a new production fault API.
            _events.OnRegister = () => { _events.OnRegister = null; _systems.Register(_system); };
            Assert.Throws<InvalidOperationException>(() => _connector.Connect(_system));
            Assert.That(_states.Contains("global"), Is.False);
            Assert.That(new EcaStateResolver(_states).Resolve<int>("number", new BaseTestSupport.State()), Is.EqualTo(42));
            Assert.That(_events.Events, Is.Empty);
            Assert.That(_commands.Commands, Is.Empty);
            Assert.That(_namespaces.Namespaces, Is.Empty);
            Assert.That(_systems.CheckRegistered(_system), Is.True, "Injected registration is not owned by the failed Connect.");
            Assert.That(_system.States.Contains("global"), Is.True);
        }

        [Test]
        public void StateConflictIsRejectedBeforeAnyExportMutation()
        {
            _states.Register<object>("global", _ => new object());
            var original = _states.Resolve<object>("global");
            Assert.Throws<InvalidOperationException>(() => _connector.Connect(_system));
            Assert.That(_states.Resolve<object>("global"), Is.SameAs(original));
            Assert.That(_events.Events, Is.Empty);
            Assert.That(_commands.Commands, Is.Empty);
            Assert.That(_systems.Systems, Is.Empty);
            Assert.That(_namespaces.Namespaces, Is.Empty);
        }

        private void AssertConnected()
        {
            Assert.That(_systems.CheckRegistered(_system), Is.True);
            Assert.That(_namespaces.CheckRegistered(_system.Namespace), Is.True);
            Assert.That(_states.Resolve<object>("global"), Is.SameAs(_system.States.Resolve<object>("global")));
            Assert.That(new EcaStateResolver(_states).Resolve<object>("global", new BaseTestSupport.State()), Is.SameAs(_externalState));
            foreach (var e in _system.Events.Events)
            {
                Assert.That(_events.Resolve(e.Id), Is.SameAs(e));
                Assert.That(_events.CheckRegistered(e), Is.True);
            }
            foreach (var c in _system.Commands.Commands)
            {
                Assert.That(_commands.Resolve(c.Id), Is.SameAs(c));
                Assert.That(_commands.CheckRegistered(c), Is.True);
            }
        }

        // Fail before mutation: exercises the Connector's existing compensation contract.
        private sealed class FaultEvents : IEcaEventRegistry
        {
            private readonly EcaBaseEventRegistry _inner = new();
            internal string FailRegisterId, FailUnregisterId;
            internal System.Action OnRegister;
            public IReadOnlyCollection<IEcaEvent> Events => _inner.Events;
            public IReadOnlyList<IEcaEvent> GetSnapshot() => _inner.GetSnapshot();
            public void Register(IEcaEvent item)
            {
                if (item.Id == FailRegisterId) throw new InvalidOperationException("register failure");
                OnRegister?.Invoke();
                _inner.Register(item);
            }
            public bool Unregister(string id)
            {
                if (id == FailUnregisterId) throw new InvalidOperationException("unregister failure");
                return _inner.Unregister(id);
            }
            public bool Contains(string id) => _inner.Contains(id);
            public IEcaEvent Resolve(string id) => _inner.Resolve(id);
            public bool CheckRegistered(IEcaEvent item) => _inner.CheckRegistered(item);
        }
    }
}
