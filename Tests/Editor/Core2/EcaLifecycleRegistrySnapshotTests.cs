using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaLifecycleRegistrySnapshotTests
    {
        private static void Frozen<T>(IReadOnlyList<T> snapshot, T original, System.Action mutate)
        {
            Assert.That(snapshot, Is.EqualTo(new[] { original }));
            Assert.Throws<NotSupportedException>(() => ((IList<T>)snapshot).Clear());
            mutate();
            Assert.That(snapshot, Is.EqualTo(new[] { original }));
        }

        [Test]
        public void EventSnapshotKeepsMembershipAndExactInstances()
        {
            IEcaEventRegistry registry = new EcaBaseEventRegistry();
            var first = new BaseTestSupport.Event<int>(); registry.Register(first);
            Frozen(registry.GetSnapshot(), first, () => { registry.Unregister(first.Id); registry.Register(new BaseTestSupport.Event<int>()); });
            Assert.That(registry.Resolve(first.Id), Is.Not.SameAs(first));
        }

        [Test]
        public void RuleSnapshotKeepsRegistrationOrderAndExactMembership()
        {
            var events = new EcaBaseEventRegistry(); var declaration = new BaseTestSupport.Event<int>(); events.Register(declaration);
            IEcaRuleRegistry rules = new EcaBaseRuleRegistry(events);
            var rule = new CommandTestSupport.Rule { Event = declaration, Action = new CommandTestSupport.CommandAction() };
            rules.Register(rule);
            Frozen(rules.GetSnapshot(), rule, () => rules.Unregister(rule));
            Assert.That(rules.GetSnapshot(), Is.Empty);
        }

        [Test]
        public void ExecutionGroupSnapshotSurvivesUnregister()
        {
            var events = new EcaBaseEventRegistry(); var declaration = new BaseTestSupport.Event<int>(); events.Register(declaration);
            var groups = new EcaExecutionGroupRegistry();
            var runtime = new EcaExecutionRuntime(new EcaBaseRuleRegistry(events), groups, new EcaBaseConditionChecker(), new EcaBaseActionRunner());
            var rule = new ExecutionTestSupport.Rule<int, EcaExecutionRuleState<int>>
            {
                Event = declaration,
                Action = new ExecutionTestSupport.Action<EcaExecutionRuleState<int>, IEcaActionContext> { Handler = (s, c) => Task.CompletedTask }
            };
            runtime.Register(rule, new EcaExecutionMode(EcaExecutionModeOverlap.Allow),
                (e, g) => new EcaExecutionRuleState<int>(rule.Id, e, g));
            Frozen(groups.GetSnapshot(), groups.Get(rule.Id), () => runtime.Unregister(rule));
            Assert.That(groups.GetSnapshot(), Is.Empty);
        }

        [Test]
        public void CommandSnapshotSurvivesReplacement()
        {
            var registry = new EcaCommandRegistry(); var command = new CommandTestSupport.Command<IEcaActionContext, int>(); registry.Register(command);
            Frozen(registry.GetSnapshot(), command, () => { registry.Unregister(command.Id); registry.Register(new CommandTestSupport.Command<IEcaActionContext, int>()); });
        }

        [Test]
        public void StateSnapshotExposesOnlyIdsAndSurvivesShapeReplacement()
        {
            var registry = new EcaStateRegistry(); registry.Register("state", _ => 1);
            Frozen(registry.GetSnapshot(), "state", () => { registry.Unregister("state"); registry.Register("payload", (_, payload) => payload); });
            Assert.That(registry.GetSnapshot(), Is.EqualTo(new[] { "payload" }));
        }

        [Test]
        public void SystemAndNamespaceSnapshotsPreserveExactInstances()
        {
            var systems = new EcaSystemRegistry(); var namespaces = new EcaSystemNamespaceRegistry();
            var ns = new EcaSystemNamespace("test");
            var system = new EcaSystem("test", ns, new EcaBaseEventRegistry(), new EcaCommandRegistry(), new EcaStateRegistry());
            systems.Register(system); namespaces.Register(ns);
            Frozen(systems.GetSnapshot(), system, () => systems.Unregister("test"));
            Frozen(namespaces.GetSnapshot(), ns, () => namespaces.Unregister("test"));
        }

        [Test]
        public void LifecycleRegistrySnapshotUsesOrdinalSystemOrderAndRejectsSharedConnector()
        {
            var registry = new EcaSystemLifecycleConnectorRegistry(); var calls = new List<string>();
            var a = new EcaLifecycleTests.Probe("a", calls); var z = new EcaLifecycleTests.Probe("z", calls);
            registry.Register("z", z); registry.Register("a", a);
            var snapshot = registry.GetSnapshot();
            Assert.That(snapshot, Is.EqualTo(new[] { a, z }));
            Assert.Throws<NotSupportedException>(() => ((IList<IEcaSystemLifecycleConnector>)snapshot).Clear());
            Assert.Throws<InvalidOperationException>(() => registry.Register("other", a));
            registry.Unregister("a"); registry.Unregister("z");
            Assert.That(snapshot, Is.EqualTo(new[] { a, z }));
            Assert.That(registry.GetSnapshot(), Is.Empty);
        }

        [Test]
        public void SystemConnectorOnlyRegistersLifecycleAndRollsBackOnRegistryFailure()
        {
            var registry = new EcaSystemLifecycleConnectorRegistry(); var calls = new List<string>();
            var probe = new EcaLifecycleTests.Probe("connector", calls);
            var events = new EcaBaseEventRegistry(); var systems = new EcaSystemRegistry(); var namespaces = new EcaSystemNamespaceRegistry();
            var connector = new EcaSystemConnector(systems, namespaces, events, new EcaCommandRegistry(), new EcaStateRegistry(), registry);
            var first = new EcaSystem("first", new EcaSystemNamespace("first"), new EcaBaseEventRegistry(), new EcaCommandRegistry(), new EcaStateRegistry(), lifecycleConnector: probe);
            var local = new EcaBaseEventRegistry(); var declaration = new BaseTestSupport.Event<int>(); local.Register(declaration);
            var second = new EcaSystem("second", new EcaSystemNamespace("second"), local, new EcaCommandRegistry(), new EcaStateRegistry(), lifecycleConnector: probe);
            connector.Connect(first);
            Assert.That(registry.Resolve("first"), Is.SameAs(probe));
            Assert.Throws<InvalidOperationException>(() => connector.Connect(second));
            Assert.That(events.Events, Is.Empty); Assert.That(namespaces.Contains("second"), Is.False);
            Assert.That(systems.Contains("second"), Is.False);
            connector.Disconnect(first);
            Assert.That(registry.GetSnapshot(), Is.Empty);
            Assert.That(calls, Is.Empty, "Low-level SystemConnector never executes topology callbacks.");
        }
    }
}
