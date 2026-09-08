using System;
using System.Collections.Generic;

namespace EcaSystems.Core
{
    public sealed class EcaRuleSelector : IEcaRuleSelector
    {
        private readonly IEcaRuleRegistry _ruleRegistry;

        public EcaRuleSelector(IEcaRuleRegistry ruleRegistry)
        {
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
        }

        public IReadOnlyList<IEcaRule<TContext>> ForEvent<TContext>(IEcaEvent ecaEvent)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (EcaContextType.GetEventContextType<TContext>() != ecaEvent.EventContextType)
                throw new InvalidOperationException($"Context '{typeof(TContext)}' is incompatible with event '{ecaEvent.Id}'.");

            var result = new List<IEcaRule<TContext>>();
            var rules = _ruleRegistry.Rules;
            for (var i = 0; i < rules.Count; i++)
            {
                var candidate = rules[i];
                if (candidate.Event.Id != ecaEvent.Id) continue;
                if (candidate.Event.EventContextType != ecaEvent.EventContextType ||
                    !(candidate is IEcaRule<TContext> rule))
                    throw new InvalidOperationException($"Rule '{candidate.Id}' is incompatible with context '{typeof(TContext)}' for event '{ecaEvent.Id}'.");
                result.Add(rule);
            }

            return result;
        }
    }
}
