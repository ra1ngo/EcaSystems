using System;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecution<TEventContext>
    {
        public long Id { get; }
        public EcaRuleId RuleId => Rule.Id;
        public IEcaRule<EcaExecutionContext<TEventContext>> Rule { get; }
        public EcaExecutionContext<TEventContext> Context { get; }
        public EcaRuleExecutionStatus Status { get; private set; }
        public Exception Exception { get; private set; }

        internal EcaRuleExecution(long id,
            IEcaRule<EcaExecutionContext<TEventContext>> rule,
            EcaExecutionContext<TEventContext> context)
        {
            Id = id;
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Status = EcaRuleExecutionStatus.Pending;
        }

        internal void MarkRunning()
        {
            Status = EcaRuleExecutionStatus.Running;
            Context.RuleExecutionGroupState.IncrementStarted();
        }

        internal void MarkCompleted() => Status = EcaRuleExecutionStatus.Completed;

        internal void MarkFailed(Exception exception)
        {
            Exception = exception;
            Status = EcaRuleExecutionStatus.Failed;
        }
    }
}
