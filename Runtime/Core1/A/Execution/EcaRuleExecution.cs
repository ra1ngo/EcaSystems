using System;

namespace EcaSystems.Core1
{
    public sealed class EcaRuleExecution
    {
        public long Id { get; }
        public IEcaRule Rule { get; }
        public string RuleId => Rule.Id;
        public EcaRuleExecutionStatus Status { get; private set; }
        public Exception Exception { get; private set; }

        internal EcaRuleExecution(long id, IEcaRule rule)
        {
            Id = id;
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            Status = EcaRuleExecutionStatus.Pending;
        }

        internal void MarkRunning() => Status = EcaRuleExecutionStatus.Running;
        internal void MarkCompleted() => Status = EcaRuleExecutionStatus.Completed;
        internal void MarkFailed(Exception exception)
        {
            Exception = exception;
            Status = EcaRuleExecutionStatus.Failed;
        }
    }
}
