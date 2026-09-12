namespace EcaSystems.Core1
{
    /* Данные живут вместе с регистрацией. Старый активный запуск удерживает их
       после Unregister; повторная регистрация создаёт независимый экземпляр. */
    public sealed class EcaRuleExecutionGroupState
    {
        public long TotalStarted { get; private set; }
        public long TotalFinished { get; private set; }
        internal void IncrementStarted() => TotalStarted++;
        internal void IncrementFinished() => TotalFinished++;
    }
}
