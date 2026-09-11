using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;
using NUnit.Framework;
using EcaSystems.Tests.Support;
using static EcaSystems.Tests.Support.ExecutionRules;
using static EcaSystems.Tests.Support.AsyncAssert;

namespace EcaSystems.Tests
{
    [TestFixture]
    public sealed class EcaCommandExecutionTests : AsyncTestFixture
    {
        [Test]
        public async Task Fire_BindsExactlyOncePerAcceptedExecution()
        {
            foreach (var overlap in new[] { EcaOverlap.Ignore, EcaOverlap.Allow })
            foreach (var limit in new[] { 0, 2 })
            {
                var checks = 0;
                var runner = new RecordingCommandRunner(new EcaCommandRunner(new EcaCommandRegistry()), new List<string>());
                var rules = new EcaRuleRegistry();
                var groups = new EcaRuleExecutionRegistry();
                var engine = new EcaExecutionEngine(rules, new EcaRuleSelector(rules), new EcaRuleChecker(),
                    groups, runner, new EcaRuleRunner());
                var evt = new EcaEvent<TestEventContext>("bind.acceptance", "Bind acceptance");
                var action = Track(new ExecutionGateAction<TestEventContext>());
                var rule = CreateExecutionRule("bind.acceptance", evt, action,
                    new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ => { checks++; return true; }));
                engine.Register(rule, new EcaRunMode(overlap, limit));
                var group = groups.Get<TestEventContext>(rule.Id);
                runner.OnBind = context =>
                {
                    var typed = (IEcaExecutionActionContext<TestEventContext>)context;
                    Assert.That(typed.EventContext.Value == 42 && ReferenceEquals(typed.RuleExecutionGroupState, group.State), Is.True, "Runner получает подготовленные payload и GroupState");
                    Assert.Catch<InvalidOperationException>(() => { _ = typed.Commands; }, "До завершения Bind чтение Commands явно отклоняется");
                };
                engine.Fire(evt, new TestEventContext(42));
                var first = limit == 0 ? 0 : 1;
                Assert.That(checks == 1 && runner.BindCount == first && action.RunCount == first &&
                    group.Executions.Count == first && group.State.EcaRuleExecutionTotalStarted == first, Is.True, overlap + ": первый Fire привязывает только принятый execution");
                if (first == 1)
                    Assert.That(group.Executions[0].Context.Commands != null, Is.True, "Опубликованный execution уже имеет Commands");
                engine.Fire(evt, new TestEventContext(42));
                var active = limit == 0 ? 0 : overlap == EcaOverlap.Ignore ? 1 : 2;
                Assert.That(checks == 2 && runner.BindCount == active && action.RunCount == active &&
                    group.Executions.Count == active && group.State.EcaRuleExecutionTotalStarted == active, Is.True, overlap + ": повторный Fire проверяет Condition без лишнего Bind");
                action.CompleteAll();
                await WaitUntil(() => group.Executions.Count == 0);
                engine.Fire(evt, new TestEventContext(42));
                var total = limit == 0 ? 0 : 2;
                Assert.That(checks == 3 && runner.BindCount == total && action.RunCount == total &&
                    group.State.EcaRuleExecutionTotalStarted == total, Is.True, overlap + ": после завершения Ignore снова разрешает Bind, исчерпанный Limit блокирует");
                action.CompleteAll();
                await WaitUntil(() => group.Executions.Count == 0);
                engine.Fire(evt, new TestEventContext(42));
                Assert.That(checks == 4 && runner.BindCount == total && group.Executions.Count == 0 &&
                    group.State.EcaRuleExecutionTotalStarted == total && group.State.EcaRuleExecutionTotalFinished == total, Is.True, overlap + ": Bind ровно один раз на фактический запуск");
            }
        }

