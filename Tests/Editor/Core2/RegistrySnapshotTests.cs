using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.ExecutionTestSupport;

namespace EcaSystems.Tests.Core2
{
    public sealed class RegistrySnapshotTests : ExecutionTestFixture
    {
        private static void ReadOnly<T>(IReadOnlyList<T> snapshot, T item)
        {
            Assert.That(snapshot, Is.InstanceOf<ICollection<T>>());
            Assert.That(((ICollection<T>)snapshot).IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => ((ICollection<T>)snapshot).Add(item));
            Assert.Throws<NotSupportedException>(() => ((ICollection<T>)snapshot).Clear());
        }

        // Membership assertions intentionally do not impose dictionary ordering.
        private static void Membership<T>(Func<IReadOnlyList<T>> snapshot, Action<T> add,
            Action<T> remove, T first, T second) where T : class
        {
            var empty = snapshot();
            Assert.That(empty, Is.Empty);
            add(first);
            var before = snapshot();
            Assert.That(before.Count, Is.EqualTo(1));
            Assert.That(before[0], Is.SameAs(first));
            add(second);
            Assert.That(empty, Is.Empty);
            Assert.That(before.Count, Is.EqualTo(1));
            Assert.That(snapshot(), Is.EquivalentTo(new[] { first, second }));
            remove(first);
            Assert.That(before[0], Is.SameAs(first));
            var after = snapshot();
            Assert.That(after.Count, Is.EqualTo(1));
            Assert.That(after[0], Is.SameAs(second));
            ReadOnly(before, second);
            Assert.That(snapshot()[0], Is.SameAs(second));
        }

        [Test]
        public void Events_FreezeMembershipThroughInterfaceWithoutCopyingMetadata()
        {
            IEcaEventRegistry registry = new EcaBaseEventRegistry();
            var first = new BaseTestSupport.Event<int> { Id = "first" };
            var second = new BaseTestSupport.Event<int> { Id = "second" };
            Membership(registry.GetSnapshot, registry.Register, e => registry.Unregister(e.Id), first, second);
            var snapshot = registry.GetSnapshot();
            second.EventStateType = typeof(string);
            Assert.That(snapshot[0].EventStateType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void Rules_FreezeMembershipThroughInterfaceAndRetainRegistrationOrder()
        {
            IEcaRuleRegistry registry = new EcaBaseRuleRegistry(Events);
            var first = NewRule("a"); var second = NewRule("b");
            registry.Register(first); registry.Register(second);
            var before = registry.GetSnapshot();
            Assert.That(before, Is.EqualTo(new IEcaRule[] { first, second }));
            Assert.That(before[0], Is.SameAs(first));
            Assert.That(before[1], Is.SameAs(second));
            registry.Unregister(first);
            var third = NewRule("c"); registry.Register(third);
            Assert.That(before, Is.EqualTo(new IEcaRule[] { first, second }));
            Assert.That(registry.GetSnapshot(), Is.EqualTo(new IEcaRule[] { second, third }));
            first.Id = "changed-after-removal";
            Assert.That(before[0].Id, Is.EqualTo(first.Id));
            ReadOnly(before, third);
        }

        [Test]
        public void ExecutionGroups_FreezeMembershipThroughInterface()
        {
            IEcaExecutionGroupRegistry registry = new EcaExecutionGroupRegistry();
            var first = new EcaExecutionGroup<int, State>(NewRule("a"), new(EcaExecutionModeOverlap.Allow),
                (e, g) => new State(e, g) { RuleId = "a" }, new EcaBaseConditionChecker(), new EcaBaseActionRunner());
            var second = new EcaExecutionGroup<int, State>(NewRule("b"), new(EcaExecutionModeOverlap.Allow),
                (e, g) => new State(e, g) { RuleId = "b" }, new EcaBaseConditionChecker(), new EcaBaseActionRunner());
            Membership(registry.GetSnapshot, registry.Register, g => registry.Unregister(g.RuleId), first, second);
        }

        private sealed class Command : AEcaCommand<IEcaRuleState, IEcaActionContext, int>
        {
            private readonly string _id;
            internal Command(string id) => _id = id;
            public override string Id => _id;
            public override Task Run(IEcaRuleState state, IEcaActionContext context, int args) => Task.CompletedTask;
        }

        [Test]
        public void Commands_FreezeMembership()
        {
            var registry = new EcaCommandRegistry();
            Membership(registry.GetSnapshot, registry.Register, c => registry.Unregister(c.Id), new Command("a"), new Command("b"));
        }

        [Test]
        public void States_ExposeOnlyFrozenIdsAcrossBothResolverShapes()
        {
            var registry = new EcaStateRegistry();
            var empty = registry.GetSnapshot();
            registry.Register("a", state => 1);
            var before = registry.GetSnapshot();
            registry.Register("b", (state, payload) => payload);
            Assert.That(empty, Is.Empty);
            Assert.That(before, Is.EqualTo(new[] { "a" }));
            Assert.That(registry.GetSnapshot(), Is.EquivalentTo(new[] { "a", "b" }));
            registry.Unregister("a");
            Assert.That(before, Is.EqualTo(new[] { "a" }));
            Assert.That(registry.GetSnapshot(), Is.EqualTo(new[] { "b" }));
            ReadOnly(before, "other");
        }

        private static EcaSystem System(string id) => new(id, new EcaSystemNamespace(id),
            new EcaBaseEventRegistry(), new EcaCommandRegistry(), new EcaStateRegistry());

        [Test]
        public void Systems_FreezeMembershipWithoutCopyingLocalRegistries()
        {
            var registry = new EcaSystemRegistry(); var first = System("a"); var second = System("b");
            Membership(registry.GetSnapshot, registry.Register, s => registry.Unregister(s.Id), first, second);
            var snapshot = registry.GetSnapshot();
            var e = new BaseTestSupport.Event<int>(); second.Events.Register(e);
            Assert.That(snapshot[0].Events.Resolve(e.Id), Is.SameAs(e));
        }

        [Test]
        public void Namespaces_FreezeMembership()
        {
            var registry = new EcaSystemNamespaceRegistry();
            Membership(registry.GetSnapshot, registry.Register, n => registry.Unregister(n.Id), new EcaSystemNamespace("a"), new EcaSystemNamespace("b"));
        }
    }
}
