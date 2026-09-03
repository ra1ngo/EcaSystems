using System;
using System.Collections.Generic;

namespace EcaSystems.Core
{
    public interface IEcaRuleChecker
    {
        IReadOnlyList<IEcaRule<TContext>> Check<TContext>(
            IReadOnlyList<IEcaRule<TContext>> rules,
            Func<IEcaRule<TContext>, TContext> contextFactory
        );
    }
}