using System;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRule<TContext> : IEcaRule<TContext>
    {
        public EcaRuleId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public IEcaEvent Event { get; }
        public IEcaCondition<TContext> Condition { get; }
        public IEcaAction<TContext> Action { get; }

        public EcaRule(EcaRuleConfig<TContext> config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(config.Id))
                throw new ArgumentException("Rule id cannot be empty.", nameof(config));

            if (string.IsNullOrWhiteSpace(config.Name))
                throw new ArgumentException("Rule name cannot be empty.", nameof(config));

            if (config.Event == null)
                throw new ArgumentNullException(nameof(config.Event));

            if (config.Action == null)
                throw new ArgumentNullException(nameof(config.Action));

            var ruleEventContextType = EcaContextType.GetEventContextType<TContext>();

            if (ruleEventContextType != config.Event.EventContextType)
            {
                throw new ArgumentException(
                    $"Rule context '{typeof(TContext)}' contains event context type " +
                    $"'{ruleEventContextType}', but event '{config.Event.Id}' expects " +
                    $"'{config.Event.EventContextType}'.",
                    nameof(config)
                );
            }

            Id = config.Id;
            Name = config.Name;
            Description = config.Description ?? string.Empty;
            Event = config.Event;
            Condition = config.Condition;
            Action = config.Action;
        }
    }
}