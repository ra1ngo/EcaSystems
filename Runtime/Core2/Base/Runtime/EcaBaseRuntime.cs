using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    //public sealed class EcaBaseRuntime : IDisposable
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

        public void Register<TEventState, TRuleState, TConditionContext, TActionContext>(
            IEcaRule<TEventState, TRuleState, TConditionContext, TActionContext> rule)
            where TRuleState : IEcaRuleState<TEventState>
            where TConditionContext : IEcaConditionContext
            where TActionContext : IEcaActionContext
        {
            _rules.Register(rule);
        }

        public bool Unregister(IEcaRule rule) => _rules.Unregister(rule);

        public void ForceFire<
            TEventState,
            TRuleState,
            TConditionContext,
            TActionContext>(
            IEcaEvent<TEventState> ecaEvent,
            TEventState eventState,
            Func<
                IEcaRule<
                    TEventState,
                    TRuleState,
                    TConditionContext,
                    TActionContext>,
                TEventState,
                TRuleState> createState,
            TConditionContext conditionContext,
            TActionContext actionContext)
            where TRuleState : IEcaRuleState<TEventState>
            where TConditionContext : IEcaConditionContext
            where TActionContext : IEcaActionContext
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));

            if (createState == null) throw new ArgumentNullException(nameof(createState));

            var rules = _rules.GetByEvent<
                TEventState,
                TRuleState,
                TConditionContext,
                TActionContext>(ecaEvent);

            var states = new TRuleState[rules.Count];
            var passed = new bool[rules.Count];

            // Сначала проверяем Conditions всех Rules.
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];

                var state = createState(rule, eventState);

                states[i] = state;

                passed[i] =
                    rule.Condition == null ||
                    _conditionChecker.Check(
                        rule.Condition,
                        state,
                        conditionContext);
            }

            // Только после всех Conditions запускаем Actions.
            for (var i = 0; i < rules.Count; i++)
            {
                if (!passed[i])
                    continue;

                _ = _actionRunner.Run(
                    rules[i].Action,
                    states[i],
                    actionContext);
            }
        }

        //public void Dispose() => _dispatcher.Fired -= OnFired;
    }
}
