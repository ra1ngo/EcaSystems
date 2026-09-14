using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public interface IEcaRuleRegistry
    {
        void Register<E, R, C, A>(IEcaRule<E, R, C, A> rule)
            where R : IEcaRuleState<E>
            where C : IEcaConditionContext
            where A : IEcaActionContext;

        bool Unregister(IEcaRule rule);

        IReadOnlyList<IEcaRule<E, R, C, A>> GetByEvent<E, R, C, A>(IEcaEvent<E> ecaEvent)
            where R : IEcaRuleState<E>
            where C : IEcaConditionContext
            where A : IEcaActionContext;
    }
}
