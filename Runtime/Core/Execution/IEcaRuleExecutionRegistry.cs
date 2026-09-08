namespace EcaSystems.Core
{
    public interface IEcaRuleExecutionRegistry
    {
        void Register<TEventContext>(IEcaRule<EcaExecutionContext<TEventContext>> rule,
            EcaOverlap overlap, IEcaExecutionExecutor executor);
        EcaRuleExecutionGroup<TEventContext> Get<TEventContext>(string ruleId);
        bool TryGet<TEventContext>(string ruleId, out EcaRuleExecutionGroup<TEventContext> group);
        bool Remove(string ruleId);
        void Clear();
    }
}
