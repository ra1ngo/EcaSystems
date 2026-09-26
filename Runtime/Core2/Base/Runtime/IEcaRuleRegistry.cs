using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public interface IEcaRuleRegistry
    {
        IReadOnlyList<IEcaRule> GetSnapshot();

        void Register<E, R>(IEcaRule<E, R> rule)
            where R : IEcaRuleState<E>;

        bool Unregister(IEcaRule rule);

        IReadOnlyList<IEcaRule<E>> GetByEvent<E>(IEcaEvent<E> ecaEvent);

        IReadOnlyList<IEcaRule<E, R>> GetByEvent<E, R>(IEcaEvent<E> ecaEvent)
            where R : IEcaRuleState<E>;
    }
}
