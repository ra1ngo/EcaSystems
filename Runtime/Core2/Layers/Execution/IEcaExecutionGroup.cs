using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public interface IEcaExecutionGroup
    {
        string RuleId { get; }
        IEcaRule Rule { get; }
        EcaExecutionMode ExecutionMode { get; }
        EcaExecutionGroupState State { get; }
        IReadOnlyList<EcaExecution> Executions { get; }
    }

    public interface IEcaExecutionGroup<E, R, C, A> : IEcaExecutionGroup
        where R : IEcaExecutionRuleState<E>
        where C : IEcaExecutionConditionContext
        where A : IEcaExecutionActionContext
    {
        bool Check(E eventState, C context);
        Task Run(E eventState, A context);
    }
}
