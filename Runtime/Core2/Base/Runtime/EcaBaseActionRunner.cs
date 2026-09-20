using System;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseActionRunner : IEcaActionRunner
    {
        public Task Run<R>(IEcaAction<R> action, R state, IEcaActionContext context)
            where R : IEcaRuleState
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return action.Run(state, context);
        }
    }
}
