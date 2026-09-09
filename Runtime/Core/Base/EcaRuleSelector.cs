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

        public IReadOnlyList<IEcaRule<TEventContext, TConditionContext, TActionContext>> ForEvent<TEventContext, TConditionContext, TActionContext>(IEcaEvent ecaEvent)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (typeof(TEventContext) != ecaEvent.EventContextType)
                throw new InvalidOperationException($"Context '{typeof(TEventContext)}' is incompatible with event '{ecaEvent.Id}'.");

            var result = new List<IEcaRule<TEventContext, TConditionContext, TActionContext>>();
            var rules = _ruleRegistry.Rules;
            for (var i = 0; i < rules.Count; i++)
            {
                var candidate = rules[i];
                if (candidate.Event.Id != ecaEvent.Id) continue;
                if (candidate.Event.EventContextType != ecaEvent.EventContextType ||
                    !(candidate is IEcaRule<TEventContext, TConditionContext, TActionContext> rule))
                    throw new InvalidOperationException($"Rule '{candidate.Id}' is incompatible with condition context '{typeof(TConditionContext)}' and action context '{typeof(TActionContext)}' for event '{ecaEvent.Id}'.");
                result.Add(rule);
            }

            return result;
        }
    }
}
