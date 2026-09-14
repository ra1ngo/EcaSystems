using System;

namespace EcaSystems.Core2
{
    public sealed class EcaExecution
    {
        public long Id { get; }
        public IEcaRule Rule { get; }
        public string RuleId => Rule.Id;
        public EcaExecutionStatus Status { get; private set; } = EcaExecutionStatus.Pending;
        public Exception Exception { get; private set; }

        internal EcaExecution(long id, IEcaRule rule)
        {
            Id = id;
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }

        internal void MarkRunning() => Status = EcaExecutionStatus.Running;
        internal void MarkCompleted() => Status = EcaExecutionStatus.Completed;

        internal void MarkFailed(Exception exception)
        {
            Exception = exception;
            Status = EcaExecutionStatus.Failed;
        }
    }
}
