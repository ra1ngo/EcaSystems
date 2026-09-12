using System;

namespace EcaSystems.Core1
{
    public class EcaExecutionRuleState<TEventState> : EcaRuleState<TEventState>, IEcaExecutionRuleState<TEventState>
    {
        public EcaRuleExecutionGroupState RuleExecutionGroupState { get; }

        public EcaExecutionRuleState(TEventState eventState, EcaRuleExecutionGroupState groupState) : base(eventState)
        {
            RuleExecutionGroupState = groupState ?? throw new ArgumentNullException(nameof(groupState));
        }
    }
}
