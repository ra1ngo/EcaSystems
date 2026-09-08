using System;
using System.Threading.Tasks;

namespace EcaSystems.Core
{
    // All per-execution state belongs to the execution, so this service can be shared.
    public sealed class EcaExecutionExecutor : IEcaExecutionExecutor
    {
        private readonly IEcaRuleRunner _ruleRunner = new EcaRuleRunner();

        public async Task Execute<TEventContext>(EcaRuleExecution<TEventContext> execution)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (execution.Status == EcaRuleExecutionStatus.Cancelled) return;
            if (execution.Status != EcaRuleExecutionStatus.Pending)
                throw new InvalidOperationException("Only a pending execution can be started.");

            execution.MarkRunning();
            try
            {
                var task = _ruleRunner.Run(execution.Rule, execution.Context);
                if (task == null) throw new InvalidOperationException("Rule runner returned null Task.");
                await task;
                if (execution.Status == EcaRuleExecutionStatus.Running)
                    execution.MarkCompleted();
            }
            catch (OperationCanceledException)
            {
                if (execution.Status == EcaRuleExecutionStatus.Running)
                    execution.MarkCancelled();
            }
            catch (Exception exception)
            {
                // Failure does not invoke the action's cancellation hook.
                if (execution.Status == EcaRuleExecutionStatus.Running ||
                    execution.Status == EcaRuleExecutionStatus.Cancelling)
                    execution.MarkFailed(exception);
            }
            finally
            {
                // Run may end while asynchronous cancellation cleanup is still in progress.
                if (execution.CancellationTask != null)
                    await execution.CancellationTask;
            }
        }

        public Task Cancel<TEventContext>(EcaRuleExecution<TEventContext> execution)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (execution.CancellationTask != null) return execution.CancellationTask;
            if (execution.Status != EcaRuleExecutionStatus.Pending &&
                execution.Status != EcaRuleExecutionStatus.Running) return Task.CompletedTask;

            // Publish before invoking user code, including synchronous/reentrant callbacks.
            var completion = new TaskCompletionSource<bool>();
            execution.CancellationTask = completion.Task;
            _ = CancelCore(execution, completion);
            return completion.Task;
        }

        private static async Task CancelCore<TEventContext>(
            EcaRuleExecution<TEventContext> execution, TaskCompletionSource<bool> completion)
        {
            try
            {
                var wasPending = execution.Status == EcaRuleExecutionStatus.Pending;
                if (!wasPending) execution.MarkCancelling();
                execution.RequestCancellation();
                if (!wasPending &&
                    execution.Rule.Action is IEcaCancellableAction<EcaExecutionContext<TEventContext>> action)
                {
                    var task = action.Cancel(execution.Context);
                    if (task == null) throw new InvalidOperationException("Action cancellation returned null Task.");
                    await task;
                }
                if (execution.Status == EcaRuleExecutionStatus.Pending ||
                    execution.Status == EcaRuleExecutionStatus.Cancelling)
                    execution.MarkCancelled();
            }
            catch (Exception exception)
            {
                execution.MarkFailed(exception);
            }
            finally
            {
                completion.TrySetResult(true);
            }
        }
    }
}
