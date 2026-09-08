namespace EcaSystems.Core
{
    public sealed class EcaRuleExecutionGroupState
    {
        // Live state of one rule's group. TODO: design read-only execution inspection later.
        public long EcaRuleExecutionTotalStarted { get; private set; }
        public long EcaRuleExecutionTotalFinished { get; private set; }

        internal void IncrementStarted()
        {
            EcaRuleExecutionTotalStarted++;
        }

        internal void IncrementFinished()
        {
            EcaRuleExecutionTotalFinished++;
        }
    }
}
