using System;
using System.Reflection;
using System.Threading.Tasks;
using EcaSystems.Core;
using EcaSystems.Tests.Support;
using NUnit.Framework;
using static EcaSystems.Tests.Support.ExecutionRules;
using static EcaSystems.Tests.Support.AsyncAssert;

namespace EcaSystems.Tests
{
    [TestFixture]
    public sealed class EcaExecutionLifecycleTests : AsyncTestFixture
    {
        [Test]
        public void Construct_Execution_StartsPendingWithoutChangingCounters()
        {
            var rule = CreateExecutionRule("pending", new EcaEvent<int>("event", "Event"),
                Track(new ExecutionGateAction<int>()));
            var state = new EcaRuleExecutionGroupState();
            var context = (EcaExecutionActionContext<int>)Activator.CreateInstance(typeof(EcaExecutionActionContext<int>),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 42, state }, null);
            // Pending exists only between the internal constructor and MarkRunning.
            // Reflection keeps this narrow lifecycle check out of the production API.
            var execution = (EcaRuleExecution<int>)Activator.CreateInstance(typeof(EcaRuleExecution<int>),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 7L, rule, context }, null);

            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Pending));
            Assert.That(execution.Id, Is.EqualTo(7));
            Assert.That(execution.RuleId, Is.EqualTo(rule.Id));
            Assert.That(execution.Rule, Is.SameAs(rule));
            Assert.That(execution.Context, Is.SameAs(context));
            Assert.That(execution.Exception, Is.Null);
            Assert.That(state.EcaRuleExecutionTotalStarted, Is.Zero);
            Assert.That(state.EcaRuleExecutionTotalFinished, Is.Zero);
        }

        [Test]
        public async Task Fire_FailedAction_ConsumesLimitAndRetainsOriginalFailure()
        {
            var action = Track(new ExecutionGateAction<int>());
            var rule = CreateExecutionRule("failed-limit", new EcaEvent<int>("event", "Event"), action);
            var group = new EcaRuleExecutionGroup<int>(rule, new EcaRunMode(EcaOverlap.Allow, 1), new EcaRuleRunner());
            var runner = new EcaCommandRunner(new EcaCommandRegistry());
            group.Fire(1, runner);
            var execution = group.Executions[0];
            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Running));
            var error = new InvalidOperationException("Expected failure");
            action.FailAll(error);
            await WaitUntil(() => group.Executions.Count == 0);
            group.Fire(2, runner);

            Assert.That(execution.Status, Is.EqualTo(EcaRuleExecutionStatus.Failed));
            Assert.That(execution.Exception, Is.SameAs(error));
            Assert.That(action.RunCount, Is.EqualTo(1));
            Assert.That(group.State.EcaRuleExecutionTotalStarted, Is.EqualTo(1));
            Assert.That(group.State.EcaRuleExecutionTotalFinished, Is.EqualTo(1));
            Assert.That(group.Executions, Is.Empty);
        }
    }
}
