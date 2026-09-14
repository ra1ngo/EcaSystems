using System;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseConditionChecker : IEcaConditionChecker
    {
        public bool Check<R, C>(IEcaCondition<R, C> condition, R state, C context)
            where R : IEcaRuleState
            where C : IEcaConditionContext
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            return condition.Check(state, context);
        }
    }
}
