using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public interface IEcaExecutionGroupRegistry
    {
        IReadOnlyList<IEcaExecutionGroup> GetSnapshot();

        void Register(IEcaExecutionGroup group);
        bool Unregister(string ruleId);
        IEcaExecutionGroup Get(string ruleId);
        bool TryGet(string ruleId, out IEcaExecutionGroup group);

        IEcaExecutionGroup<E> Get<E>(string ruleId);
        bool TryGet<E>(string ruleId, out IEcaExecutionGroup<E> group);

        IEcaExecutionGroup<E, R> Get<E, R>(string ruleId)
            where R : IEcaExecutionRuleState<E>;

        bool TryGet<E, R>(string ruleId, out IEcaExecutionGroup<E, R> group)
            where R : IEcaExecutionRuleState<E>;
    }
}
