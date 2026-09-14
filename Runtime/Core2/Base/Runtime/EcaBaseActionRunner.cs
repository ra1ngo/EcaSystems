using System;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseActionRunner : IEcaActionRunner
    {
        public Task Run<TRuleState, TActionContext>(IEcaAction<TRuleState, TActionContext> action,
            TRuleState state, TActionContext context)
            where TRuleState : IEcaRuleState
            where TActionContext : IEcaActionContext
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return action.Run(state, context);
        }
    }
}
