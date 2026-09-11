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
    public sealed class EcaRuleRegistryTests : AsyncTestFixture
    {
        [Test]
        public async Task Registry_CustomContexts_PreservesExactRolesAndVariance()
        {
            var evt = new EcaEvent<TestEventContext>("custom.context", "Custom context");
            var action = new BaseCountingAction<TestEventContext>();
            var rule = new EcaRule<TestEventContext, CustomConditionContext, CustomActionContext>(
                new EcaRuleConfig<TestEventContext, CustomConditionContext, CustomActionContext>
                {
                    Id = "custom.rule", Name = "Custom rule", Event = evt, Action = action,
                    Condition = new DelegateEcaCondition<CustomConditionContext>(context => context.EventContext.Value == 17)
                });
            var registry = new EcaRuleRegistry();
            registry.Register(rule);
            var selector = new EcaRuleSelector(registry);
            var selected = selector.ForEvent<TestEventContext, CustomConditionContext, CustomActionContext>(evt);
            Assert.That(selected.Count == 1 && ReferenceEquals(selected[0], rule), Is.True, "Общий Registry хранит Rule с пользовательской парой контекстов");
            Assert.That(new EcaRuleChecker().Check(rule, new CustomConditionContext(new TestEventContext(17))), Is.True, "Общий Checker проверяет пользовательский ConditionContext");
            await new EcaRuleRunner().Run(rule, new CustomActionContext(new TestEventContext(17)));
            Assert.That(action.LastValue == 17, Is.True, "Общий Runner выполняет пользовательский ActionContext");
            Assert.Catch<InvalidOperationException>(() =>
                selector.ForEvent<TestEventContext, EcaConditionContext<TestEventContext>, EcaActionContext<TestEventContext>>(evt), "Selector отклоняет другую пару role contexts");
            Assert.That(selector.ForEvent<TestEventContext, CustomConditionContext, CustomActionContext>(
                new EcaEvent<TestEventContext>("other", "Other")).Count, Is.Zero, "Selector проверяет Event.Id");
            Assert.Catch<InvalidOperationException>(() =>
                selector.ForEvent<object, EcaConditionContext<object>, EcaActionContext<object>>(evt), "Selector не расширяет payload до object");
            Assert.That(registry.Unregister(rule), Is.True, "Общий Registry удаляет пользовательскую Rule");

            Assert.That(!typeof(IEcaConditionContext<object>).IsAssignableFrom(typeof(IEcaConditionContext<string>)) &&
                !typeof(IEcaActionContext<object>).IsAssignableFrom(typeof(IEcaActionContext<string>)) &&
                !typeof(IEcaCommandsActionContext<object>).IsAssignableFrom(typeof(IEcaCommandsActionContext<string>)) &&
                !typeof(IEcaExecutionConditionContext<object>).IsAssignableFrom(typeof(IEcaExecutionConditionContext<string>)) &&
                !typeof(IEcaExecutionActionContext<object>).IsAssignableFrom(typeof(IEcaExecutionActionContext<string>)), Is.True, "Все role-интерфейсы invariant по payload");
            Assert.That(typeof(IEcaContext<object>).IsAssignableFrom(typeof(IEcaContext<string>)), Is.True, "Read-only IEcaContext сохраняет covariance");
        }

        private sealed class CustomConditionContext : EcaConditionContext<TestEventContext>
        {
            public CustomConditionContext(TestEventContext context) : base(context) { }
        }

        private sealed class CustomActionContext : EcaActionContext<TestEventContext>
        {
            public CustomActionContext(TestEventContext context) : base(context) { }
        }
    }
}
