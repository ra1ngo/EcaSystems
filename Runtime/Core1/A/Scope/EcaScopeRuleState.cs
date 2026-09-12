using System;

namespace EcaSystems.Core1
{
    public class EcaScopeRuleState<TEventState> : EcaExecutionRuleState<TEventState>, IEcaScopeRuleState<TEventState>
    {
        public EcaScopeState ScopeState { get; }

        public EcaScopeRuleState(TEventState eventState, EcaRuleExecutionGroupState groupState, EcaScopeState scopeState)
            : base(eventState, groupState)
        {
            ScopeState = scopeState ?? throw new ArgumentNullException(nameof(scopeState));
        }
    }
}
