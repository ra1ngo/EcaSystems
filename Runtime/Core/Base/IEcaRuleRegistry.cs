using System.Collections.Generic;

namespace EcaSystems.Core
{
    public interface IEcaRuleRegistry
    {
        void Register<TContext>(IEcaRule<TContext> rule);
        bool Unregister<TContext>(IEcaRule<TContext> rule);
        IReadOnlyList<IEcaRule<TContext>> GetRulesForEvent<TContext>(IEcaEvent ecaEvent);
    }
}