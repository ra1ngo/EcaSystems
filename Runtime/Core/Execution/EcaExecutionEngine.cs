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
        private readonly IEcaCommandRunner _commandRunner;
        private readonly IEcaRuleRunner _ruleRunner;

        public EcaExecutionEngine(IEcaCommandRunner commandRunner) : this(new EcaRuleRegistry(), commandRunner) {}

        private EcaExecutionEngine(IEcaRuleRegistry ruleRegistry, IEcaCommandRunner commandRunner)
            : this(ruleRegistry, new EcaRuleSelector(ruleRegistry), new EcaRuleChecker(),
                new EcaRuleExecutionRegistry(), commandRunner, new EcaRuleRunner()) {}

        public EcaExecutionEngine(IEcaRuleRegistry ruleRegistry, IEcaRuleSelector ruleSelector,
            IEcaRuleChecker ruleChecker, IEcaRuleExecutionRegistry executionRegistry,
            IEcaCommandRunner commandRunner, IEcaRuleRunner ruleRunner)
        {
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
            _ruleSelector = ruleSelector ?? throw new ArgumentNullException(nameof(ruleSelector));
            _ruleChecker = ruleChecker ?? throw new ArgumentNullException(nameof(ruleChecker));
            _executionRegistry = executionRegistry ?? throw new ArgumentNullException(nameof(executionRegistry));
            _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
            _ruleRunner = ruleRunner ?? throw new ArgumentNullException(nameof(ruleRunner));
        }

        public void Register<TEventContext>(IEcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> rule,
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

        public bool Unregister<TEventContext>(IEcaRule<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>> rule)
        {
            if (!_ruleRegistry.Unregister(rule)) return false;

            // Running Actions finish naturally through their existing group references.
            _executionRegistry.Remove(rule.Id);
            return true;
        }

        public void Fire(EcaEvent<EcaEventContextEmpty> ecaEvent)
        {
            Fire(ecaEvent, EcaEventContextEmpty.Value);
        }

        public void Fire<TEventContext>(EcaEvent<TEventContext> ecaEvent, TEventContext eventContext)
        {
            if (ecaEvent == null) throw new ArgumentNullException(nameof(ecaEvent));
            var rules = _ruleSelector.ForEvent<TEventContext, IEcaExecutionConditionContext<TEventContext>, IEcaExecutionActionContext<TEventContext>>(ecaEvent);
            var passed = new List<EcaRuleExecutionGroup<TEventContext>>(rules.Count);

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var group = _executionRegistry.Get<TEventContext>(rule.Id);
                var context = new EcaExecutionConditionContext<TEventContext>(eventContext, group.State);
                if (_ruleChecker.Check(rule, context)) passed.Add(group);
            }

            // All Conditions of this Fire have been checked before any Action starts.
            for (var i = 0; i < passed.Count; i++)
                passed[i].Fire(new EcaExecutionActionContext<TEventContext>(eventContext, passed[i].State, _commandRunner));
        }
    }
}
