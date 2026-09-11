using System;
using System.Collections.Generic;

namespace EcaSystems.Core
{
    /* Минимальный самостоятельный engine поверх Base: клиент может использовать
       его напрямую, когда достаточно простого ECA. Это также reference implementation
       Base pipeline; Execution/Scope используют собственную runtime-инфраструктуру.
       Отдельное существование engine и дублирование orchestration с
       EcaExecutionEngine нужно пересмотреть после architecture checkpoint. */
    public sealed class EcaBaseEngine
    {
        private readonly IEcaRuleRegistry _ruleRegistry;
        private readonly IEcaRuleSelector _ruleSelector;
        private readonly IEcaRuleChecker _ruleChecker;
        private readonly IEcaRuleRunner _ruleRunner;

        public EcaBaseEngine(): this(new EcaRuleRegistry(), new EcaRuleChecker(), new EcaRuleRunner()) {}

        public EcaBaseEngine(IEcaRuleRegistry ruleRegistry, IEcaRuleChecker ruleChecker, IEcaRuleRunner ruleRunner)
            : this(ruleRegistry, new EcaRuleSelector(ruleRegistry), ruleChecker, ruleRunner) {}

        public EcaBaseEngine(IEcaRuleRegistry ruleRegistry, IEcaRuleSelector ruleSelector,
            IEcaRuleChecker ruleChecker, IEcaRuleRunner ruleRunner)
        {
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
            _ruleSelector = ruleSelector ?? throw new ArgumentNullException(nameof(ruleSelector));
            _ruleChecker = ruleChecker ?? throw new ArgumentNullException(nameof(ruleChecker));
            _ruleRunner = ruleRunner ?? throw new ArgumentNullException(nameof(ruleRunner));
        }

        public void Register<TEventContext>(
            IEcaRule<TEventContext, EcaConditionContext<TEventContext>, EcaActionContext<TEventContext>> rule)
        {
            _ruleRegistry.Register(rule);
        }

        public bool Unregister<TEventContext>(
            IEcaRule<TEventContext, EcaConditionContext<TEventContext>, EcaActionContext<TEventContext>> rule)
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

            /* Все Conditions одного Fire проверены до любой Action:
               изменения из Actions не влияют на отбор Rules этого Fire. */
            for (var i = 0; i < checkedRules.Count; i++)
                _ = _ruleRunner.Run(checkedRules[i], actionContext);
        }
    }
}
