using System;
using System.Collections.Generic;

namespace EcaSystems.Core2
{
    public sealed class EcaBaseRuntime //: IDisposable
    {
        private readonly IEcaRuleRegistry _rules;
        private readonly IEcaActionRunner actionRunner;
        private readonly IEcaConditionChecker _conditionChecker;

        public EcaBaseEngine(EcaRuleRegistry rules, IEcaActionRunner actionRunner, IEcaConditionChecker conditionChecker)
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
            //_runner.ValidateRule<TEventState>(rule); //TODO важно, валидация должна быть в регистре!!!
            _rules.Register(rule);
        }

        public bool Unregister(IEcaRule rule) => _rules.Unregister(rule);

        //public void Dispose() => _dispatcher.Fired -= OnFired;
    }
}
