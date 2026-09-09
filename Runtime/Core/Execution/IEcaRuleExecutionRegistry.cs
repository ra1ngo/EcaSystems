namespace EcaSystems.Core
{
    public interface IEcaRuleExecutionRegistry
    {
        void Register<TEventContext>(IEcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> rule,
            EcaRunMode runMode, IEcaRuleRunner ruleRunner);
        EcaRuleExecutionGroup<TEventContext> Get<TEventContext>(string ruleId);
        bool TryGet<TEventContext>(string ruleId, out EcaRuleExecutionGroup<TEventContext> group);
        bool Remove(string ruleId);
        void Clear();
    }
}
