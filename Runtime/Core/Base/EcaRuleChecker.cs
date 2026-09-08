using System;

namespace EcaSystems.Core
{
    public sealed class EcaRuleChecker : IEcaRuleChecker
    {
        public bool Check<TContext>(IEcaRule<TContext> rule, TContext context)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            return rule.Condition == null || rule.Condition.Check(context);
        }
    }
}
