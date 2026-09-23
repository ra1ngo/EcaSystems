using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EcaSystems.Core2
{
    public sealed class EcaExecutionGroup<E, R> : IEcaExecutionGroup<E, R>
        where R : IEcaExecutionRuleState<E>
    {
        private readonly IEcaRule<E, R> _rule;
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
            IEcaRule<E, R> rule, EcaExecutionMode executionMode,
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

        public bool Check(E eventState, IEcaConditionContext context)
        {
            if (_rule.Condition == null) return true;
            return _conditionChecker.Check(_rule.Condition, CreateState(eventState), context);
        }

        private R CreateState(E eventState)
        {
            var state = _createState(eventState, State);
            if (state == null || !string.Equals(state.RuleId, _rule.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"State for rule '{_rule.Id}' must be non-null and have the same RuleId.");
            return state;
        }

        public Task Run(E eventState, IEcaActionContext context)
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

        private async Task RunLifecycle(EcaExecution execution, E eventState, IEcaActionContext context)
        {
            execution.MarkRunning();
            State.IncrementStarted();
            try
            {
                // Active и Started уже видны даже из reentrant createState/Action.
                var state = CreateState(eventState);
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
