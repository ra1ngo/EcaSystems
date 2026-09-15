using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.EventTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaEventsBaseRuntimeTests
    {
        [Test]
        public void OneSubscription_HandlesArbitraryUserClassAndStructRegisteredLater()
        {
            var events = new EcaBaseEventRegistry();
            var dispatcher = new EcaEventDispatcher(events);
            using var receiver = new EcaEventReceiver(events, dispatcher);
            using var runtime = new EcaTestBaseRuntime(events, receiver);
            var messageEvent = new BaseTestSupport.Event<UserMessage> { Id = "message" };
            var scoreEvent = new BaseTestSupport.Event<UserScore> { Id = "score" };
            events.Register(messageEvent);
            events.Register(scoreEvent);
            var seenMessages = new List<UserMessage>();
            var scores = new List<int>();
            runtime.Register(new Rule<UserMessage>
            {
                Id = "message.rule", Event = messageEvent,
                Action = new EventAction<UserMessage> { Handler = state => { seenMessages.Add(state); return Task.CompletedTask; } }
            });
            runtime.Register(new Rule<UserScore>
            {
                Id = "score.rule", Event = scoreEvent,
                Action = new EventAction<UserScore> { Handler = state => { scores.Add(state.Value); return Task.CompletedTask; } }
            });
            var message = new DerivedMessage { Text = "arbitrary payload" };
            dispatcher.Fire<UserMessage>(messageEvent, message);
            dispatcher.Fire(scoreEvent, new UserScore(42));
            dispatcher.Fire<UserMessage>(messageEvent, null);
            dispatcher.Fire(scoreEvent, new UserScore(17));
            Assert.That(seenMessages.Count, Is.EqualTo(2));
            Assert.That(seenMessages[0], Is.SameAs(message));
            Assert.That(seenMessages[0].Text, Is.EqualTo("arbitrary payload"));
            Assert.That(seenMessages[1], Is.Null);
            Assert.That(scores, Is.EqualTo(new[] { 42, 17 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Dispatch_ConditionControlsAction(bool passes)
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<UserScore>();
            events.Register(ecaEvent);
            var dispatcher = new EcaEventDispatcher(events);
            using var receiver = new EcaEventReceiver(events, dispatcher);
            using var runtime = new EcaTestBaseRuntime(events, receiver);
            var checks = 0;
            var actions = 0;
            runtime.Register(new Rule<UserScore>
            {
                Event = ecaEvent,
                Condition = new Condition<UserScore> { Handler = state => { checks++; Assert.That(state.Value, Is.EqualTo(5)); return passes; } },
                Action = new EventAction<UserScore> { Handler = state => { actions++; Assert.That(state.Value, Is.EqualTo(5)); return Task.CompletedTask; } }
            });
            dispatcher.Fire(ecaEvent, new UserScore(5));
            Assert.That(checks, Is.EqualTo(1));
            Assert.That(actions, Is.EqualTo(passes ? 1 : 0));
        }

        [Test]
        public void Dispatch_RuntimeSelectsMultipleRulesAndKeepsConditionBarrier()
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<UserScore>();
            var other = new BaseTestSupport.Event<UserScore> { Id = "other" };
            events.Register(ecaEvent);
            events.Register(other);
            var dispatcher = new EcaEventDispatcher(events);
            using var receiver = new EcaEventReceiver(events, dispatcher);
            using var runtime = new EcaTestBaseRuntime(events, receiver);
            var trace = new List<string>();
            foreach (var id in new[] { "A", "B", "C", "other" })
            {
                runtime.Register(new Rule<UserScore>
                {
                    Id = id, Event = id == "other" ? other : ecaEvent,
                    Condition = new Condition<UserScore> { Handler = state => { trace.Add("Check " + id); return id != "B"; } },
                    Action = new EventAction<UserScore> { Handler = state => { trace.Add("Action " + id); return Task.CompletedTask; } }
                });
            }
            dispatcher.Fire(ecaEvent, new UserScore(1));
            Assert.That(trace, Is.EqualTo(new[] { "Check A", "Check B", "Check C", "Action A", "Action C" }));
        }

        [Test]
        public void Dispatch_ReentrantFireCompletesInnerEventBeforeOuterActionReturns()
        {
            var events = new EcaBaseEventRegistry();
            var first = new BaseTestSupport.Event<UserMessage> { Id = "first" };
            var second = new BaseTestSupport.Event<UserScore> { Id = "second" };
            events.Register(first);
            events.Register(second);
            var dispatcher = new EcaEventDispatcher(events);
            using var receiver = new EcaEventReceiver(events, dispatcher);
            using var runtime = new EcaTestBaseRuntime(events, receiver);
            var trace = new List<string>();
            runtime.Register(new Rule<UserMessage>
            {
                Id = "first.rule", Event = first,
                Action = new EventAction<UserMessage> { Handler = state =>
                {
                    trace.Add(state.Text);
                    dispatcher.Fire(second, new UserScore(2));
                    trace.Add("returned");
                    return Task.CompletedTask;
                } }
            });
            runtime.Register(new Rule<UserScore>
            {
                Id = "second.rule", Event = second,
                Action = new EventAction<UserScore> { Handler = state => { trace.Add("inner " + state.Value); return Task.CompletedTask; } }
            });
            dispatcher.Fire(first, new UserMessage { Text = "outer" });
            Assert.That(trace, Is.EqualTo(new[] { "outer", "inner 2", "returned" }));
        }

        [Test]
        public void RuntimeUnsubscribe_LeavesReceiverAvailableToNextConsumer()
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<UserScore>();
            events.Register(ecaEvent);
            var dispatcher = new EcaEventDispatcher(events);
            using var receiver = new EcaEventReceiver(events, dispatcher);
            var runtime = new EcaTestBaseRuntime(events, receiver);
            var calls = 0;
            runtime.Register(new Rule<UserScore>
            {
                Event = ecaEvent,
                Action = new EventAction<UserScore> { Handler = state => { calls++; return Task.CompletedTask; } }
            });
            dispatcher.Fire(ecaEvent, new UserScore(1));
            runtime.Dispose();
            dispatcher.Fire(ecaEvent, new UserScore(2));
            var next = new RecordingHandler();
            receiver.Subscribe(next);
            dispatcher.Fire(ecaEvent, new UserScore(3));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(next.Types, Is.EqualTo(new[] { typeof(UserScore) }));
        }
    }
}
