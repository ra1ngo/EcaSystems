using System;

namespace EcaSystems.Core1
{
    public sealed class EcaConditionRunner
    {
        public bool Check<TRuleState, TRunnerContext>(IEcaCondition<TRuleState, TRunnerContext> condition,
            TRuleState state, TRunnerContext runnerContext)
            where TRuleState : IEcaRuleState
            where TRunnerContext : IEcaConditionRunnerContext
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            return condition.Check(state, runnerContext);
        }
    }
}
