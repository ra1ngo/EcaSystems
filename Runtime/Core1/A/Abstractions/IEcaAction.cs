using System.Threading.Tasks;

namespace EcaSystems.Core1
{
    public interface IEcaAction
    {
        string Id { get; }
        string Name { get; }
        string Description { get; }
    }

    public interface IEcaAction<in TRuleState, in TRunnerContext> : IEcaAction
        where TRuleState : IEcaRuleState
        where TRunnerContext : IEcaActionRunnerContext
    {
        Task Run(TRuleState state, TRunnerContext runnerContext);
    }
}
