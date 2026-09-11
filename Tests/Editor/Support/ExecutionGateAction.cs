using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;

namespace EcaSystems.Tests.Support
{
    internal sealed class ExecutionGateAction<TEventContext>
        : IEcaAction<IEcaExecutionActionContext<TEventContext>>, IDisposable
    {
        private readonly List<TaskCompletionSource<bool>> _gates = new();

        public int RunCount { get; private set; }

        public List<ExecutionRecord> Records { get; } = new();

        public List<EcaRuleExecutionGroupState> ExecutionGroupStates { get; } = new();

        public Task Run(
            IEcaExecutionActionContext<TEventContext> context)
        {
            RunCount++;

            ExecutionGroupStates.Add(
                context.RuleExecutionGroupState
            );

            Records.Add(
                new ExecutionRecord(
                    context.RuleExecutionGroupState
                        .EcaRuleExecutionTotalStarted,
                    context.RuleExecutionGroupState
                        .EcaRuleExecutionTotalFinished
                )
            );

            var gate =
                new TaskCompletionSource<bool>();

            _gates.Add(gate);

            return gate.Task;
        }

        public void FailAll(Exception exception)
        {
            foreach (var gate in _gates) gate.TrySetException(exception);
            _gates.Clear();
        }

        public void Dispose() => CompleteAll();

        public void CompleteAll()
        {
            for (var i = 0; i < _gates.Count; i++)
                _gates[i].TrySetResult(true);

            _gates.Clear();
        }
    }
}
