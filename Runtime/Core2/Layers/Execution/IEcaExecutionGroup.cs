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

    public interface IEcaExecutionGroup<E> : IEcaExecutionGroup
    {
        bool Check(E eventState, IEcaConditionContext context);
        Task Run(E eventState, IEcaActionContext context);
    }

    public interface IEcaExecutionGroup<E, R> : IEcaExecutionGroup<E>
        where R : IEcaExecutionRuleState<E>
    {
    }
}
