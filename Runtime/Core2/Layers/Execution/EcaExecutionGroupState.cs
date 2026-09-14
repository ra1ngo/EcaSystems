namespace EcaSystems.Core2
{
    public sealed class EcaExecutionGroupState
    {
        public long TotalStarted { get; private set; }
        public long TotalFinished { get; private set; }

        internal void IncrementStarted() => TotalStarted++;
        internal void IncrementFinished() => TotalFinished++;
    }
}
