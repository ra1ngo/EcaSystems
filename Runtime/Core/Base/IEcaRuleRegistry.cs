using System.Collections.Generic;

namespace EcaSystems.Core
{
    public interface IEcaRuleRegistry
    {
        void Register<TEventContext, TConditionContext, TActionContext>(IEcaRule<TEventContext, TConditionContext, TActionContext> rule)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>;
        bool Unregister<TEventContext, TConditionContext, TActionContext>(IEcaRule<TEventContext, TConditionContext, TActionContext> rule)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>;
        IReadOnlyList<IEcaRule> Rules { get; }
    }
}
