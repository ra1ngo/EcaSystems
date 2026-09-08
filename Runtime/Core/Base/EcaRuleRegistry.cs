using System;
using System.Collections.Generic;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleRegistry : IEcaRuleRegistry
    {
        private readonly List<IEcaRule> _rules = new();
        private readonly HashSet<EcaRuleId> _registeredRuleIds = new();

        public IReadOnlyList<IEcaRule> Rules { get; }

        public EcaRuleRegistry()
        {
            Rules = _rules.AsReadOnly();
        }

        public void Register<TContext>(IEcaRule<TContext> rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            ValidateRuleContext<TContext>(rule.Event);

            if (!_registeredRuleIds.Add(rule.Id)) throw new InvalidOperationException($"Rule with id '{rule.Id}' is already registered.");

            try
            {
                _rules.Add(rule);
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
            if (!_rules.Remove(rule)) return false;
            _registeredRuleIds.Remove(rule.Id);
            return true;
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
