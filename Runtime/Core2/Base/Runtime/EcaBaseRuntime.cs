using System;

namespace EcaSystems.Core2
{
    public class EcaBaseRuntime
    {
        private readonly IEcaRuleRegistry _rules;
        private readonly IEcaActionRunner _actionRunner;
        private readonly IEcaConditionChecker _conditionChecker;

        public EcaBaseRuntime(IEcaRuleRegistry rules, IEcaActionRunner actionRunner, IEcaConditionChecker conditionChecker)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _actionRunner = actionRunner ?? throw new ArgumentNullException(nameof(actionRunner));
            _conditionChecker = conditionChecker ?? throw new ArgumentNullException(nameof(conditionChecker));
        }

        public void Register<E, R>(IEcaRule<E, R> rule)
            where R : IEcaRuleState<E>
        {
            _rules.Register(rule);
        }

        public bool Unregister(IEcaRule rule) => _rules.Unregister(rule);

        public void ForceFire<E, R>(
            IEcaEvent<E> ecaEvent, E eventState,
            Func<IEcaRule<E, R>, E, R> createState,
            IEcaConditionContext conditionContext = null, IEcaActionContext actionContext = null)
            where R : IEcaRuleState<E>
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (createState == null) throw new ArgumentNullException(nameof(createState));

            var rules = _rules.GetByEvent<E, R>(ecaEvent);
            var states = new R[rules.Count];
            var passed = new bool[rules.Count];

            // Сначала проверяем Conditions всех Rules.
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var state = createState(rule, eventState);
                if (state == null || !string.Equals(state.RuleId, rule.Id, StringComparison.Ordinal))
                    throw new InvalidOperationException($"State for rule '{rule.Id}' must be non-null and have the same RuleId.");
                states[i] = state;
                passed[i] = rule.Condition == null || _conditionChecker.Check(rule.Condition, state, conditionContext);
            }

            // Только после всех Conditions запускаем Actions.
            for (var i = 0; i < rules.Count; i++)
            {
                if (!passed[i]) continue;
                _ = _actionRunner.Run(rules[i].Action, states[i], actionContext);
            }
        }
    }
}
