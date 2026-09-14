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

        public void Register<
            TEventState,
            TRuleState,
            TConditionContext,
            TActionContext>(
            IEcaRule<
                TEventState,
                TRuleState,
                TConditionContext,
                TActionContext> rule)
            where TRuleState : IEcaRuleState<TEventState>
            where TConditionContext : IEcaConditionContext
            where TActionContext : IEcaActionContext
        {
            ValidateRule<TEventState, TRuleState, TConditionContext, TActionContext>(rule);

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


        public IReadOnlyList<
            IEcaRule<
                TEventState,
                TRuleState,
                TConditionContext,
                TActionContext>>
            GetByEvent<
                TEventState,
                TRuleState,
                TConditionContext,
                TActionContext>(
                IEcaEvent<TEventState> ecaEvent)
            where TRuleState : IEcaRuleState<TEventState>
            where TConditionContext : IEcaConditionContext
            where TActionContext : IEcaActionContext
        {
            if (ecaEvent == null)
                throw new ArgumentNullException(nameof(ecaEvent));

            if (!_events.CheckRegistered(ecaEvent))
                throw new InvalidOperationException(
                    $"Event '{ecaEvent.Id}' is not registered.");

            var matching = new List<
                IEcaRule<
                    TEventState,
                    TRuleState,
                    TConditionContext,
                    TActionContext>>();

            foreach (var rule in _rules)
            {
                if (rule.Event.Id != ecaEvent.Id)
                    continue;

                if (rule is not IEcaRule<
                        TEventState,
                        TRuleState,
                        TConditionContext,
                        TActionContext> typedRule)
                {
                    throw new InvalidOperationException(
                        $"Rule '{rule.Id}' is incompatible with requested runtime types.");
                }

                matching.Add(typedRule);
            }

            return matching.AsReadOnly();
        }

        private void ValidateRule<
            TEventState,
            TRuleState,
            TConditionContext,
            TActionContext>(
            IEcaRule<
                TEventState,
                TRuleState,
                TConditionContext,
                TActionContext> rule)
            where TRuleState : IEcaRuleState<TEventState>
            where TConditionContext : IEcaConditionContext
            where TActionContext : IEcaActionContext
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new ArgumentException(
                    "Rule id cannot be empty.",
                    nameof(rule));

            if (rule.Event == null)
                throw new ArgumentException(
                    "Rule event is required.",
                    nameof(rule));

            if (rule.Action == null)
                throw new ArgumentException(
                    "Rule action is required.",
                    nameof(rule));

            if (!_events.CheckRegistered(rule.Event))
            {
                throw new InvalidOperationException(
                    $"Event '{rule.Event.Id}' is not registered.");
            }

            if (rule.Event.EventStateType != typeof(TEventState))
            {
                throw new ArgumentException(
                    "Event metadata disagrees with the rule generic contract.",
                    nameof(rule));
            }

            foreach (var existing in _rules)
            {
                if (existing.Id == rule.Id)
                {
                    throw new InvalidOperationException(
                        $"Rule '{rule.Id}' is already registered.");
                }
            }
        }
    }
}