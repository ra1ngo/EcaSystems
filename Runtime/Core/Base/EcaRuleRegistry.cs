using System;
using System.Collections.Generic;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleRegistry : IEcaRuleRegistry
    {
        private readonly Dictionary<string, List<object>> _rulesByEventId = new();
        private readonly HashSet<EcaRuleId> _registeredRuleIds = new();

        public void Register<TContext>(IEcaRule<TContext> rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            ValidateRuleContext<TContext>(rule.Event);

            if (!_registeredRuleIds.Add(rule.Id)) throw new InvalidOperationException($"Rule with id '{rule.Id}' is already registered.");

            try
            {
                if (!_rulesByEventId.TryGetValue(rule.Event.Id, out var rules))
                {
                    rules = new List<object>();
                    _rulesByEventId.Add(rule.Event.Id, rules);
                }

                rules.Add(rule);
            }
            catch
            {
                _registeredRuleIds.Remove(rule.Id);
                throw;
            }
        }

        public bool Unregister<TContext>(IEcaRule<TContext> rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (!_rulesByEventId.TryGetValue(rule.Event.Id, out var rules)) return false;
            if (!rules.Remove(rule)) return false;

            _registeredRuleIds.Remove(rule.Id);

            if (rules.Count == 0) _rulesByEventId.Remove(rule.Event.Id);

            return true;
        }

        public IReadOnlyList<IEcaRule<TContext>> GetRulesForEvent<TContext>(IEcaEvent ecaEvent)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));

            ValidateRuleContext<TContext>(ecaEvent);

            if (!_rulesByEventId.TryGetValue(ecaEvent.Id, out var rules))
                return Array.Empty<IEcaRule<TContext>>();

            var result = new List<IEcaRule<TContext>>(rules.Count);

            for (var i = 0; i < rules.Count; i++)
            {
                if (rules[i] is IEcaRule<TContext> rule)
                    result.Add(rule);
            }

            return result;
        }

        private static void ValidateRuleContext<TContext>(IEcaEvent ecaEvent)
        {
            var eventContextType = EcaContextType.GetEventContextType<TContext>();

            if (eventContextType == ecaEvent.EventContextType)
                return;

            throw new InvalidOperationException(
                $"Context '{typeof(TContext)}' contains event context type " +
                $"'{eventContextType}', but event '{ecaEvent.Id}' expects " +
                $"'{ecaEvent.EventContextType}'."
            );
        }
    }
}