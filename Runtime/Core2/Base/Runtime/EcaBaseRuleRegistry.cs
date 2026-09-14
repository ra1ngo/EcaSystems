using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseRuleRegistry : IEcaRuleRegistry
    {
        private readonly List<IEcaRule> _rules = new();
        private readonly IEcaEventRegistry _events;

        public EcaBaseRuleRegistry(IEcaEventRegistry events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public void Register<E, R, C, A>(IEcaRule<E, R, C, A> rule)
            where R : IEcaRuleState<E>
            where C : IEcaConditionContext
            where A : IEcaActionContext
        {
            ValidateRule(rule);
            _rules.Add(rule);
        }

        public bool Unregister(IEcaRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            for (var i = 0; i < _rules.Count; i++)
            {
                if (!ReferenceEquals(_rules[i], rule)) continue;
                _rules.RemoveAt(i);
                return true;
            }

            return false;
        }

        public IReadOnlyList<IEcaRule<E, R, C, A>> GetByEvent<E, R, C, A>(IEcaEvent<E> ecaEvent)
            where R : IEcaRuleState<E>
            where C : IEcaConditionContext
            where A : IEcaActionContext
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (!_events.CheckRegistered(ecaEvent))
                throw new InvalidOperationException($"Event '{ecaEvent.Id}' is not registered.");

            var matching = new List<IEcaRule<E, R, C, A>>();
            foreach (var rule in _rules)
            {
                if (rule.Event.Id != ecaEvent.Id) continue;
                if (rule is not IEcaRule<E, R, C, A> typedRule)
                    throw new InvalidOperationException($"Rule '{rule.Id}' is incompatible with requested runtime types.");

                matching.Add(typedRule);
            }

            return matching.AsReadOnly();
        }

        private void ValidateRule<E, R, C, A>(IEcaRule<E, R, C, A> rule)
            where R : IEcaRuleState<E>
            where C : IEcaConditionContext
            where A : IEcaActionContext
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (string.IsNullOrWhiteSpace(rule.Id)) throw new ArgumentException("Rule id cannot be empty.", nameof(rule));
            if (rule.Event == null) throw new ArgumentException("Rule event is required.", nameof(rule));
            if (rule.Action == null) throw new ArgumentException("Rule action is required.", nameof(rule));
            if (!_events.CheckRegistered(rule.Event))
                throw new InvalidOperationException($"Event '{rule.Event.Id}' is not registered.");
            if (rule.Event.EventStateType != typeof(E))
                throw new ArgumentException("Event metadata disagrees with the rule generic contract.", nameof(rule));

            foreach (var existing in _rules)
            {
                if (existing.Id == rule.Id)
                    throw new InvalidOperationException($"Rule '{rule.Id}' is already registered.");
            }
        }
    }
}
