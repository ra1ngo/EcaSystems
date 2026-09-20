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
        private EcaSystemConnector _connector;
        private EcaSystem _system;

        [SetUp]
        public void SetUp()
        {
            _systems = new EcaSystemRegistry();
            _namespaces = new EcaSystemNamespaceRegistry();
            _events = new FaultEvents();
            _commands = new EcaCommandRegistry();
            _connector = new EcaSystemConnector(_systems, _namespaces, _events, _commands);
            var events = new EcaBaseEventRegistry();
            events.Register(new BaseTestSupport.Event<int> { Id = "first" });
            events.Register(new BaseTestSupport.Event<string> { Id = "second" });
            var commands = new EcaCommandRegistry();
            commands.Register(new CommandTestSupport.Command<CommandTestSupport.Context, int>());
            _system = new EcaSystem("system", new EcaSystemNamespace("ns"), events, commands);
        }

        [Test]
        public void ConnectDisconnect_KeepExactLocalReferences_AndUnrelatedRegistrations()
        {
            var localEvents = _system.Events;
            var localCommands = _system.Commands;
            var otherEvent = new BaseTestSupport.Event<int> { Id = "other" };
            _events.Register(otherEvent);
            Assert.That(_events.CheckRegistered(localEvents.Resolve("first")), Is.False);
            _connector.Connect(_system);
            Assert.That(_system.Events, Is.SameAs(localEvents));
            Assert.That(_system.Commands, Is.SameAs(localCommands));
            AssertConnected();
            Assert.Throws<InvalidOperationException>(() => _connector.Connect(_system));
            _connector.Disconnect(_system);
            Assert.That(_events.CheckRegistered(otherEvent), Is.True);
            Assert.That(_events.CheckRegistered(localEvents.Resolve("first")), Is.False);
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
            var e = _events.Resolve("first");
            var c = _commands.Resolve("command");
            var n = _namespaces.Resolve("ns");
            Assert.Throws<InvalidOperationException>(() => _connector.Disconnect(_system));
            Assert.That(_systems.CheckRegistered(_system), Is.True);
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
            Assert.Throws<ArgumentNullException>(() => new EcaSystem("system", null, _system.Events, _system.Commands));
            Assert.Throws<ArgumentNullException>(() => new EcaSystem("system", _system.Namespace, null, _system.Commands));
            Assert.Throws<ArgumentNullException>(() => new EcaSystem("system", _system.Namespace, _system.Events, null));
        }

        private void AssertConnected()
        {
            Assert.That(_systems.CheckRegistered(_system), Is.True);
            Assert.That(_namespaces.CheckRegistered(_system.Namespace), Is.True);
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
            public IReadOnlyCollection<IEcaEvent> Events => _inner.Events;
            public void Register(IEcaEvent item)
            {
                if (item.Id == FailRegisterId) throw new InvalidOperationException("register failure");
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
