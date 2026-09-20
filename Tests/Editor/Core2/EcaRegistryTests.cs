using System;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.BaseTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaRegistryTests
    {
        private EcaBaseEventRegistry _events;
        private EcaBaseRuleRegistry _rules;
        private Event<int> _event;

        [SetUp]
        public void SetUp()
        {
            _events = new EcaBaseEventRegistry();
            _rules = new EcaBaseRuleRegistry(_events);
            _event = new Event<int>();
            _events.Register(_event);
        }

        [Test]
        public void EventRegistry_CheckRegistered_UsesCanonicalInstance()
        {
            Assert.That(_events.CheckRegistered(_event), Is.True);
            Assert.That(_events.CheckRegistered(new Event<int>()), Is.False);
            Assert.That(_events.CheckRegistered(new Event<int> { Id = "other" }), Is.False);
            Assert.That(_events.CheckRegistered(new Event<string>()), Is.False);
            Assert.That(_events.CheckRegistered(null), Is.False);
            Assert.That(_events.CheckRegistered(new Event<int> { Id = null }), Is.False);
        }

        [Test]
        public void EventRegistry_Register_RejectsDuplicateId()
        {
            Assert.Throws<InvalidOperationException>(() => _events.Register(_event));
            Assert.Throws<InvalidOperationException>(() => _events.Register(new Event<int>()));
            Assert.Throws<InvalidOperationException>(() => _events.Register(new Event<string>()));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void EventRegistry_Register_RejectsEmptyId(string id)
        {
            Assert.Throws<ArgumentException>(() => _events.Register(new Event<int> { Id = id }));
        }

        [Test]
        public void EventRegistry_Register_RejectsNull_StoresMetadataWithoutTypedValidation()
        {
            Assert.Throws<ArgumentNullException>(() => _events.Register(null));
            var invalid = new Event<int> { Id = "invalid", EventStateType = typeof(string) };
            _events.Register(invalid);
            Assert.That(_events.CheckRegistered(invalid), Is.True);
        }

        [Test]
        public void RuleRegistry_Register_AllowsOptionalConditionAndPreservesOrder()
        {
            IEcaRuleRegistry registry = _rules;
            var first = new Rule { Id = "first", Event = _event };
            var second = new Rule { Id = "second", Event = _event };
            var other = new Event<int> { Id = "other" };
            _events.Register(other);
            registry.Register(first);
            registry.Register(new Rule { Id = "other.rule", Event = other });
            registry.Register(second);

            var matching = registry.GetByEvent<int, State>(_event);
            Assert.That(matching, Is.EqualTo(new[] { first, second }));
            Assert.That(matching[0].Condition, Is.Null);
        }

        [Test]
        public void RuleRegistry_Register_RejectsUnregisteredEvent()
        {
            Assert.Throws<InvalidOperationException>(() => _rules.Register(new Rule { Event = new Event<int> { Id = "missing" } }));
        }

        [Test]
        public void RuleRegistry_Register_RejectsDuplicateId()
        {
            var rule = new Rule { Event = _event };
            _rules.Register(rule);
            Assert.Throws<InvalidOperationException>(() => _rules.Register(rule));
            Assert.Throws<InvalidOperationException>(() => _rules.Register(new Rule { Event = _event }));
            Assert.That(_rules.GetByEvent<int, State>(_event), Has.Count.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void RuleRegistry_Register_RejectsEmptyId(string id)
        {
            Assert.Throws<ArgumentException>(() => _rules.Register(new Rule { Id = id, Event = _event }));
        }

        [Test]
        public void RuleRegistry_Register_RejectsMissingRequiredMembers()
        {
            Assert.Throws<ArgumentNullException>(() => _rules.Register<int, State>(null));
            Assert.Throws<ArgumentException>(() => _rules.Register(new Rule()));
            Assert.Throws<ArgumentException>(() => _rules.Register(new Rule { Event = _event, Action = null }));
        }

        [Test]
        public void RuleRegistry_Register_RejectsMismatchedEventContract()
        {
            var invalid = new Event<int> { Id = "string", EventStateType = typeof(string) };
            _events.Register(invalid);
            Assert.Throws<ArgumentException>(() => _rules.Register(new Rule { Event = invalid }));
        }

        [Test]
        public void RuleRegistry_Unregister_RequiresRegisteredInstance()
        {
            var rule = new Rule { Event = _event };
            _rules.Register(rule);
            Assert.That(_rules.Unregister(new Rule { Event = _event }), Is.False);
            Assert.That(_rules.GetByEvent<int, State>(_event), Is.EqualTo(new[] { rule }));
            Assert.That(_rules.Unregister(rule), Is.True);
            Assert.That(_rules.Unregister(rule), Is.False);
            Assert.That(_rules.GetByEvent<int, State>(_event), Is.Empty);
            _rules.Register(rule);
            Assert.That(_rules.Unregister(rule), Is.True);
            Assert.Throws<ArgumentNullException>(() => _rules.Unregister(null));
        }

        [Test]
        public void RuleRegistry_GetByEvent_RejectsIncompatibleStateAndEventSpecializations()
        {
            _rules.Register(new Rule { Event = _event });
            Assert.Throws<InvalidOperationException>(() =>
                _rules.GetByEvent<int, OtherState>(_event));
            Assert.That(_rules.GetByEvent(_event), Has.Count.EqualTo(1));
            _events.Unregister(_event.Id);
            var replacement = new Event<string> { Id = _event.Id };
            _events.Register(replacement);
            Assert.Throws<InvalidOperationException>(() => _rules.GetByEvent(replacement));
        }

        [Test]
        public void RuleRegistry_GetByEvent_ValidatesEventAndAllowsNoMatchingRules()
        {
            Assert.That(_rules.GetByEvent<int, State>(_event), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => _rules.GetByEvent<int, State>(null));
            Assert.Throws<InvalidOperationException>(() =>
                _rules.GetByEvent<int, State>(new Event<int> { Id = "missing" }));
            Assert.Throws<ArgumentNullException>(() => new EcaBaseRuleRegistry(null));
        }
    }
}
