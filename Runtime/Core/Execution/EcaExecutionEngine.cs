using System;
using System.Collections.Generic;

namespace EcaSystems.Core
{
    public sealed class EcaExecutionEngine
    {
        private readonly IEcaRuleRegistry _ruleRegistry;
        private readonly IEcaRuleSelector _ruleSelector;
        private readonly IEcaRuleChecker _ruleChecker;
        private readonly IEcaRuleExecutionRegistry _executionRegistry;
        private readonly IEcaExecutionContextFactory _contextFactory;
        private readonly IEcaRuleRunner _ruleRunner;

        public EcaExecutionEngine() : this(new EcaRuleRegistry()) {}

        private EcaExecutionEngine(IEcaRuleRegistry ruleRegistry)
            : this(ruleRegistry, new EcaRuleSelector(ruleRegistry), new EcaRuleChecker(),
                new EcaRuleExecutionRegistry(), new EcaExecutionContextFactory(), new EcaRuleRunner()) {}

        public EcaExecutionEngine(IEcaRuleRegistry ruleRegistry, IEcaRuleSelector ruleSelector,
            IEcaRuleChecker ruleChecker, IEcaRuleExecutionRegistry executionRegistry,
            IEcaExecutionContextFactory contextFactory, IEcaRuleRunner ruleRunner)
        {
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
            _ruleSelector = ruleSelector ?? throw new ArgumentNullException(nameof(ruleSelector));
            _ruleChecker = ruleChecker ?? throw new ArgumentNullException(nameof(ruleChecker));
            _executionRegistry = executionRegistry ?? throw new ArgumentNullException(nameof(executionRegistry));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _ruleRunner = ruleRunner ?? throw new ArgumentNullException(nameof(ruleRunner));
        }

        public void Register<TEventContext>(IEcaRule<EcaExecutionContext<TEventContext>> rule,
            EcaRunMode runMode)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            _ruleRegistry.Register(rule);
            try
            {
                _executionRegistry.Register(rule, runMode, _ruleRunner);
            }
            catch
            {
                _ruleRegistry.Unregister(rule);
                throw;
            }
        }

        public bool Unregister<TEventContext>(IEcaRule<EcaExecutionContext<TEventContext>> rule)
        {
            // TODO: decide lifecycle of retained groups and active executions before adding Scope.
            return _ruleRegistry.Unregister(rule);
        }

        public void Fire(EcaEvent<EcaEventContextEmpty> ecaEvent)
        {
            Fire(ecaEvent, EcaEventContextEmpty.Value);
        }

        public void Fire<TEventContext>(EcaEvent<TEventContext> ecaEvent, TEventContext eventContext)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            var rules = _ruleSelector.ForEvent<EcaExecutionContext<TEventContext>>(ecaEvent);
            var passed = new List<(EcaRuleExecutionGroup<TEventContext> Group,
                EcaExecutionContext<TEventContext> Context)>(rules.Count);

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var group = _executionRegistry.Get<TEventContext>(rule.Id);
                var context = _contextFactory.Create(eventContext, group.State);
                if (_ruleChecker.Check(rule, context)) passed.Add((group, context));
            }

            // All Conditions of this Fire have been checked before any Action starts.
            for (var i = 0; i < passed.Count; i++)
                passed[i].Group.Fire(passed[i].Context);
        }
    }
}
