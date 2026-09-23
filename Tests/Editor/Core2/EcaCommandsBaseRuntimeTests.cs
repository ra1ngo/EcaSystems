using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.CommandTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaCommandsBaseRuntimeTests
    {
        private BaseTestSupport.Event<int> _event;
        private EcaBaseRuntime _runtime;
        private EcaCommandRegistry _commands;
        private EcaCommandRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _event = new BaseTestSupport.Event<int>();
            var events = new EcaBaseEventRegistry();
            events.Register(_event);
            _runtime = new EcaBaseRuntime(new EcaBaseRuleRegistry(events), new EcaBaseActionRunner(), new EcaBaseConditionChecker());
            _commands = new EcaCommandRegistry();
            _runner = new EcaCommandRunner(_commands);
        }

        private void Fire(ActionInput context, int value = 7) =>
            _runtime.ForceFire<int, BaseTestSupport.State>(
                _event, value, (rule, payload) => new BaseTestSupport.State { RuleId = rule.Id, EventState = payload },
                new BaseTestSupport.ConditionContext(), context);

        [TestCase(true)]
        [TestCase(false)]
        public void ForceFire_ConditionControlsActionAndCommand(bool passes)
        {
            var context = new ActionInput();
            ActionInput commandContext = null, actionContext = null;
            var actions = 0;
            _commands.Register(new Command<ActionInput, int>
            {
                Handler = (c, args) => { commandContext = c; c.Total += args; return Task.CompletedTask; }
            });
            _runtime.Register(new Rule
            {
                Event = _event,
                Condition = new BaseTestSupport.Condition { CheckHandler = (state, c) => passes },
                Action = new CommandAction { Handler = (state, c) =>
                {
                    actions++;
                    actionContext = c;
                    return _runner.Run("command", state, c, state.EventState);
                } }
            });
            Fire(context, 17);
            Assert.That(actions, Is.EqualTo(passes ? 1 : 0));
            Assert.That(context.Total, Is.EqualTo(passes ? 17 : 0));
            Assert.That(commandContext, Is.SameAs(passes ? context : null));
            Assert.That(actionContext, Is.SameAs(passes ? context : null));
        }

        [Test]
        public void ForceFire_MultipleRulesKeepBarrierBeforeCommandSideEffects()
        {
            var trace = new List<string>();
            var context = new ActionInput();
            var totalsAtCheck = new List<int>();
            _commands.Register(new Command<ActionInput, string>
            {
                Handler = (c, id) => { trace.Add("Command " + id); c.Total++; return Task.CompletedTask; }
            });
            foreach (var id in new[] { "A", "B", "C" })
            {
                _runtime.Register(new Rule
                {
                    Id = id, Event = _event,
                    Condition = new BaseTestSupport.Condition { CheckHandler = (state, c) =>
                    {
                        trace.Add("Condition " + id);
                        totalsAtCheck.Add(context.Total);
                        return id != "B";
                    } },
                    Action = new CommandAction { Handler = (state, c) =>
                    {
                        trace.Add("Action " + id);
                        return _runner.Run("command", state, c, id);
                    } }
                });
            }
            Fire(context);
            Assert.That(trace, Is.EqualTo(new[]
            {
                "Condition A", "Condition B", "Condition C", "Action A", "Command A", "Action C", "Command C"
            }));
            Assert.That(totalsAtCheck, Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(context.Total, Is.EqualTo(2));
        }

        [Test]
        public void ForceFire_SeparateCallsKeepExplicitContextsIndependent()
        {
            var a = new ActionInput();
            var b = new ActionInput();
            var seen = new List<ActionInput>();
            _commands.Register(new Command<ActionInput, int>
            {
                Handler = (c, args) => { seen.Add(c); c.Total += args; return Task.CompletedTask; }
            });
            foreach (var id in new[] { "A", "B" })
            {
                _runtime.Register(new Rule
                {
                    Id = id, Event = _event,
                    Condition = new BaseTestSupport.Condition { CheckHandler = (state, c) => state.EventState == (id == "A" ? 1 : 2) },
                    Action = new CommandAction { Handler = (state, c) => _runner.Run("command", state, c, state.EventState) }
                });
            }
            Fire(a, 1);
            Fire(b, 2);
            Assert.That(seen, Is.EqualTo(new[] { a, b }));
            Assert.That(a.Total, Is.EqualTo(1));
            Assert.That(b.Total, Is.EqualTo(2));
        }

        [Test]
        public async Task ForceFire_ReturnsWhileActionHasUnfinishedCommandTask()
        {
            var gate = new TaskCompletionSource<bool>();
            var context = new ActionInput();
            Task commandTask = null, actionTask = null;
            _commands.Register(new Command<ActionInput, int>
            {
                Handler = async (c, args) => { await gate.Task; c.Total += args; }
            });
            _runtime.Register(new Rule
            {
                Event = _event,
                Action = new CommandAction { Handler = (state, c) =>
                {
                    commandTask = _runner.Run("command", state, c, state.EventState);
                    actionTask = commandTask;
                    return actionTask;
                } }
            });
            try
            {
                Fire(context);
                Assert.That(commandTask, Is.Not.Null);
                Assert.That(actionTask, Is.SameAs(commandTask));
                Assert.That(actionTask.IsCompleted, Is.False);
                Assert.That(context.Total, Is.Zero);
                gate.SetResult(true);
                Assert.That(await Task.WhenAny(actionTask, Task.Delay(5000)), Is.SameAs(actionTask));
                await actionTask;
                Assert.That(context.Total, Is.EqualTo(7));
            }
            finally { gate.TrySetResult(true); }
        }

        [Test]
        public void ForceFire_PropagatesCommandLookupFailureThroughSynchronousAction()
        {
            var context = new ActionInput();
            _runtime.Register(new Rule
            {
                Event = _event,
                Action = new CommandAction { Handler = (state, c) => _runner.Run("missing", state, c, state.EventState) }
            });
            Assert.Throws<InvalidOperationException>(() => Fire(context));
            Assert.That(context.Total, Is.Zero);
        }
    }
}
