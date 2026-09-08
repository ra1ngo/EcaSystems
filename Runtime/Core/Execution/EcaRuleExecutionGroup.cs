using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecutionGroup<TEventContext> : IEcaRuleExecutionGroup
    {
        private readonly List<EcaRuleExecution<TEventContext>> _executions = new();
        private readonly IEcaExecutionExecutor _executor;
        private long _nextExecutionId = 1;

        public EcaRuleId RuleId => Rule.Id;
        public IEcaRule<EcaExecutionContext<TEventContext>> Rule { get; }
        public EcaOverlap Overlap { get; }
        public EcaRuleExecutionGroupState State { get; } = new();
        public IReadOnlyList<EcaRuleExecution<TEventContext>> Executions { get; }

        public EcaRuleExecutionGroup(IEcaRule<EcaExecutionContext<TEventContext>> rule,
            EcaOverlap overlap, IEcaExecutionExecutor executor)
        {
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new ArgumentException("Rule id cannot be empty.", nameof(rule));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            Overlap = overlap;
            Executions = _executions.AsReadOnly();
        }

        public void Fire(EcaExecutionContext<TEventContext> context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!ReferenceEquals(context.RuleExecutionGroupState, State))
                throw new ArgumentException("Context must use this group's state.", nameof(context));

            switch (Overlap)
            {
                case EcaOverlap.Ignore:
                    if (_executions.Count > 0) return;
                    break;
                case EcaOverlap.Allow:
                    break;
                default:
                    throw new NotSupportedException($"Overlap strategy '{Overlap}' is not supported.");
            }

            var execution = new EcaRuleExecution<TEventContext>(_nextExecutionId++, Rule, context);
            _executions.Add(execution);
            _ = Execute(execution);
        }

        private async Task Execute(EcaRuleExecution<TEventContext> execution)
        {
            try
            {
                await _executor.Execute(execution);
            }
            catch (Exception exception)
            {
                execution.MarkFailed(exception);
            }
            finally
            {
                if (execution.HasStarted) State.IncrementFinished();
                _executions.Remove(execution);
                execution.Dispose();
            }
        }

        public Task Cancel(EcaRuleExecution<TEventContext> execution)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (!_executions.Contains(execution))
                throw new InvalidOperationException("Execution does not belong to this EcaRuleExecutionGroup.");
            return _executor.Cancel(execution);
        }

        public Task CancelAll()
        {
            var executions = _executions.ToArray();
            var tasks = new Task[executions.Length];
            for (var i = 0; i < executions.Length; i++)
                tasks[i] = _executor.Cancel(executions[i]);
            return Task.WhenAll(tasks);
        }

        // TODO: Close/Dispose and unregister ownership require a separate lifecycle decision.
    }
}
