using System;
using System.Collections.Generic;

namespace EcaSystems.Core
{
    public sealed class EcaRuleChecker : IEcaRuleChecker
    {
        public IReadOnlyList<IEcaRule<TContext>> Check<TContext>(
            IReadOnlyList<IEcaRule<TContext>> rules,
            Func<IEcaRule<TContext>, TContext> contextFactory)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            if (contextFactory == null)
                throw new ArgumentNullException(nameof(contextFactory));

            var result = new List<IEcaRule<TContext>>(rules.Count);

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var context = contextFactory(rule);

                if (rule.Condition == null || rule.Condition.Check(context))
                    result.Add(rule);
            }

            return result;
        }
    }
}