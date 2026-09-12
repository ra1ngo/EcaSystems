using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcaSystems.Core1
{
    /* Group не знает generic-типы: Rule/State/RunnerContexts уже связаны внутри
       RuleRun. Здесь только admission и наблюдение одного Action callback. */
    public sealed class EcaRuleExecutionGroup
    {
        private readonly List<EcaRuleExecution> _executions = new();
        private long _nextExecutionId = 1;

        public IEcaRule Rule { get; }
        public string RuleId => Rule.Id;
        public EcaExecutionMode ExecutionMode { get; }
        public EcaRuleExecutionGroupState State { get; } = new();
        public IReadOnlyList<EcaRuleExecution> Executions { get; }

        public EcaRuleExecutionGroup(IEcaRule rule, EcaExecutionMode mode)
        {
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            if (string.IsNullOrWhiteSpace(rule.Id)) throw new ArgumentException("Rule id cannot be empty.", nameof(rule));
            ExecutionMode = mode ?? throw new ArgumentNullException(nameof(mode));
            Executions = _executions.AsReadOnly();
        }

        public Task Run(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (ExecutionMode.Limit >= 0 && State.TotalStarted >= ExecutionMode.Limit) return Task.CompletedTask;
            if (ExecutionMode.Overlap == EcaOverlap.Ignore && _executions.Count > 0) return Task.CompletedTask;

            var execution = new EcaRuleExecution(_nextExecutionId++, Rule);
            _executions.Add(execution);
            return Execute(execution, action);
        }

        private async Task Execute(EcaRuleExecution execution, Func<Task> action)
        {
            try
            {
                /* Running и счётчик устанавливаются до callback: nested Fire
                   уже видит занятый слот Ignore и расход Limit. */
                execution.MarkRunning();
                State.IncrementStarted();
                var task = action();
                if (task == null) throw new InvalidOperationException("Action returned null Task.");
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
    }
}
