using System;
using System.Threading.Tasks;
using EcaSystems.Core1;

namespace EcaSystems.Tests.Core1
{
    internal sealed class BaseFixture : IDisposable
    {
        internal readonly EcaEventRegistry Events = new();
        internal readonly EcaRuleRegistry Rules;
        internal readonly EcaEventDispatcher Dispatcher;
        internal readonly EcaBaseEngine Engine;

        internal BaseFixture(IEcaRuleRunner runner = null)
        {
            Rules = new EcaRuleRegistry(Events);
            Dispatcher = new EcaEventDispatcher(Events);
            Engine = new EcaBaseEngine(Dispatcher, Rules, runner ?? new EcaRuleRunner());
        }

        internal EcaEvent<T> Event<T>(string id)
        {
            var ecaEvent = new EcaEvent<T>(id, id);
            Events.Register(ecaEvent);
            return ecaEvent;
        }

        internal EcaRule<T> Rule<T>(string id, IEcaEvent<T> ecaEvent,
            Action<EcaRuleState<T>> action, Func<EcaRuleState<T>, bool> condition = null)
        {
            var rule = new EcaRule<T>(id, id, ecaEvent,
                new TestAction<EcaRuleState<T>>((s, _) => { action(s); return Task.CompletedTask; }),
                condition == null ? null : new TestCondition<EcaRuleState<T>>((s, _) => condition(s)));
            Rules.Register(rule);
            return rule;
        }

        public void Dispose() => Engine.Dispose();
    }

    internal sealed class TestCondition<T> : IEcaCondition<T, IEcaConditionRunnerContext> where T : IEcaRuleState
    {
        private readonly Func<T, IEcaConditionRunnerContext, bool> _check;
        public string Id => "custom.condition";
        public string Name => "Custom condition";
        public string Description => "Programmatic condition";
        internal TestCondition(Func<T, IEcaConditionRunnerContext, bool> check) { _check = check; }
        public bool Check(T state, IEcaConditionRunnerContext runnerContext) => _check(state, runnerContext);
    }

    internal sealed class TestAction<T> : IEcaAction<T, IEcaActionRunnerContext> where T : IEcaRuleState
    {
        private readonly Func<T, IEcaActionRunnerContext, Task> _run;
        public string Id => "custom.action";
        public string Name => "Custom action";
        public string Description => "Programmatic action";
        internal TestAction(Func<T, IEcaActionRunnerContext, Task> run) { _run = run; }
        public Task Run(T state, IEcaActionRunnerContext runnerContext) => _run(state, runnerContext);
    }
}
