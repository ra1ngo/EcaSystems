namespace EcaSystems.Core2
{
    public interface IEcaExecutionGroupRegistry
    {
        void Register(IEcaExecutionGroup group);
        bool Unregister(string ruleId);
        IEcaExecutionGroup Get(string ruleId);
        bool TryGet(string ruleId, out IEcaExecutionGroup group);

        IEcaExecutionGroup<E, R, C, A> Get<E, R, C, A>(string ruleId)
            where R : IEcaExecutionRuleState<E>
            where C : IEcaExecutionConditionContext
            where A : IEcaExecutionActionContext;

        bool TryGet<E, R, C, A>(string ruleId, out IEcaExecutionGroup<E, R, C, A> group)
            where R : IEcaExecutionRuleState<E>
            where C : IEcaExecutionConditionContext
            where A : IEcaExecutionActionContext;
    }
}
