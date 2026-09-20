using System;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseConditionChecker : IEcaConditionChecker
    {
        public bool Check<R>(IEcaCondition<R> condition, R state, IEcaConditionContext context)
            where R : IEcaRuleState
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            return condition.Check(state, context);
        }
    }
}
