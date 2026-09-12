using System;

namespace EcaSystems.Core1
{
    public class EcaRule<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext>
        : IEcaRule<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext>
        where TRuleState : IEcaRuleState<TEventState>
        where TConditionRunnerContext : IEcaConditionRunnerContext
        where TActionRunnerContext : IEcaActionRunnerContext
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public IEcaEvent<TEventState> Event { get; }
        public IEcaCondition<TRuleState, TConditionRunnerContext> Condition { get; }
        public IEcaAction<TRuleState, TActionRunnerContext> Action { get; }

        IEcaEvent IEcaRule.Event => Event;
        IEcaCondition IEcaRule.Condition => Condition;
        IEcaAction IEcaRule.Action => Action;

        public EcaRule(string id, string name, IEcaEvent<TEventState> ecaEvent,
            IEcaAction<TRuleState, TActionRunnerContext> action,
            IEcaCondition<TRuleState, TConditionRunnerContext> condition = null, string description = "")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Rule id cannot be empty.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Rule name cannot be empty.", nameof(name));
            Event = ecaEvent ?? throw new ArgumentNullException(nameof(ecaEvent));
            if (ecaEvent.EventStateType != typeof(TEventState))
                throw new ArgumentException("Event metadata disagrees with its generic contract.", nameof(ecaEvent));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Condition = condition;
            Id = id;
            Name = name;
            Description = description ?? string.Empty;
        }
    }

    public class EcaRule<TEventState> : EcaRule<TEventState, EcaRuleState<TEventState>,
        IEcaConditionRunnerContext, IEcaActionRunnerContext>
    {
        public EcaRule(string id, string name, IEcaEvent<TEventState> ecaEvent,
            IEcaAction<EcaRuleState<TEventState>, IEcaActionRunnerContext> action,
            IEcaCondition<EcaRuleState<TEventState>, IEcaConditionRunnerContext> condition = null,
            string description = "") : base(id, name, ecaEvent, action, condition, description) { }
    }

    public sealed class EcaRule : EcaRule<EcaEventStateEmpty>
    {
        public EcaRule(string id, string name, IEcaEvent<EcaEventStateEmpty> ecaEvent,
            IEcaAction<EcaRuleState<EcaEventStateEmpty>, IEcaActionRunnerContext> action,
            IEcaCondition<EcaRuleState<EcaEventStateEmpty>, IEcaConditionRunnerContext> condition = null,
            string description = "") : base(id, name, ecaEvent, action, condition, description) { }
    }
}
