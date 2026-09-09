using System;

namespace EcaSystems.Core
{
    public sealed class EcaRuleChecker : IEcaRuleChecker
    {
        public bool Check<TEventContext, TConditionContext, TActionContext>(IEcaRule<TEventContext, TConditionContext, TActionContext> rule, TConditionContext context)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            return rule.Condition == null || rule.Condition.Check(context);
        }
    }
}
