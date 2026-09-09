using System;
using System.Collections.Generic;

namespace EcaSystems.Core
{
    public sealed class EcaEngine
    {
        private readonly IEcaRuleRegistry _ruleRegistry;
        private readonly IEcaRuleSelector _ruleSelector;
        private readonly IEcaRuleChecker _ruleChecker;
        private readonly IEcaRuleRunner _ruleRunner;

        public EcaEngine(): this(new EcaRuleRegistry(), new EcaRuleChecker(), new EcaRuleRunner()) {}

        public EcaEngine(IEcaRuleRegistry ruleRegistry, IEcaRuleChecker ruleChecker, IEcaRuleRunner ruleRunner)
            : this(ruleRegistry, new EcaRuleSelector(ruleRegistry), ruleChecker, ruleRunner) {}

        public EcaEngine(IEcaRuleRegistry ruleRegistry, IEcaRuleSelector ruleSelector,
            IEcaRuleChecker ruleChecker, IEcaRuleRunner ruleRunner)
        {
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
            _ruleSelector = ruleSelector ?? throw new ArgumentNullException(nameof(ruleSelector));
            _ruleChecker = ruleChecker ?? throw new ArgumentNullException(nameof(ruleChecker));
            _ruleRunner = ruleRunner ?? throw new ArgumentNullException(nameof(ruleRunner));
        }

        public void Register<TEventContext, TConditionContext, TActionContext>(IEcaRule<TEventContext, TConditionContext, TActionContext> rule)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>
        {
            _ruleRegistry.Register(rule);
        }

        public bool Unregister<TEventContext, TConditionContext, TActionContext>(IEcaRule<TEventContext, TConditionContext, TActionContext> rule)
            where TConditionContext : IEcaConditionContext<TEventContext>
            where TActionContext : IEcaActionContext<TEventContext>
        {
            return _ruleRegistry.Unregister(rule);
        }

        public void Fire(EcaEvent<EcaEventContextEmpty> ecaEvent)
        {
            Fire(ecaEvent, EcaEventContextEmpty.Value);
        }

        public void Fire<TEventContext>(EcaEvent<TEventContext> ecaEvent, TEventContext eventContext)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));

            var context = new EcaConditionContext<TEventContext>(eventContext);
            var rules = _ruleSelector.ForEvent<TEventContext, EcaConditionContext<TEventContext>, EcaActionContext<TEventContext>>(ecaEvent);
            var checkedRules = new List<IEcaRule<TEventContext, EcaConditionContext<TEventContext>, EcaActionContext<TEventContext>>>(rules.Count);
            for (var i = 0; i < rules.Count; i++)
                if (_ruleChecker.Check(rules[i], context)) checkedRules.Add(rules[i]);

            var actionContext = new EcaActionContext<TEventContext>(eventContext);

            // Complete the condition phase before starting any action of this Fire.
            for (var i = 0; i < checkedRules.Count; i++)
                _ = _ruleRunner.Run(checkedRules[i], actionContext);
        }
    }
}
