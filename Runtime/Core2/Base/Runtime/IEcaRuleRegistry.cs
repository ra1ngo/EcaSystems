using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public interface IEcaRuleRegistry
    {
        void Register<TEventState, TRuleState, TConditionContext, TActionContext>(
            IEcaRule<TEventState, TRuleState, TConditionContext, TActionContext> rule)
            where TRuleState : IEcaRuleState<TEventState>
            where TConditionContext : IEcaConditionContext
            where TActionContext : IEcaActionContext;

        bool Unregister(IEcaRule rule);

        IReadOnlyList<IEcaRule> GetByEvent(IEcaEvent ecaEvent);
    }
}