using System;
using System.Threading;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecution
    {
        private readonly CancellationTokenSource _cancellationSource = new();
        public long Id { get; }
        public EcaRuleId RuleId { get; }
        public EcaRuleExecutionStatus Status { get; private set; }
        public Exception Exception { get; private set; }
        public CancellationToken CancellationToken => _cancellationSource.Token;
        internal EcaRuleExecution(long id, EcaRuleId ruleId)
        {
            Id = id;
            RuleId = ruleId;
            Status = EcaRuleExecutionStatus.Pending;
        }

        public void Cancel()
        {
            if (Status != EcaRuleExecutionStatus.Pending && Status != EcaRuleExecutionStatus.Running)
            {
                return;
            }

            _cancellationSource.Cancel();
        }

        internal void MarkRunning()
        {
            Status = EcaRuleExecutionStatus.Running;
        }

        internal void MarkCompleted()
        {
            Status = EcaRuleExecutionStatus.Completed;
        }

        internal void MarkCancelled()
        {
            Status = EcaRuleExecutionStatus.Cancelled;
        }

        internal void MarkFailed(Exception exception)
        {
            Exception = exception;
            Status = EcaRuleExecutionStatus.Failed;
        }

        internal void Dispose()
        {
            _cancellationSource.Dispose();
        }
    }
}