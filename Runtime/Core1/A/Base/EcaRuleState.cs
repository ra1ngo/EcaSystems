namespace EcaSystems.Core1
{
    public class EcaRuleState<TEventState> : IEcaRuleState<TEventState>
    {
        public TEventState EventState { get; }

        public EcaRuleState(TEventState eventState)
        {
            EventState = eventState;
        }
    }
}
