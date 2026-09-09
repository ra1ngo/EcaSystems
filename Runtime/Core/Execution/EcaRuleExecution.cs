using System;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecution<TEventContext>
    {
        public long Id { get; }
        public EcaRuleId RuleId => Rule.Id;
        public IEcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> Rule { get; }
        public IEcaExecutionActionContext<TEventContext> Context { get; }
        public EcaRuleExecutionStatus Status { get; private set; }
        public Exception Exception { get; private set; }

        internal EcaRuleExecution(long id,
            IEcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> rule,
            IEcaExecutionActionContext<TEventContext> context)
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
