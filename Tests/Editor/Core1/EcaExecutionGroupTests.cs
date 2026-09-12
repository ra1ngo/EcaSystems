using System;
using System.Reflection;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;
using static EcaSystems.Tests.Core1.LayerRules;

namespace EcaSystems.Tests.Core1
{
    [TestFixture]
    public sealed class EcaExecutionGroupTests
    {
        private static IEcaRule Rule(string id = "rule") => Execution(id, new EcaEvent<int>("event", "Event"), _ => Task.CompletedTask);

        [Test]
        public async Task Lifecycle_PendingRunningCompleted_ActiveListIsNotHistory()
        {
            var rule = Rule();
            /* Pending кратковременный: проверяем закрытый конструктор без расширения public API. */
            var pending = (EcaRuleExecution)Activator.CreateInstance(typeof(EcaRuleExecution),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1L, rule }, null);
            Assert.That(pending.Status, Is.EqualTo(EcaRuleExecutionStatus.Pending));
            var group = new EcaRuleExecutionGroup(rule, new EcaExecutionMode(EcaOverlap.Allow));
            using var gate = new ActionGate<int>();
            var lifecycle = group.Run(() => gate.Run(1));
            var execution = group.Executions[0];
            Assert.That(lifecycle.IsCompleted, Is.False);
            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Running));
            Assert.That(execution.Rule, Is.SameAs(rule));
            Assert.That(execution.RuleId, Is.EqualTo(rule.Id));
            Assert.That(execution.Exception, Is.Null);
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(group.State.TotalFinished, Is.Zero);
            gate.Complete(0);
            await lifecycle;
            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Completed));
            Assert.That(group.Executions, Is.Empty);
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
        }

        [TestCase("sync")]
        [TestCase("faulted")]
        [TestCase("null")]
        [TestCase("canceled")]
        public async Task Failures_AreObservedAndConsumeLimit(string kind)
        {
            var group = new EcaRuleExecutionGroup(Rule(), new EcaExecutionMode(EcaOverlap.Allow, 1));
            var error = new InvalidOperationException("expected");
            EcaRuleExecution execution = null;
            var calls = 0;
            await group.Run(() =>
            {
                calls++;
                execution = group.Executions[0];
                if (kind == "sync") throw error;
                if (kind == "null") return null;
                if (kind == "canceled") return Task.FromCanceled(new System.Threading.CancellationToken(true));
                return Task.FromException(error);
            });
            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Failed));
            if (kind == "sync" || kind == "faulted") Assert.That(execution.Exception, Is.SameAs(error));
            else Assert.That(execution.Exception, Is.Not.Null);
            await group.Run(() => { calls++; return Task.CompletedTask; });
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(group.State.TotalStarted, Is.EqualTo(1));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
            Assert.That(group.Executions, Is.Empty);
        }

        [Test]
        public async Task DelayedFailure_PreservesOriginalExceptionAndFinishesExactlyOnce()
        {
            var group = new EcaRuleExecutionGroup(Rule(), new EcaExecutionMode(EcaOverlap.Ignore));
            using var gate = new ActionGate<int>();
            var task = group.Run(() => gate.Run(0));
            var execution = group.Executions[0];
            var error = new InvalidOperationException("delayed failure");
            gate.Fail(0, error);
            await task;
            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Failed));
            Assert.That(execution.Exception, Is.SameAs(error));
            Assert.That(group.State.TotalFinished, Is.EqualTo(1));
            Assert.That(group.Executions, Is.Empty);
        }

        [Test]
        public void Registry_IsNonGeneric_ValidatesIdentityModeAndNewLifetime()
        {
            var registry = new EcaRuleExecutionRegistry();
            var rule = Rule();
            var mode = new EcaExecutionMode(EcaOverlap.Allow, 2);
            registry.Register(rule, mode);
            var old = registry.Get(rule.Id);
            registry.Register(rule, new EcaExecutionMode(EcaOverlap.Allow, 2));
            Assert.That(registry.Get(rule.Id), Is.SameAs(old));
            Assert.Throws<InvalidOperationException>(() => registry.Register(Rule(), mode));
            Assert.Throws<InvalidOperationException>(() => registry.Register(rule, new EcaExecutionMode(EcaOverlap.Ignore, 2)));
            Assert.Throws<InvalidOperationException>(() => registry.Register(rule, new EcaExecutionMode(EcaOverlap.Allow, 3)));
            Assert.That(registry.TryGet("missing", out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => registry.Get("missing"));
            Assert.That(registry.Remove(rule.Id), Is.True);
            registry.Register(rule, mode);
            Assert.That(registry.Get(rule.Id).State, Is.Not.SameAs(old.State));
            registry.Clear();
            Assert.That(registry.TryGet(rule.Id, out _), Is.False);
        }

        [Test]
        public void Mode_RejectsInvalidLimitAndOverlap()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EcaExecutionMode(EcaOverlap.Allow, -2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EcaExecutionMode((EcaOverlap)99));
            Assert.That(new EcaExecutionMode(EcaOverlap.Ignore).Limit, Is.EqualTo(-1));
        }
    }
}
