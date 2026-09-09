using System;
using System.Threading.Tasks;

namespace EcaSystems.Core
{
    public sealed class EcaRuleRunner : IEcaRuleRunner
    {
        public Task Run<TEventContext, TConditionContext, TActionContext>(IEcaRule<TEventContext, TConditionContext, TActionContext> rule, TActionContext context)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            return rule.Action.Run(context);
        }
    }
}
