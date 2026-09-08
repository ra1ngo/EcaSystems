using System;
using System.Threading;
using System.Threading.Tasks;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecution<TEventContext>
    {
        private readonly CancellationTokenSource _cancellationSource = new();
        public long Id { get; }
        public EcaRuleId RuleId => Rule.Id;
        public IEcaRule<EcaExecutionContext<TEventContext>> Rule { get; }
        public EcaExecutionContext<TEventContext> Context { get; }
        public EcaRuleExecutionStatus Status { get; private set; }
        public Exception Exception { get; private set; }
        // TODO: decide the user-facing token delivery API; do not put it in Base or Context.
        public CancellationToken CancellationToken { get; }
        internal Task CancellationTask { get; set; }
        internal bool HasStarted { get; private set; }

        internal EcaRuleExecution(long id,
            IEcaRule<EcaExecutionContext<TEventContext>> rule,
            EcaExecutionContext<TEventContext> context)
        {
            Id = id;
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            CancellationToken = _cancellationSource.Token;
            Status = EcaRuleExecutionStatus.Pending;
        }

        internal void RequestCancellation() => _cancellationSource.Cancel();

        internal void MarkRunning()
        {
            Status = EcaRuleExecutionStatus.Running;
            HasStarted = true;
            Context.RuleExecutionGroupState.IncrementStarted();
        }

        internal void MarkCancelling() => Status = EcaRuleExecutionStatus.Cancelling;
        internal void MarkCompleted() => Status = EcaRuleExecutionStatus.Completed;
        internal void MarkCancelled() => Status = EcaRuleExecutionStatus.Cancelled;

        internal void MarkFailed(Exception exception)
        {
            Exception = exception;
            Status = EcaRuleExecutionStatus.Failed;
        }

        internal void Dispose() => _cancellationSource.Dispose();
    }
}
