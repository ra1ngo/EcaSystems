using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;
using NUnit.Framework;
using EcaSystems.Tests.Support;
using static EcaSystems.Tests.Support.ExecutionRules;
using static EcaSystems.Tests.Support.AsyncAssert;

namespace EcaSystems.Tests
{
    [TestFixture]
    public sealed class EcaBaseEngineTests : AsyncTestFixture
    {
        [Test]
        public void Fire_EmptyContext_RunsUntilUnregistered()
        {


            var engine = new EcaBaseEngine();

            var testEvent = new EcaEvent<EcaEventContextEmpty>(
                "test.base.empty",
                "Base Empty Event"
            );

            var action = new BaseCountingAction<EcaEventContextEmpty>();

            var rule = new EcaRule<EcaEventContextEmpty, EcaConditionContext<EcaEventContextEmpty>, EcaActionContext<EcaEventContextEmpty>>(
                new EcaRuleConfig<EcaEventContextEmpty, EcaConditionContext<EcaEventContextEmpty>, EcaActionContext<EcaEventContextEmpty>>
                {
                    Id = "test.base.empty.rule",
                    Name = "Base Empty Rule",
                    Event = testEvent,
                    Action = action
                }
            );

            engine.Register(rule);

            engine.Fire(testEvent);
            engine.Fire(testEvent);

            Assert.That(action.RunCount == 2, Is.True, "Base empty event runs Action on every Fire");

            Assert.That(engine.Unregister(rule), Is.True, "Base unregister succeeds");

            engine.Fire(testEvent);

            Assert.That(action.RunCount == 2, Is.True, "Unregistered Rule no longer runs");
        }

        [Test]
        public void Fire_PassesEventContextToAction()
        {


            var engine = new EcaBaseEngine();

            var testEvent = new EcaEvent<TestEventContext>(
                "test.base.context",
                "Base Context Event"
            );

            var action = new BaseCountingAction<TestEventContext>();

            var rule = new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                {
                    Id = "test.base.context.rule",
                    Name = "Base Context Rule",
                    Event = testEvent,
                    Action = action
                }
            );

            engine.Register(rule);

            engine.Fire(
                testEvent,
                new TestEventContext(42)
            );

            Assert.That(action.LastValue == 42, Is.True, "Base Action receives EventContext");
        }

        [Test]
        public void Fire_FalseCondition_PreventsAction()
        {


            var engine = new EcaBaseEngine();

            var testEvent = new EcaEvent<TestEventContext>(
                "test.base.condition-false",
                "Condition False Event"
            );

            var action = new BaseCountingAction<TestEventContext>();

            var condition =
                new DelegateEcaCondition<EcaConditionContext<TestEventContext>>(
                    _ => false
                );

            var rule = new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                {
                    Id = "test.base.condition-false.rule",
                    Name = "Condition False Rule",
                    Event = testEvent,
                    Condition = condition,
                    Action = action
                }
            );

            engine.Register(rule);

            engine.Fire(
                testEvent,
                new TestEventContext(1)
            );

            Assert.That(action.RunCount == 0, Is.True, "Condition false prevents Action");
        }

        [Test]
        public void Fire_ChecksAllConditionsBeforeAnyAction()
        {


            var engine = new EcaBaseEngine();

            var testEvent = new EcaEvent<TestEventContext>(
                "test.base.condition-order",
                "Condition Order Event"
            );

            var sharedState = new SharedState();

            var firstRule =
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                    {
                        Id = "test.base.condition-order.first",
                        Name = "First Rule",
                        Event = testEvent,
                        Action = new SetFlagAction(sharedState)
                    }
                );

            var secondAction =
                new BaseCountingAction<TestEventContext>();

            var secondRule =
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                    {
                        Id = "test.base.condition-order.second",
                        Name = "Second Rule",
                        Event = testEvent,
                        Condition = new FlagMustBeFalseCondition(sharedState),
                        Action = secondAction
                    }
                );

            engine.Register(firstRule);
            engine.Register(secondRule);

            engine.Fire(
                testEvent,
                new TestEventContext(1)
            );

            Assert.That(sharedState.Value, Is.True, "First Action changed shared state");

            Assert.That(secondAction.RunCount == 1, Is.True, "Second Condition was checked before First Action");
        }

        [Test]
        public void Register_DuplicateRuleId_Throws()
        {


            var engine = new EcaBaseEngine();

            var testEvent = new EcaEvent<TestEventContext>(
                "test.base.duplicate",
                "Duplicate Rule Event"
            );

            var rule1 =
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                    {
                        Id = "same.rule.id",
                        Name = "Rule One",
                        Event = testEvent,
                        Action = new BaseCountingAction<TestEventContext>()
                    }
                );

            var rule2 =
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                    {
                        Id = "same.rule.id",
                        Name = "Rule Two",
                        Event = testEvent,
                        Action = new BaseCountingAction<TestEventContext>()
                    }
                );

            engine.Register(rule1);

            var exceptionThrown = false;

            try
            {
                engine.Register(rule2);
            }
            catch (InvalidOperationException)
            {
                exceptionThrown = true;
            }

            Assert.That(exceptionThrown, Is.True, "Duplicate RuleId is rejected");
        }

        [Test]
        public void Fire_SeparatesConditionAndActionContexts()
        {
            var evt = new EcaEvent<TestEventContext>("split", "Split");
            var payload = new TestEventContext(73);
            var action = new BaseCountingAction<TestEventContext>();
            var seen = 0;
            var rule = new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                {
                    Id = "split", Name = "Split", Event = evt, Action = action,
                    Condition = new DelegateEcaCondition<EcaConditionContext<TestEventContext>>(context =>
                    {
                        seen = context.EventContext.Value;
                        return true;
                    })
                });
            var engine = new EcaBaseEngine();
            engine.Register(rule);
            engine.Fire(evt, payload);
            Assert.That(seen == 73 && action.LastValue == 73, Is.True, "Разные Base contexts получают один payload");
            Assert.That(!typeof(IEcaCommandsActionContext).IsAssignableFrom(typeof(EcaConditionContext<TestEventContext>)) &&
                !typeof(IEcaCommandsActionContext).IsAssignableFrom(typeof(EcaExecutionConditionContext<TestEventContext>)) &&
                typeof(EcaExecutionConditionContext<TestEventContext>).GetProperty("Commands") == null &&
                typeof(IEcaExecutionConditionContext<TestEventContext>).GetProperty("Commands") == null, Is.True, "Condition не предоставляет Commands");
            Assert.Catch<ArgumentException>(() =>
                new EcaRule<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(
                    new EcaRuleConfig<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>
                    {
                        Id = "wrong", Name = "Wrong", Event = new EcaEvent<int>("wrong", "Wrong"), Action = action
                    }), "Rule проверяет явный тип payload");
        }

        private sealed class SharedState
        {
            public bool Value;
        }

        private sealed class FlagMustBeFalseCondition
            : IEcaCondition<EcaConditionContext<TestEventContext>>
        {
            private readonly SharedState _state;

            public FlagMustBeFalseCondition(
                SharedState state)
            {
                _state = state;
            }

            public bool Check(
                EcaConditionContext<TestEventContext> context)
            {
                return !_state.Value;
            }
        }

        private sealed class SetFlagAction
            : IEcaAction<EcaActionContext<TestEventContext>>
        {
            private readonly SharedState _state;

            public SetFlagAction(
                SharedState state)
            {
                _state = state;
            }

            public Task Run(
                EcaActionContext<TestEventContext> context)
            {
                _state.Value = true;

                return Task.CompletedTask;
            }
        }
    }
}
