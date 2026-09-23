using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.BaseTestSupport;
using Action = EcaSystems.Tests.Core2.BaseTestSupport.Action;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaBaseRuntimeTests
    {
        private Event<int> _event;
        private EcaBaseEventRegistry _events;
        private EcaBaseRuleRegistry _rules;
        private EcaBaseRuntime _runtime;

        [SetUp]
        public void SetUp()
        {
            _event = new Event<int>();
            _events = new EcaBaseEventRegistry();
            _events.Register(_event);
            _rules = new EcaBaseRuleRegistry(_events);
            _runtime = new EcaBaseRuntime(_rules, new EcaBaseActionRunner(), new EcaBaseConditionChecker());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ForceFire_RejectsNullOrWrongRuleIdentityBeforeCallbacks(bool nullState)
        {
            var callbacks = 0;
            _runtime.Register(new Rule
            {
                Id = "actual", Event = _event,
                Condition = new Condition { CheckHandler = (state, context) => { callbacks++; return true; } },
                Action = new Action { RunHandler = (state, context) => { callbacks++; return Task.CompletedTask; } }
            });
            var error = Assert.Throws<InvalidOperationException>(() => _runtime.ForceFire<int, State>(
                _event, 1, (rule, value) => nullState ? null : new State { RuleId = "foreign" }));
            Assert.That(error.Message, Does.Contain("actual"));
            Assert.That(callbacks, Is.Zero);
        }

        [Test]
        public void Fire_ChecksAllConditionsBeforeActions_InRegistryOrder()
        {
            var trace = new List<string>();
            var created = new List<string>();
            var states = new Dictionary<string, State>();
            var conditionContext = new ConditionContext();
            var actionContext = new ActionContext();
            var actionStarted = false;
            foreach (var id in new[] { "A", "B", "C" })
            {
                _runtime.Register(new Rule
                {
                    Id = id,
                    Event = _event,
                    Condition = new Condition
                    {
                        CheckHandler = (state, context) =>
                        {
                            trace.Add("Condition " + id);
                            Assert.That(actionStarted, Is.False, "Actions must not affect later conditions in the same fire.");
                            Assert.That(state, Is.SameAs(states[id]));
                            Assert.That(state.EventState, Is.EqualTo(42));
                            Assert.That(context, Is.SameAs(conditionContext));
                            return id != "B";
                        }
                    },
                    Action = new Action
                    {
                        RunHandler = (state, context) =>
                        {
                            actionStarted = true;
                            trace.Add("Action " + id);
                            Assert.That(state, Is.SameAs(states[id]));
                            Assert.That(context, Is.SameAs(actionContext));
                            return Task.CompletedTask;
                        }
                    }
                });
            }

            var otherEvent = new Event<int> { Id = "other" };
            _events.Register(otherEvent);
            _runtime.Register(new Rule
            {
                Id = "other.rule", Event = otherEvent,
                Condition = new Condition { CheckHandler = (state, context) => throw new AssertionException("Unmatched condition") },
                Action = new Action { RunHandler = (state, context) => throw new AssertionException("Unmatched action") }
            });

            _runtime.ForceFire<int, State>(_event, 42, (rule, eventState) =>
            {
                created.Add(rule.Id);
                var state = new State { RuleId = rule.Id, EventState = eventState };
                states.Add(rule.Id, state);
                return state;
            }, conditionContext, actionContext);

            Assert.That(created, Is.EqualTo(new[] { "A", "B", "C" }));
            Assert.That(trace, Is.EqualTo(new[] { "Condition A", "Condition B", "Condition C", "Action A", "Action C" }));
        }

        [Test]
        public void Fire_OptionalConditionRunsActionAfterOtherConditions()
        {
            var trace = new List<string>();
            _runtime.Register(new Rule
            {
                Id = "optional", Event = _event,
                Action = new Action { RunHandler = (state, context) => { trace.Add("Action optional"); return Task.CompletedTask; } }
            });
            _runtime.Register(new Rule
            {
                Id = "failed", Event = _event,
                Condition = new Condition { CheckHandler = (state, context) => { trace.Add("Condition failed"); return false; } },
                Action = new Action { RunHandler = (state, context) => throw new AssertionException("Failed rule action") }
            });

            _runtime.ForceFire<int, State>(
                _event, 7, (rule, value) => new State { RuleId = rule.Id, EventState = value }, new ConditionContext(), new ActionContext());

            Assert.That(trace, Is.EqualTo(new[] { "Condition failed", "Action optional" }));
        }

        [Test]
        public void Runtime_Unregister_RemovesRuleFromSubsequentFire()
        {
            var rule = new Rule { Event = _event };
            _runtime.Register(rule);
            Assert.That(_runtime.Unregister(rule), Is.True);
            Assert.That(_runtime.Unregister(rule), Is.False);
            _runtime.ForceFire<int, State>(
                _event, 0, (matching, value) => throw new AssertionException("No matching rule"),
                new ConditionContext(), new ActionContext());
        }

        [Test]
        public void Fire_RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _runtime.ForceFire<int, State>(
                null, 0, (rule, value) => new State(), new ConditionContext(), new ActionContext()));
            Assert.Throws<ArgumentNullException>(() => _runtime.ForceFire<int, State>(
                _event, 0, null, new ConditionContext(), new ActionContext()));
        }

        [Test]
        public void Runtime_RejectsMissingDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaBaseRuntime(null, new EcaBaseActionRunner(), new EcaBaseConditionChecker()));
            Assert.Throws<ArgumentNullException>(() => new EcaBaseRuntime(_rules, null, new EcaBaseConditionChecker()));
            Assert.Throws<ArgumentNullException>(() => new EcaBaseRuntime(_rules, new EcaBaseActionRunner(), null));
        }
    }
}
