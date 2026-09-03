using System;
using System.Collections.Generic;
using EcaRuleId = System.String;

namespace EcaSystems.Core
{
    public sealed class EcaRuleRuntimeEngine
    {
        private readonly IEcaRuleRegistry _ruleRegistry;
        private readonly IEcaRuleChecker _ruleChecker;
        private readonly IEcaRuleRunner _ruleRunner;
        private readonly EcaRuleRuntimeRegistry _runtimeRegistry;

        public EcaRuleRuntimeEngine()
            : this(
                new EcaRuleRegistry(),
                new EcaRuleChecker(),
                new EcaRuleRunner(),
                new EcaRuleRuntimeRegistry())
        {
        }

        public EcaRuleRuntimeEngine(
            IEcaRuleRegistry ruleRegistry,
            IEcaRuleChecker ruleChecker,
            IEcaRuleRunner ruleRunner,
            EcaRuleRuntimeRegistry runtimeRegistry)
        {
            _ruleRegistry = ruleRegistry ?? throw new ArgumentNullException(nameof(ruleRegistry));
            _ruleChecker = ruleChecker ?? throw new ArgumentNullException(nameof(ruleChecker));
            _ruleRunner = ruleRunner ?? throw new ArgumentNullException(nameof(ruleRunner));
            _runtimeRegistry = runtimeRegistry ?? throw new ArgumentNullException(nameof(runtimeRegistry));
        }

        public void Register<TEventContext>(
            IEcaRule<EcaRuntimeContext<TEventContext>> rule,
            EcaOverlap overlap)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            _ruleRegistry.Register(rule);

            try
            {
                _runtimeRegistry.GetOrCreate(rule.Id, overlap);
            }
            catch
            {
                _ruleRegistry.Unregister(rule);
                throw;
            }
        }

        public bool Unregister<TEventContext>(
            IEcaRule<EcaRuntimeContext<TEventContext>> rule)
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

            var rules =
                _ruleRegistry.GetRulesForEvent<EcaRuntimeContext<TEventContext>>(ecaEvent);

            var contexts =
                new Dictionary<EcaRuleId, EcaRuntimeContext<TEventContext>>(rules.Count);

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                var runtime = _runtimeRegistry.Get(rule.Id);

                contexts.Add(
                    rule.Id,
                    new EcaRuntimeContext<TEventContext>(
                        eventContext,
                        runtime.State)
                );
            }

            var checkedRules =
                _ruleChecker.Check(
                    rules,
                    rule => contexts[rule.Id]
                );

            var pending = new List<(
                IEcaRule<EcaRuntimeContext<TEventContext>> Rule,
                EcaRuntimeContext<TEventContext> Context,
                EcaRuleRuntime Runtime,
                EcaRuleExecution Execution
            )>(checkedRules.Count);

            for (var i = 0; i < checkedRules.Count; i++)
            {
                var rule = checkedRules[i];
                var runtime = _runtimeRegistry.Get(rule.Id);

                if (!runtime.TryCreateExecution(out var execution))
                    continue;

                pending.Add((
                    rule,
                    contexts[rule.Id],
                    runtime,
                    execution
                ));
            }

            for (var i = 0; i < pending.Count; i++)
            {
                var item = pending[i];

                _ = item.Runtime.Run(
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