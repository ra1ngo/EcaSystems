using System;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;

namespace EcaSystems.Tests.Core1
{
    [TestFixture]
    public sealed class EcaRegistryTests
    {
        [Test]
        public void Events_RegisterExplicitly_UseIdAndExactDeclaredType()
        {
            var events = new EcaEventRegistry();
            var evt = new EcaEvent<int>("event", "Event");
            Assert.That(events.IsRegistered(evt), Is.False);
            events.Register(evt);
            Assert.That(events.IsRegistered(evt), Is.True);
            Assert.That(events.IsRegistered(new EcaEvent<int>("event", "Alias")), Is.True);
            Assert.That(events.IsRegistered(new EcaEvent<string>("event", "Wrong type")), Is.False);
            Assert.That(events.IsRegistered(null), Is.False);
            Assert.Throws<InvalidOperationException>(() => events.Register(evt));
            Assert.Throws<InvalidOperationException>(() => events.Register(new EcaEvent<int>("event", "Duplicate")));
            Assert.Throws<InvalidOperationException>(() => events.Register(new EcaEvent<string>("event", "Conflict")));
        }

        [Test]
        public void Dispatcher_UnknownOrConflictingFire_FailsBeforeNotificationAndNeverRegisters()
        {
            using var f = new BaseFixture();
            var events = f.Events;
            var dispatcher = f.Dispatcher;
            var notifications = 0;
            var evt = new EcaEvent<int>("event", "Event");
            Assert.Throws<InvalidOperationException>(() => dispatcher.Fire(evt, 1));
            Assert.That(events.IsRegistered(evt), Is.False);
            events.Register(evt);
            f.Rule("r", evt, _ => notifications++);
            Assert.Throws<InvalidOperationException>(() => dispatcher.Fire(new EcaEvent<string>("event", "Wrong"), "x"));
            Assert.That(notifications, Is.Zero);
            dispatcher.Fire(new EcaEvent<int>("event", "Alias"), 42);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void DishonestCustomEvent_MetadataIsValidatedAtEveryTypedBoundary()
        {
            var events = new EcaEventRegistry();
            var dishonest = new WrongEvent();
            Assert.Throws<ArgumentException>(() => events.Register(dishonest));
            events.Register(new EcaEvent<string>("wrong", "Canonical"));
            var dispatcher = new EcaEventDispatcher(events);
            Assert.Throws<ArgumentException>(() => dispatcher.Fire(dishonest, 1));
            Assert.Throws<ArgumentException>(() => new EcaRule<int>("r", "Rule", dishonest, NoOp()));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void Declarations_RejectEmptyIdentityAndName(string value)
        {
            var evt = new EcaEvent<int>("event", "Event");
            Assert.Throws<ArgumentException>(() => new EcaEvent<int>(value, "Name"));
            Assert.Throws<ArgumentException>(() => new EcaEvent<int>("id", value));
            Assert.Throws<ArgumentException>(() => new EcaRule<int>(value, "Name", evt, NoOp()));
            Assert.Throws<ArgumentException>(() => new EcaRule<int>("id", value, evt, NoOp()));
        }

        [Test]
        public void Rules_RequireEventAndAction_KeepOptionalConditionAndDirectReferences()
        {
            var evt = new EcaEvent<int>("event", "Event", null);
            var action = NoOp();
            Assert.Throws<ArgumentNullException>(() => new EcaRule<int>("r", "Rule", null, action));
            Assert.Throws<ArgumentNullException>(() => new EcaRule<int>("r", "Rule", evt, null));
            IEcaRule rule = new EcaRule<int>("r", "Rule", evt, action, description: null);
            Assert.That(rule.Event, Is.SameAs(evt));
            Assert.That(rule.Action, Is.SameAs(action));
            Assert.That(rule.Condition, Is.Null);
            Assert.That(rule.Description, Is.Empty);
            Assert.That(evt.Description, Is.Empty);
        }

        [Test]
        public void Rules_SelectSnapshotInOrder_RejectDuplicates_UnregisterByInstance()
        {
            using var f = new BaseFixture();
            var evt = f.Event<int>("event");
            var other = f.Event<int>("other");
            var first = f.Rule("one", evt, _ => { });
            var second = f.Rule("two", evt, _ => { });
            f.Rule("other", other, _ => { });
            var snapshot = f.Rules.GetByEvent(new EcaEvent<int>("event", "Alias"));
            CollectionAssert.AreEqual(new IEcaRule[] { first, second }, snapshot);
            Assert.Throws<InvalidOperationException>(() => f.Rules.Register(first));
            var replacement = new EcaRule<int>("one", "Replacement", evt, NoOp());
            Assert.That(f.Rules.Unregister(replacement), Is.False);
            Assert.That(f.Rules.Unregister(first), Is.True);
            Assert.That(f.Rules.Unregister(first), Is.False);
            f.Rules.Register(replacement);
            CollectionAssert.AreEqual(new IEcaRule[] { first, second }, snapshot);
            CollectionAssert.AreEqual(new IEcaRule[] { second, replacement }, f.Rules.GetByEvent(evt));
        }

        [Test]
        public void Rules_CannotImplicitlyRegisterAnEvent()
        {
            using var f = new BaseFixture();
            var evt = new EcaEvent<int>("unknown", "Unknown");
            var rule = new EcaRule<int>("r", "Rule", evt, NoOp());
            Assert.Throws<InvalidOperationException>(() => f.Rules.Register(rule));
            Assert.That(f.Events.IsRegistered(evt), Is.False);
            f.Events.Register(evt);
            f.Rules.Register(rule);
            Assert.That(f.Rules.GetByEvent(evt).Count, Is.EqualTo(1));
        }

        private static TestAction<EcaRuleState<int>> NoOp() => new((_, __) => Task.CompletedTask);

        private sealed class WrongEvent : IEcaEvent<int>
        {
            public string Id => "wrong";
            public string Name => "Wrong";
            public string Description => "";
            public Type EventStateType => typeof(string);
        }
    }
}
