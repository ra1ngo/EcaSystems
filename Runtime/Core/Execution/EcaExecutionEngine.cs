using System;
using System.Collections.Generic;

using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaExecutionEngine
    {
        private readonly IEcaRuleRegistry _ruleRegistry;
        private readonly IEcaRuleChecker _ruleChecker;
        private readonly IEcaRuleRunner _ruleRunner;
        private readonly EcaRuleExecutionRegistry _executionRegistry;

        public EcaExecutionEngine()
            : this(
                new EcaRuleRegistry(),
                new EcaRuleChecker(),
                new EcaRuleRunner(),
                new EcaRuleExecutionRegistry())
        {
        }

        public EcaExecutionEngine(
            IEcaRuleRegistry ruleRegistry,
            IEcaRuleChecker ruleChecker,
            IEcaRuleRunner ruleRunner,
            EcaRuleExecutionRegistry executionRegistry)
        {
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
            _ruleChecker = ruleChecker ?? throw new ArgumentNullException(nameof(ruleChecker));
            _ruleRunner = ruleRunner ?? throw new ArgumentNullException(nameof(ruleRunner));
            _executionRegistry = executionRegistry ?? throw new ArgumentNullException(nameof(executionRegistry));
        }

        public void Register<TEventContext>(
            IEcaRule<EcaExecutionContext<TEventContext>> rule,
            EcaOverlap overlap)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            _ruleRegistry.Register(rule);

            try
            {
                _executionRegistry.GetOrCreate(rule.Id, overlap);
            }
            catch
            {
                _ruleRegistry.Unregister(rule);
                throw;
            }
        }

        public bool Unregister<TEventContext>(
            IEcaRule<EcaExecutionContext<TEventContext>> rule)
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

            var rules = _ruleRegistry.GetRulesForEvent<EcaExecutionContext<TEventContext>>(ecaEvent);

            var contexts = new Dictionary<EcaRuleId, EcaExecutionContext<TEventContext>>(rules.Count);

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var group = _executionRegistry.Get(rule.Id);

                contexts.Add(
                    rule.Id,
                    new EcaExecutionContext<TEventContext>(
                        eventContext,
                        group.State
                    )
                );
            }

            var checkedRules = _ruleChecker.Check(
                rules,
                rule => contexts[rule.Id]
            );

            var pending = new List<(
                IEcaRule<EcaExecutionContext<TEventContext>> Rule,
                EcaExecutionContext<TEventContext> Context,
                EcaRuleExecutionGroup Group,
                EcaRuleExecution Execution
            )>(checkedRules.Count);

            for (var i = 0; i < checkedRules.Count; i++)
            {
                var rule = checkedRules[i];
                var group = _executionRegistry.Get(rule.Id);

                if (!group.TryCreateExecution(out var execution))
                    continue;

                pending.Add((
                    rule,
                    contexts[rule.Id],
                    group,
                    execution
                ));
            }

            for (var i = 0; i < pending.Count; i++)
            {
                var item = pending[i];

                _ = item.Group.Run(
                    item.Execution,
                    token => _ruleRunner.Run(
                        item.Rule,
                        item.Context,
                        token
                    )
                );
            }
        }
    }
}