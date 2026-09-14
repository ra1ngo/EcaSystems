namespace EcaSystems.Core2
{
    public interface IEcaConditionChecker
    {
        bool Check<TRuleState, TConditionContext>(
            IEcaCondition<TRuleState, TConditionContext> condition,
            TRuleState state,
            TConditionContext context)
            where TRuleState : IEcaRuleState
            where TConditionContext : IEcaConditionContext;
    }
}