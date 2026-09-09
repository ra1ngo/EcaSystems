using System.Collections.Generic;

namespace EcaSystems.Core
{
    public interface IEcaRuleSelector
    {
        IReadOnlyList<IEcaRule<TEventContext, TConditionContext, TActionContext>> ForEvent<TEventContext, TConditionContext, TActionContext>(IEcaEvent ecaEvent)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>;
    }
}
