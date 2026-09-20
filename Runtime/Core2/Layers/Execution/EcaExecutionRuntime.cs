using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaExecutionRuntime
    {
        private readonly IEcaRuleRegistry _rules;
        private readonly IEcaExecutionGroupRegistry _groups;
        private readonly IEcaConditionChecker _conditionChecker;
        private readonly IEcaActionRunner _actionRunner;
        private readonly EcaBaseRuntime _baseRuntime;

        public EcaExecutionRuntime(
            IEcaRuleRegistry rules, IEcaExecutionGroupRegistry groups,
            IEcaConditionChecker conditionChecker, IEcaActionRunner actionRunner)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _groups = groups ?? throw new ArgumentNullException(nameof(groups));
            _conditionChecker = conditionChecker ?? throw new ArgumentNullException(nameof(conditionChecker));
            _actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
            _baseRuntime = new EcaBaseRuntime(_rules, _actionRunner, _conditionChecker);
        }

        public void Register<E, R>(
            IEcaRule<E, R> rule, EcaExecutionMode executionMode,
            Func<E, EcaExecutionGroupState, R> createState)
            where R : IEcaExecutionRuleState<E>
        {
            _baseRuntime.Register(rule);
            try
            {
                var group = new EcaExecutionGroup<E, R>(
                    rule, executionMode, createState, _conditionChecker, _actionRunner);
                _groups.Register(group);
            }
            catch
            {
                _baseRuntime.Unregister(rule);
                throw;
            }
        }

        public bool Unregister(IEcaRule rule)
        {
            if (!_baseRuntime.Unregister(rule)) return false;
            _groups.Unregister(rule.Id);
            return true;
        }

        public void Fire<E>(IEcaEvent<E> ecaEvent, E eventState, IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
        {
            var rules = _rules.GetByEvent<E>(ecaEvent);
            var passedGroups = new List<IEcaExecutionGroup<E>>();
            foreach (var rule in rules)
            {
                var group = _groups.Get<E>(rule.Id);
                if (group.Check(eventState, conditionContext)) passedGroups.Add(group);
            }

            // Сохраняем выбранные Group: Unregister из Action влияет только на будущие Fire.
            foreach (var group in passedGroups)
                _ = group.Run(eventState, actionContext);
        }

        public void Fire<E, R>(
            IEcaEvent<E> ecaEvent, E eventState, Func<IEcaRule<E, R>, E, R> createState,
            IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
            where R : IEcaRuleState<E>
        {
            _baseRuntime.Fire(ecaEvent, eventState, createState, conditionContext, actionContext);
        }

        public IEcaExecutionGroup GetGroup(string ruleId) => _groups.Get(ruleId);
        public bool TryGetGroup(string ruleId, out IEcaExecutionGroup group) => _groups.TryGet(ruleId, out group);
    }
}