        [Test]
        public async Task Fire_ConditionsPrecedeBindAndSequentialCommandsAcrossScopes()
        {
            var log = new List<string>();
            var registry = new EcaCommandRegistry();
            var runner = new RecordingCommandRunner(new EcaCommandRunner(registry), log);
            var received = new List<IEcaExecutionActionContext<TestEventContext>>();
            registry.Register(new TestCommand<IEcaExecutionActionContext<TestEventContext>, string>("record", (context, label) =>
            {
                received.Add(context);
                log.Add(label);
                return Task.CompletedTask;
            }));
            var evt = new EcaEvent<TestEventContext>("commands.order", "Order");
            using var scopes = new EcaScopeEngine(runner);
            var a = scopes.CreateScope();
            var b = scopes.CreateScope();
            var actionA = new CommandsTestAction("A", log);
            var actionB = new CommandsTestAction("B", log);
            var ruleA = CreateExecutionRule("A", evt, actionA,
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(context =>
                {
                    log.Add("condition A");
                    Assert.That(context.EventContext.Value == 42 && runner.BindCount == 0, Is.True, "Condition получает payload до bind");
                    return true;
                }));
            var ruleB = CreateExecutionRule("B", evt, actionB,
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ =>
                {
                    log.Add("condition B");
                    return true;
                }));
            var rejected = CreateExecutionRule("rejected", evt, new CommandsTestAction("rejected", log),
                new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(_ =>
                {
                    log.Add("condition rejected");
                    return false;
                }));
            a.Register(ruleA, new EcaRunMode(EcaOverlap.Allow, 1));
            a.Register(ruleB, new EcaRunMode(EcaOverlap.Allow, 1));
            a.Register(rejected, new EcaRunMode(EcaOverlap.Allow));
            a.Fire(evt, new TestEventContext(42));
            Assert.That(string.Join(",", log) ==
                "condition A,condition B,condition rejected,bind,action A,A1,A2,bind,action B,B1,B2", Is.True, "Все Conditions до любого bind, Action и Command");
            Assert.That(runner.BindCount == 2, Is.True, "False Condition не вызывает bind");
            Assert.That(received.Count == 4 &&
                ReferenceEquals(received[0], actionA.Contexts[0]) && ReferenceEquals(received[1], actionA.Contexts[0]) &&
                ReferenceEquals(received[2], actionB.Contexts[0]) && received[0].EventContext.Value == 42, Is.True, "Command автоматически получает точный ActionContext и payload");
            b.Register(ruleB, new EcaRunMode(EcaOverlap.Allow, 1));
            b.Fire(evt, new TestEventContext(99));
            Assert.That(received.Count == 6 && received[4].EventContext.Value == 99, Is.True, "Общая Command работает во втором Scope");
            Assert.That(actionB.Contexts.Count == 2 &&
                !ReferenceEquals(actionB.Contexts[0].RuleExecutionGroupState, actionB.Contexts[1].RuleExecutionGroupState) &&
                actionB.Contexts[0].RuleExecutionGroupState.EcaRuleExecutionTotalStarted == 1 &&
                actionB.Contexts[1].RuleExecutionGroupState.EcaRuleExecutionTotalStarted == 1, Is.True, "Одна Rule имеет независимые GroupState и Limit");
            a.Unregister(ruleB);
            b.Fire(evt, new TestEventContext(100));
            Assert.That(received.Count == 6 && b.Unregister(ruleB), Is.True, "Unregister в A не сбрасывает Limit в B");

            // Следующая Command должна дождаться завершения предыдущей, включая async-паузу.
            var gate = new TaskCompletionSource<bool>();
            registry.Register(new TestCommand<IEcaActionContext, int>("gate", (_, __) => gate.Task));
            var completed = false;
            var bound = runner.Bind(new EcaActionContext<int>(0));
            async Task Sequence()
            {
                await bound.Run("gate", 0);
                await bound.Run("nullable", 0);
                completed = true;
            }
            registry.Register(new TestCommand<IEcaActionContext, int>("nullable", (_, __) => Task.CompletedTask));
            var sequence = Sequence();
            Assert.That(!completed, Is.True, "Последовательность ожидает async Command");
            gate.SetResult(true);
            await sequence;
            Assert.That(completed, Is.True, "Следующая Command запускается после завершения предыдущей");
        }

        private sealed class CommandsTestAction : IEcaAction<IEcaExecutionActionContext<TestEventContext>>
        {
            private readonly string _label;
            private readonly List<string> _log;
            public List<IEcaExecutionActionContext<TestEventContext>> Contexts { get; } = new();
            public CommandsTestAction(string label, List<string> log) { _label = label; _log = log; }
            public async Task Run(IEcaExecutionActionContext<TestEventContext> context)
            {
                Contexts.Add(context);
                _log.Add("action " + _label);
                await context.Commands.Run("record", _label + "1");
                await context.Commands.Run("record", _label + "2");
            }
        }
    }
}
