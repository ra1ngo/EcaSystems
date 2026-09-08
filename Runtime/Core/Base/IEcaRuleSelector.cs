using System.Collections.Generic;

namespace EcaSystems.Core
{
    public interface IEcaRuleSelector
    {
        IReadOnlyList<IEcaRule<TContext>> ForEvent<TContext>(IEcaEvent ecaEvent);
    }
}
