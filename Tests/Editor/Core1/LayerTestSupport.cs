using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;

namespace EcaSystems.Tests.Core1
{
    internal static class LayerRules
    {
        internal static EcaRule<T, EcaExecutionRuleState<T>, IEcaExecutionConditionRunnerContext, IEcaExecutionActionRunnerContext>
            Execution<T>(string id, IEcaEvent<T> evt, Func<EcaExecutionRuleState<T>, Task> action,
                Func<EcaExecutionRuleState<T>, bool> condition = null) => new(id, id, evt,
                    new TestAction<EcaExecutionRuleState<T>>((s, _) => action(s)),
                    condition == null ? null : new TestCondition<EcaExecutionRuleState<T>>((s, _) => condition(s)));

        internal static EcaRule<T, EcaScopeRuleState<T>, IEcaScopeConditionRunnerContext, IEcaScopeActionRunnerContext>
            Scope<T>(string id, IEcaEvent<T> evt, Func<EcaScopeRuleState<T>, Task> action,
                Func<EcaScopeRuleState<T>, bool> condition = null) => new(id, id, evt,
                    new TestAction<EcaScopeRuleState<T>>((s, _) => action(s)),
                    condition == null ? null : new TestCondition<EcaScopeRuleState<T>>((s, _) => condition(s)));

        internal static EcaEvent<T> Event<T>(EcaEventRegistry events, string id = "event")
        {
            var evt = new EcaEvent<T>(id, id);
            events.Register(evt);
            return evt;
        }

        internal static EcaRuleExecutionGroup Group(EcaExecutionEngine engine, string id)
        {
            Assert.That(engine.TryGetGroup(id, out var group), Is.True);
            return group;
        }

        internal static async Task WaitUntil(Func<bool> predicate)
        {
            var timer = Stopwatch.StartNew();
            while (!predicate() && timer.ElapsedMilliseconds < 5000) await Task.Delay(1);
            Assert.That(predicate(), Is.True, "Асинхронная операция не завершилась за 5 секунд.");
        }
    }

    /* Управляемые Tasks завершаются и при падении проверки, без фоновой работы. */
    internal sealed class ActionGate<T> : IDisposable
    {
        internal readonly List<T> States = new();
        private readonly List<TaskCompletionSource<bool>> _pending = new();
        internal Task Run(T state)
        {
            States.Add(state);
            var source = new TaskCompletionSource<bool>();
            _pending.Add(source);
            return source.Task;
        }
        internal void Complete(int index) => _pending[index].TrySetResult(true);
        internal void Fail(int index, Exception exception) => _pending[index].TrySetException(exception);
        public void Dispose()
        {
            foreach (var pending in _pending) pending.TrySetResult(true);
        }
    }
}
