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

        public void Register<E, R, C, A>(IEcaRule<E, R, C, A> rule)
            where R : IEcaRuleState<E>
            where C : IEcaConditionContext
            where A : IEcaActionContext
        {
            _rules.Register(rule);
        }

        public bool Unregister(IEcaRule rule) => _rules.Unregister(rule);

        public void Fire<E, R, C, A>(
            IEcaEvent<E> ecaEvent, E eventState,
            Func<IEcaRule<E, R, C, A>, E, R> createState,
            C conditionContext, A actionContext)
            where R : IEcaRuleState<E>
            where C : IEcaConditionContext
            where A : IEcaActionContext
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            if (createState == null) throw new ArgumentNullException(nameof(createState));

            var rules = _rules.GetByEvent<E, R, C, A>(ecaEvent);
            var states = new R[rules.Count];
            var passed = new bool[rules.Count];

            // Сначала проверяем Conditions всех Rules.
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var state = createState(rule, eventState);
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
