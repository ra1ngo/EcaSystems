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

        public void Register<E, R>(IEcaRule<E, R> rule)
            where R : IEcaRuleState<E>
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

        public IReadOnlyList<IEcaRule<E>> GetByEvent<E>(IEcaEvent<E> ecaEvent)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (!_events.CheckRegistered(ecaEvent))
                throw new InvalidOperationException($"Event '{ecaEvent.Id}' is not registered.");
            if (ecaEvent.EventStateType != typeof(E))
                throw new ArgumentException("Event metadata disagrees with its generic contract.", nameof(ecaEvent));

            var matching = new List<IEcaRule<E>>();
            foreach (var rule in _rules)
            {
                if (rule.Event.Id != ecaEvent.Id) continue;
                if (rule is not IEcaRule<E> typedRule)
                    throw new InvalidOperationException($"Rule '{rule.Id}' is incompatible with requested event type.");
                matching.Add(typedRule);
            }
            return matching.AsReadOnly();
        }

        // Caller-state Base Fire still requires one compatible R before checking any Conditions.
        public IReadOnlyList<IEcaRule<E, R>> GetByEvent<E, R>(IEcaEvent<E> ecaEvent)
            where R : IEcaRuleState<E>
        {
            var matching = new List<IEcaRule<E, R>>();
            foreach (var rule in GetByEvent(ecaEvent))
            {
                if (rule is not IEcaRule<E, R> typedRule)
                    throw new InvalidOperationException($"Rule '{rule.Id}' is incompatible with requested state type.");
                matching.Add(typedRule);
            }
            return matching.AsReadOnly();
        }

        private void ValidateRule<E, R>(IEcaRule<E, R> rule)
            where R : IEcaRuleState<E>
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
