using System;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.BaseTestSupport;
using Action = EcaSystems.Tests.Core2.BaseTestSupport.Action;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaRuleRunnerTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void ConditionChecker_ForwardsTypedArgumentsAndResult(bool result)
        {
            var state = new State { EventState = 42 };
            var context = new ConditionContext();
            var calls = 0;
            var condition = new Condition
            {
                CheckHandler = (receivedState, receivedContext) =>
                {
                    calls++;
                    Assert.That(receivedState, Is.SameAs(state));
                    Assert.That(receivedContext, Is.SameAs(context));
                    return result;
                }
            };

            IEcaConditionChecker checker = new EcaBaseConditionChecker();
            Assert.That(checker.Check(condition, state, context), Is.EqualTo(result));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ConditionChecker_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EcaBaseConditionChecker().Check<State, ConditionContext>(null, new State(), new ConditionContext()));
        }

        [Test]
        public void ActionRunner_ForwardsTypedArgumentsAndReturnsOriginalTask()
        {
            var state = new State { EventState = 42 };
            var context = new ActionContext();
            var completion = new TaskCompletionSource<bool>();
            var calls = 0;
            var action = new Action
            {
                RunHandler = (receivedState, receivedContext) =>
                {
                    calls++;
                    Assert.That(receivedState, Is.SameAs(state));
                    Assert.That(receivedContext, Is.SameAs(context));
                    return completion.Task;
                }
            };

            IEcaActionRunner runner = new EcaBaseActionRunner();
            var task = runner.Run(action, state, context);
            Assert.That(task, Is.SameAs(completion.Task));
            Assert.That(task.IsCompleted, Is.False);
            Assert.That(calls, Is.EqualTo(1));
            completion.SetResult(true);
            Assert.That(task.IsCompleted, Is.True);
        }

        [Test]
        public void ActionRunner_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new EcaBaseActionRunner().Run<State, ActionContext>(null, new State(), new ActionContext()));
        }
    }
}
