namespace EcaSystems.Core2
{
    public interface IEcaConditionChecker
    {
        bool Check<R, C>(IEcaCondition<R, C> condition, R state, C context)
            where R : IEcaRuleState
            where C : IEcaConditionContext;
    }
}
