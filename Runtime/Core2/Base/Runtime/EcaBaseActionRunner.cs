using System;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseActionRunner : IEcaActionRunner
    {
        public Task Run<R, A>(IEcaAction<R, A> action, R state, A context)
            where R : IEcaRuleState
            where A : IEcaActionContext
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return action.Run(state, context);
        }
    }
}
