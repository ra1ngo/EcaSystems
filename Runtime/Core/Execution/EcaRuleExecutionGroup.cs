using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleExecutionGroup
    {
        private readonly List<EcaRuleExecution> _executions = new();
        private long _nextExecutionId = 1;

        public EcaRuleId RuleId { get; }
        public EcaOverlap Overlap { get; }

        public EcaRuleExecutionState State { get; } = new();
        public IReadOnlyList<EcaRuleExecution> Executions => _executions;

        public EcaRuleExecutionGroup(EcaRuleId ruleId, EcaOverlap overlap)
        {
            if (string.IsNullOrWhiteSpace(ruleId))
                throw new ArgumentException("Rule id cannot be empty.", nameof(ruleId));

            RuleId = ruleId;
            Overlap = overlap;
        }

        public bool TryCreateExecution(out EcaRuleExecution execution)
        {
            switch (Overlap)
            {
                case EcaOverlap.Ignore:
                    if (_executions.Count > 0)
                    {
                        execution = null;
                        return false;
                    }

                    break;

                case EcaOverlap.Allow:
                    break;

                default:
                    throw new NotSupportedException(
                        $"Overlap strategy '{Overlap}' is not supported."
                    );
            }

            execution = new EcaRuleExecution(_nextExecutionId++, RuleId);

            _executions.Add(execution);

            return true;
        }

        public async Task Run(
            EcaRuleExecution execution,
            Func<CancellationToken, Task> run)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (run == null) throw new ArgumentNullException(nameof(run));

            if (!_executions.Contains(execution))
                throw new InvalidOperationException("Execution does not belong to this EcaRuleExecutionGroup.");

            if (execution.Status != EcaRuleExecutionStatus.Pending)
                throw new InvalidOperationException("Only a pending execution can be started.");

            execution.MarkRunning();
            State.IncrementStarted();

            try
            {
                var task = run(execution.CancellationToken);

                if (task == null)
                    throw new InvalidOperationException("Rule runner returned null Task.");

                await task;

                if (execution.CancellationToken.IsCancellationRequested)
                    execution.MarkCancelled();
                else
                    execution.MarkCompleted();
            }
            catch (OperationCanceledException)
            {
                execution.MarkCancelled();
            }
            catch (Exception exception)
            {
                execution.MarkFailed(exception);
            }
            finally
            {
                State.IncrementFinished();

                _executions.Remove(execution);

                execution.Dispose();

                // Future Queue continuation naturally belongs here.
            }
        }
    }
}