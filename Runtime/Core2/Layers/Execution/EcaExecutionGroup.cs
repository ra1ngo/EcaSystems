using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public sealed class EcaExecutionGroup<E, R, C, A> : IEcaExecutionGroup<E, R, C, A>
        where R : IEcaExecutionRuleState<E>
        where C : IEcaExecutionConditionContext
        where A : IEcaExecutionActionContext
    {
        private readonly IEcaRule<E, R, C, A> _rule;
        private readonly Func<E, EcaExecutionGroupState, R> _createState;
        private readonly IEcaConditionChecker _conditionChecker;
        private readonly IEcaActionRunner _actionRunner;
        private readonly List<EcaExecution> _executions = new();
        private long _nextExecutionId;

        public string RuleId => _rule.Id;
        public IEcaRule Rule => _rule;
        public EcaExecutionMode ExecutionMode { get; }
        public EcaExecutionGroupState State { get; } = new();
        public IReadOnlyList<EcaExecution> Executions { get; }

        public EcaExecutionGroup(
            IEcaRule<E, R, C, A> rule, EcaExecutionMode executionMode,
            Func<E, EcaExecutionGroupState, R> createState,
            IEcaConditionChecker conditionChecker, IEcaActionRunner actionRunner)
        {
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            ExecutionMode = executionMode ?? throw new ArgumentNullException(nameof(executionMode));
            _createState = createState ?? throw new ArgumentNullException(nameof(createState));
            _conditionChecker = conditionChecker ?? throw new ArgumentNullException(nameof(conditionChecker));
            _actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
            Executions = _executions.AsReadOnly();
        }

        public bool Check(E eventState, C context)
        {
            if (_rule.Condition == null) return true;
            return _conditionChecker.Check(_rule.Condition, _createState(eventState, State), context);
        }

        public Task Run(E eventState, A context)
        {
            // После Check вложенный Fire мог занять Group или исчерпать её Limit.
            if (!CheckExecutionMode()) return Task.CompletedTask;

            var execution = new EcaExecution(++_nextExecutionId, _rule);
            _executions.Add(execution);
            return RunLifecycle(execution, eventState, context);
        }

        private bool CheckExecutionMode()
        {
            if (ExecutionMode.Limit >= 0 && State.TotalStarted >= ExecutionMode.Limit) return false;
            if (ExecutionMode.Overlap == EcaExecutionModeOverlap.Ignore && _executions.Count > 0) return false;
            return true;
        }

        private async Task RunLifecycle(EcaExecution execution, E eventState, A context)
        {
            execution.MarkRunning();
            State.IncrementStarted();
            try
            {
                // Active и Started уже видны даже из reentrant createState/Action.
                var state = _createState(eventState, State);
                var task = _actionRunner.Run(_rule.Action, state, context)
                    ?? throw new InvalidOperationException($"Action for rule '{RuleId}' returned null Task.");
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
