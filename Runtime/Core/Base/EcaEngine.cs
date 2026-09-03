using System;
using System.Threading;

namespace EcaSystems.Core
{
    public sealed class EcaEngine
    {
        private readonly IEcaRuleRegistry _ruleRegistry;
        private readonly IEcaRuleChecker _ruleChecker;
        private readonly IEcaRuleRunner _ruleRunner;

        public EcaEngine()
            : this(new EcaRuleRegistry(), new EcaRuleChecker(), new EcaRuleRunner())
        {
        }

        public EcaEngine(
            IEcaRuleRegistry ruleRegistry,
            IEcaRuleChecker ruleChecker,
            IEcaRuleRunner ruleRunner)
        {
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
            _ruleChecker = ruleChecker ?? throw new ArgumentNullException(nameof(ruleChecker));
            _ruleRunner = ruleRunner ?? throw new ArgumentNullException(nameof(ruleRunner));
        }

        public void Register<TContext>(IEcaRule<TContext> rule)
        {
            _ruleRegistry.Register(rule);
        }

        public bool Unregister<TContext>(IEcaRule<TContext> rule)
        {
            return _ruleRegistry.Unregister(rule);
        }

        public void Fire(EcaEvent<EcaEventContextEmpty> ecaEvent)
        {
            Fire(ecaEvent, EcaEventContextEmpty.Value);
        }

        public void Fire<TEventContext>(
            EcaEvent<TEventContext> ecaEvent,
            TEventContext eventContext)
        {
            if (ecaEvent == null)
                throw new ArgumentNullException(nameof(ecaEvent));

            var context = new EcaContext<TEventContext>(eventContext);

            var rules = _ruleRegistry.GetRulesForEvent<EcaContext<TEventContext>>(ecaEvent);
            var checkedRules = _ruleChecker.Check(rules, _ => context);

            for (var i = 0; i < checkedRules.Count; i++)
                _ = _ruleRunner.Run(checkedRules[i], context, CancellationToken.None);
        }
    }
}