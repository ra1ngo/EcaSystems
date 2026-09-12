using System;
using System.Collections.Generic;

namespace EcaSystems.Core1
{
    public sealed class EcaRuleRegistry
    {
        private readonly List<IEcaRule> _rules = new();
        private readonly EcaEventRegistry _events;

        public EcaRuleRegistry(EcaEventRegistry events)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public void Register<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext>(
            IEcaRule<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext> rule)
            where TRuleState : IEcaRuleState<TEventState>
            where TConditionRunnerContext : IEcaConditionRunnerContext
            where TActionRunnerContext : IEcaActionRunnerContext
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (string.IsNullOrWhiteSpace(rule.Id)) throw new ArgumentException("Rule id cannot be empty.", nameof(rule));
            if (rule.Action == null) throw new ArgumentException("Rule action is required.", nameof(rule));
            _events.RequireRegistered(rule.Event);
            if (rule.Event.EventStateType != typeof(TEventState))
                throw new ArgumentException("Event metadata disagrees with the rule generic contract.", nameof(rule));
            foreach (var existing in _rules)
                if (existing.Id == rule.Id) throw new InvalidOperationException($"Rule '{rule.Id}' already registered.");
            _rules.Add(rule);
        }

        public bool Unregister(IEcaRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            for (var i = 0; i < _rules.Count; i++)
                if (ReferenceEquals(_rules[i], rule))
                {
                    _rules.RemoveAt(i);
                    return true;
                }
            return false;
        }

        public IReadOnlyList<IEcaRule> GetByEvent(IEcaEvent ecaEvent)
        {
            _events.RequireRegistered(ecaEvent);
            var matching = new List<IEcaRule>();
            foreach (var rule in _rules)
                if (rule.Event.Id == ecaEvent.Id)
                {
                    if (rule.Event.EventStateType != ecaEvent.EventStateType)
                        throw new InvalidOperationException($"Rule '{rule.Id}' has an incompatible event state type.");
                    matching.Add(rule);
                }
            /* A snapshot keeps nested Fire and registration changes local to each pipeline. */
            return matching.AsReadOnly();
        }
    }
}
