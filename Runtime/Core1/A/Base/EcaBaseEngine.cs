using System;
using System.Collections.Generic;

namespace EcaSystems.Core1
{
    public sealed class EcaBaseEngine : IDisposable
    {
        private readonly EcaEventDispatcher _dispatcher;
        private readonly EcaRuleRegistry _rules;
        private readonly IEcaRuleRunner _runner;

        public EcaBaseEngine(EcaEventDispatcher dispatcher, EcaRuleRegistry rules, IEcaRuleRunner runner)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _dispatcher.Fired += OnFired;
        }

        public void Register<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext>(
            IEcaRule<TEventState, TRuleState, TConditionRunnerContext, TActionRunnerContext> rule)
            where TRuleState : IEcaRuleState<TEventState>
            where TConditionRunnerContext : IEcaConditionRunnerContext
            where TActionRunnerContext : IEcaActionRunnerContext
        {
            _runner.ValidateRule<TEventState>(rule);
            _rules.Register(rule);
        }

        public bool Unregister(IEcaRule rule) => _rules.Unregister(rule);

        private void OnFired(EcaEventOccurrence occurrence)
        {
            var rules = _rules.GetByEvent(occurrence.Event);
            var runs = new List<IEcaRuleRun>(rules.Count);
            for (var i = 0; i < rules.Count; i++) runs.Add(_runner.CreateRun(rules[i], occurrence));

            var passed = new List<IEcaRuleRun>(runs.Count);
            for (var i = 0; i < runs.Count; i++)
                if (_runner.Check(runs[i])) passed.Add(runs[i]);

            /* ALL CONDITIONS -> ALL ACTIONS для одного Fire. Вложенный Fire входит
               немедленно со своими локальными списками. Base не ожидает Task;
               синхронные исключения распространяются вызывающему коду. */
            for (var i = 0; i < passed.Count; i++) _ = _runner.RunAction(passed[i]);
        }

        public void Dispose() => _dispatcher.Fired -= OnFired;
    }
}
