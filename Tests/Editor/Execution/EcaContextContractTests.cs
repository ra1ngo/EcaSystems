using System;
using System.Threading.Tasks;
using EcaSystems.Core;
using EcaSystems.Tests.Support;
using NUnit.Framework;
using static EcaSystems.Tests.Support.ExecutionRules;
using static EcaSystems.Tests.Support.AsyncAssert;

namespace EcaSystems.Tests
{
    [TestFixture]
    public sealed class EcaContextContractTests : AsyncTestFixture
    {
        [Test]
        public async Task CommonExecutionContract_ExposesSameLiveStateAcrossRoles()
        {
            var engine = new EcaExecutionEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            var evt = new EcaEvent<TestEventContext>("context", "Context");
            var payload = new TestEventContext(1);
            IEcaExecutionContext<TestEventContext> observed = null;
            var action = Track(new ExecutionGateAction<TestEventContext>());
            var condition = new DelegateEcaCondition<IEcaExecutionConditionContext<TestEventContext>>(context =>
            {
                observed = context;
                Assert.That(observed.EventContext.Value, Is.EqualTo(payload.Value));
                Assert.That(observed.RuleExecutionGroupState.EcaRuleExecutionTotalStarted, Is.Zero);
                return true;
            });
            engine.Register(CreateExecutionRule("context.rule", evt, action, condition), new EcaRunMode(EcaOverlap.Allow));
            engine.Fire(evt, payload);
            Assert.That(observed.RuleExecutionGroupState, Is.SameAs(action.ExecutionGroupStates[0]));
            Assert.That(observed.RuleExecutionGroupState.EcaRuleExecutionTotalStarted, Is.EqualTo(1));
            action.CompleteAll();
            await WaitUntil(() => observed.RuleExecutionGroupState.EcaRuleExecutionTotalFinished == 1);
        }

        [Test]
        public void LayerContracts_PreserveExactPayloadAndRoleSeparation()
        {
            Assert.That(typeof(IEcaExecutionContext<TestEventContext>).IsAssignableFrom(
                typeof(IEcaExecutionActionContext<TestEventContext>)), Is.True);
            Assert.That(typeof(IEcaExecutionContext<TestEventContext>).IsAssignableFrom(
                typeof(IEcaScopeContext<TestEventContext>)), Is.True);
            Assert.That(typeof(IEcaContext<object>).IsAssignableFrom(
                typeof(IEcaExecutionContext<string>)), Is.True);
            foreach (var contract in new[] { typeof(IEcaExecutionContext<>), typeof(IEcaScopeContext<>),
                typeof(IEcaExecutionConditionContext<>), typeof(IEcaExecutionActionContext<>) })
                Assert.That(contract.MakeGenericType(typeof(object)).IsAssignableFrom(
                    contract.MakeGenericType(typeof(string))), Is.False, contract.Name);
            Assert.That(typeof(IEcaActionContext<TestEventContext>).IsAssignableFrom(
                typeof(IEcaExecutionConditionContext<TestEventContext>)), Is.False);
            Assert.That(typeof(IEcaCommandsActionContext<TestEventContext>).IsAssignableFrom(
                typeof(IEcaExecutionConditionContext<TestEventContext>)), Is.False);
        }
    }
}
