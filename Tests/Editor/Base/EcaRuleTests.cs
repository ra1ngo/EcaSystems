using System;
using EcaSystems.Core;
using EcaSystems.Tests.Support;
using NUnit.Framework;

namespace EcaSystems.Tests
{
    [TestFixture]
    public sealed class EcaRuleTests
    {
        [Test]
        public void Construct_ValidConfig_PreservesDefinitionAndOptionalCondition()
        {
            var config = ValidConfig();
            var rule = new EcaRule<int, EcaConditionContext<int>, EcaActionContext<int>>(config);
            Assert.That(rule.Id, Is.EqualTo(config.Id));
            Assert.That(rule.Name, Is.EqualTo(config.Name));
            Assert.That(rule.Event, Is.SameAs(config.Event));
            Assert.That(rule.Action, Is.SameAs(config.Action));
            Assert.That(rule.Condition, Is.Null);
            Assert.That(rule.Description, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void Construct_EmptyIdOrName_Throws(string value)
        {
            var config = ValidConfig();
            config.Id = value;
            Assert.Throws<ArgumentException>(() => new EcaRule<int, EcaConditionContext<int>, EcaActionContext<int>>(config));
            config = ValidConfig();
            config.Name = value;
            Assert.Throws<ArgumentException>(() => new EcaRule<int, EcaConditionContext<int>, EcaActionContext<int>>(config));
        }

        [Test]
        public void Construct_MissingRequiredReference_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaRule<int, EcaConditionContext<int>, EcaActionContext<int>>(null));
            var config = ValidConfig();
            config.Event = null;
            Assert.Throws<ArgumentNullException>(() => new EcaRule<int, EcaConditionContext<int>, EcaActionContext<int>>(config));
            config = ValidConfig();
            config.Action = null;
            Assert.Throws<ArgumentNullException>(() => new EcaRule<int, EcaConditionContext<int>, EcaActionContext<int>>(config));
        }

        [Test]
        public void Select_DifferentEventInstanceWithSameId_MatchesRule()
        {
            var rule = new EcaRule<int, EcaConditionContext<int>, EcaActionContext<int>>(ValidConfig());
            var registry = new EcaRuleRegistry();
            registry.Register(rule);
            var selected = new EcaRuleSelector(registry).ForEvent<int, EcaConditionContext<int>, EcaActionContext<int>>(
                new EcaEvent<int>(rule.Event.Id, "Another instance"));
            Assert.That(selected, Has.Count.EqualTo(1));
            Assert.That(selected[0], Is.SameAs(rule));
        }

        private static EcaRuleConfig<int, EcaConditionContext<int>, EcaActionContext<int>> ValidConfig() => new()
        {
            Id = "rule", Name = "Rule", Event = new EcaEvent<int>("event", "Event"),
            Action = new BaseCountingAction<int>()
        };
    }
}
