using System;
using System.Threading.Tasks;

namespace EcaSystems.Core1
{
    public sealed class EcaActionRunner
    {
        public Task Run<TRuleState, TRunnerContext>(IEcaAction<TRuleState, TRunnerContext> action,
            TRuleState state, TRunnerContext runnerContext)
            where TRuleState : IEcaRuleState
            where TRunnerContext : IEcaActionRunnerContext
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return action.Run(state, runnerContext);
        }
    }
}
