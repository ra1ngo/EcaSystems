using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecutionGroup<TEventContext> : IEcaRuleExecutionGroup
    {
        private readonly List<EcaRuleExecution<TEventContext>> _executions = new();
        private readonly IEcaRuleRunner _ruleRunner;
        private long _nextExecutionId = 1;

        public EcaRuleId RuleId => Rule.Id;
        public IEcaRule<EcaExecutionContext<TEventContext>> Rule { get; }
        public EcaRunMode RunMode { get; }
        public EcaRuleExecutionGroupState State { get; } = new();
        public IReadOnlyList<EcaRuleExecution<TEventContext>> Executions { get; }

        public EcaRuleExecutionGroup(IEcaRule<EcaExecutionContext<TEventContext>> rule,
            EcaRunMode runMode, IEcaRuleRunner ruleRunner)
        {
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new ArgumentException("Rule id cannot be empty.", nameof(rule));
            _ruleRunner = ruleRunner ?? throw new ArgumentNullException(nameof(ruleRunner));
            RunMode = runMode ?? throw new ArgumentNullException(nameof(runMode));
            Executions = _executions.AsReadOnly();
        }

        public void Fire(EcaExecutionContext<TEventContext> context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!ReferenceEquals(context.RuleExecutionGroupState, State))
                throw new ArgumentException("Context must use this group's state.", nameof(context));

            if (RunMode.Limit >= 0 && State.EcaRuleExecutionTotalStarted >= RunMode.Limit) return;

            switch (RunMode.Overlap)
            {
                case EcaOverlap.Ignore:
                    if (_executions.Count > 0) return;
                    break;
                case EcaOverlap.Allow:
                    break;
                default:
                    throw new NotSupportedException($"Overlap strategy '{RunMode.Overlap}' is not supported.");
            }

            var execution = new EcaRuleExecution<TEventContext>(_nextExecutionId++, Rule, context);
            _executions.Add(execution);
            _ = Execute(execution);
        }

        private async Task Execute(EcaRuleExecution<TEventContext> execution)
        {
            try
            {
                execution.MarkRunning();
                var task = _ruleRunner.Run(execution.Rule, execution.Context);
                if (task == null) throw new InvalidOperationException("Rule runner returned null Task.");
                await task;
                execution.MarkCompleted();
            }
            catch (Exception exception)
            {
                execution.MarkFailed(exception);
            }
            finally
            {
                State.IncrementFinished();
                _executions.Remove(execution);
            }
        }

        // TODO: Close/Dispose and unregister ownership require a separate lifecycle decision.
    }
}
