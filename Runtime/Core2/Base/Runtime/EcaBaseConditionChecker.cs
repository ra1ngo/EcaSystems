using System;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseConditionChecker : IEcaConditionChecker
    {
        public bool Check<TRuleState, TConditionContext>(IEcaCondition<TRuleState, TConditionContext> condition,
            TRuleState state, TConditionContext context)
            where TRuleState : IEcaRuleState
            where TConditionContext : IEcaConditionContext
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            return condition.Check(state, context);
        }
    }
}
