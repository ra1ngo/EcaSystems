using System;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRule<TEventContext, TConditionContext, TActionContext> : IEcaRule<TEventContext, TConditionContext, TActionContext>
        where TConditionContext : IEcaConditionContext<TEventContext>
        where TActionContext : IEcaActionContext<TEventContext>
    {
        public EcaRuleId Id { get; }
        public string Name { get; }
        public string Description { get; }
        public IEcaEvent Event { get; }
        public IEcaCondition<TConditionContext> Condition { get; }
        public IEcaAction<TActionContext> Action { get; }

        public EcaRule(EcaRuleConfig<TEventContext, TConditionContext, TActionContext> config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.Id)) throw new ArgumentException("Rule id cannot be empty.", nameof(config));
            if (string.IsNullOrWhiteSpace(config.Name)) throw new ArgumentException("Rule name cannot be empty.", nameof(config));
            if (config.Event == null) throw new ArgumentNullException(nameof(config.Event));
            if (config.Action == null) throw new ArgumentNullException(nameof(config.Action));

            var ruleEventContextType = typeof(TEventContext);

            if (ruleEventContextType != config.Event.EventContextType)
            {
                throw new ArgumentException(
                    $"Rule payload type '{ruleEventContextType}', but event '{config.Event.Id}' expects " +
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