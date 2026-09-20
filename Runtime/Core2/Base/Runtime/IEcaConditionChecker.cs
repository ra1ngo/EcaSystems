namespace EcaSystems.Core2
{
    public interface IEcaConditionChecker
    {
        bool Check<R>(IEcaCondition<R> condition, R state, IEcaConditionContext context)
            where R : IEcaRuleState;
    }
}
