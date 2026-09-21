using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.CommandTestSupport;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaCommandStateTests
    {
        private class State : IEcaRuleState { public int Value; }
        private sealed class DerivedState : State { }
        private sealed class OtherState : IEcaRuleState { }
        private sealed class StatefulCommand : AEcaCommand<State, Context, Args>
        {
            public override string Id => "stateful";
            public Func<State, Context, Args, Task> Handler;
            public override Task Run(State state, Context context, Args args) => Handler(state, context, args);
        }

        [Test]
        public void NonGenericBridgePreservesStateMetadataReferencesAndTask()
        {
            var state = new DerivedState();
            var context = new DerivedContext();
            var args = new DerivedArgs();
            var task = Task.FromResult(1);
            AEcaCommand command = new StatefulCommand { Handler = (s, c, a) =>
            {
                Assert.That(s, Is.SameAs(state));
                Assert.That(c, Is.SameAs(context));
                Assert.That(a, Is.SameAs(args));
                return task;
            } };
            Assert.That(command.RuleStateType, Is.EqualTo(typeof(State)));
            Assert.That(command.ContextType, Is.EqualTo(typeof(Context)));
            Assert.That(command.ArgsType, Is.EqualTo(typeof(Args)));
            Assert.That(command.Run(state, context, args), Is.SameAs(task));
            var registry = new EcaCommandRegistry();
            registry.Register(command);
            // Runtime compatibility permits the same object passed through the base state interface.
            Assert.That(new EcaCommandRunner(registry).Run<IEcaRuleState, Args>(command.Id, state, context, args), Is.SameAs(task));
        }

        [Test]
        public void RunnerRejectsIncompatibleStateAndNonNullContextButPassesNullContext()
        {
            var calls = 0;
            var state = new State();
            var registry = new EcaCommandRegistry();
            registry.Register(new StatefulCommand { Handler = (s, c, a) =>
            { Assert.That(s, Is.SameAs(state)); Assert.That(c, Is.Null); calls++; return Task.CompletedTask; } });
            IEcaCommands runner = new EcaCommandRunner(registry);
            Assert.Throws<InvalidOperationException>(() => runner.Run("stateful", new OtherState(), null, new Args()));
            Assert.Throws<InvalidOperationException>(() => runner.Run("stateful", state, new ActionInput(), new Args()));
            Assert.That(calls, Is.Zero);
            runner.Run("stateful", state, null, new Args()).GetAwaiter().GetResult();
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public async Task OverlappingCallsKeepExactStateContextAndArgsAfterAwait()
        {
            var firstGate = new TaskCompletionSource<bool>();
            var secondGate = new TaskCompletionSource<bool>();
            var states = new[] { new State(), new DerivedState() };
            var contexts = new[] { new Context(), new DerivedContext() };
            var args = new[] { new Args { Value = 3 }, new DerivedArgs { Value = 5 } };
            var seen = new List<(State, Context, Args)>();
            var registry = new EcaCommandRegistry();
            registry.Register(new StatefulCommand { Handler = async (s, c, a) =>
            {
                await (ReferenceEquals(s, states[0]) ? firstGate.Task : secondGate.Task);
                seen.Add((s, c, a));
                s.Value += a.Value;
                c.Value += a.Value;
            } });
            var runner = new EcaCommandRunner(registry);
            var first = runner.Run("stateful", states[0], contexts[0], args[0]);
            var second = runner.Run("stateful", states[1], contexts[1], args[1]);
            try
            {
                Assert.That(first.IsCompleted || second.IsCompleted, Is.False);
                registry.Unregister("stateful");
                Assert.Throws<InvalidOperationException>(() => runner.Run("stateful", states[0], contexts[0], args[0]));
                secondGate.SetResult(true);
                await second;
                Assert.That(first.IsCompleted, Is.False);
                Assert.That(seen[0], Is.EqualTo((states[1], contexts[1], args[1])));
                firstGate.SetResult(true);
                await first;
                Assert.That(seen[1], Is.EqualTo((states[0], contexts[0], args[0])));
                Assert.That(states[0].Value, Is.EqualTo(3));
                Assert.That(states[1].Value, Is.EqualTo(5));
                Assert.That(contexts[0].Value, Is.EqualTo(3));
                Assert.That(contexts[1].Value, Is.EqualTo(5));
            }
            finally { firstGate.TrySetResult(true); secondGate.TrySetResult(true); }
        }
    }
}
