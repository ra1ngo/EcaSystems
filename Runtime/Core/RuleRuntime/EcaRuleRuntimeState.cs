namespace EcaSystems.Core
{
    public sealed class EcaRuleRuntimeState
    {
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