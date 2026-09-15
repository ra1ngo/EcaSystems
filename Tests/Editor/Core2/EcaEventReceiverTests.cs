using System;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.EventTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaEventReceiverTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Factory_PreservesDeclaredTypeAndSameEventThroughReceiver(bool nullState)
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<UserMessage>();
            events.Register(ecaEvent);
            UserMessage state = nullState ? null : new DerivedMessage { Text = "payload" };
            var occurrence = EcaEventOccurrence.Create(ecaEvent, state);
            Assert.That(occurrence.Event, Is.SameAs(ecaEvent));
            var source = new Source();
            using var receiver = new EcaEventReceiver(events, source);
            var handler = new RecordingHandler();
            receiver.Subscribe(handler);
            source.Emit(occurrence);
            Assert.That(handler.Events[0], Is.SameAs(occurrence.Event));
            Assert.That(handler.Types, Is.EqualTo(new[] { typeof(UserMessage) }));
            Assert.That(handler.States[0], Is.SameAs(state));
        }

        [Test]
        public void CustomSource_PublicFactorySupportsArbitraryTypesWithOneSubscription()
        {
            var events = new EcaBaseEventRegistry();
            var messageEvent = new BaseTestSupport.Event<UserMessage> { Id = "message" };
            var scoreEvent = new BaseTestSupport.Event<UserScore> { Id = "score" };
            events.Register(messageEvent);
            var source = new Source();
            using var receiver = new EcaEventReceiver(events, source);
            var handler = new RecordingHandler();
            receiver.Subscribe(handler);
            var message = new UserMessage { Text = "custom source" };
            source.Fire(messageEvent, message);
            Assert.Throws<InvalidOperationException>(() => source.Fire(scoreEvent, new UserScore(7)));
            Assert.That(handler.States.Count, Is.EqualTo(1));
            events.Register(scoreEvent);
            source.Fire(scoreEvent, new UserScore(42));
            Assert.That(handler.Types, Is.EqualTo(new[] { typeof(UserMessage), typeof(UserScore) }));
            Assert.That(handler.Events, Is.EqualTo(new IEcaEvent[] { messageEvent, scoreEvent }));
            Assert.That(handler.States[0], Is.SameAs(message));
            Assert.That(((UserScore)handler.States[1]).Value, Is.EqualTo(42));
            Assert.That(source.Adds, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Reception_ValidatesFakeSourceIndependentlyEvenWithoutHandler(bool subscribe)
        {
            var events = new EcaBaseEventRegistry();
            var source = new Source();
            using var receiver = new EcaEventReceiver(events, source);
            var handler = new RecordingHandler();
            if (subscribe) receiver.Subscribe(handler);
            var ecaEvent = new BaseTestSupport.Event<int>();
            var occurrence = EcaEventOccurrence.Create<int>(ecaEvent, 42);
            Assert.Throws<InvalidOperationException>(() => source.Emit(occurrence));
            Assert.That(handler.States, Is.Empty);
            events.Register(ecaEvent);
            source.Emit(occurrence);
            if (subscribe) Assert.That(handler.Events[0], Is.SameAs(occurrence.Event));
            Assert.That(handler.Types.Count, Is.EqualTo(subscribe ? 1 : 0));
            Assert.That(source.Adds, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Subscribe_RejectsDuplicateWithoutReplacingHandler(bool sameInstance)
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            var source = new Source();
            using var receiver = new EcaEventReceiver(events, source);
            var first = new RecordingHandler();
            var second = sameInstance ? first : new RecordingHandler();
            receiver.Subscribe(first);
            Assert.Throws<InvalidOperationException>(() => receiver.Subscribe(second));
            source.Emit(EcaEventOccurrence.Create<int>(ecaEvent, 1));
            Assert.That(first.States, Is.EqualTo(new object[] { 1 }));
            if (!sameInstance) Assert.That(second.States, Is.Empty);
        }

        [Test]
        public void Unsubscribe_RequiresSameInstanceAndAllowsNewHandler()
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            var source = new Source();
            using var receiver = new EcaEventReceiver(events, source);
            var first = new RecordingHandler();
            var second = new RecordingHandler();
            Assert.That(receiver.Unsubscribe(first), Is.False);
            receiver.Subscribe(first);
            Assert.That(receiver.Unsubscribe(second), Is.False);
            source.Emit(EcaEventOccurrence.Create<int>(ecaEvent, 1));
            Assert.That(receiver.Unsubscribe(first), Is.True);
            Assert.That(receiver.Unsubscribe(first), Is.False);
            source.Emit(EcaEventOccurrence.Create<int>(ecaEvent, 2));
            receiver.Subscribe(second);
            source.Emit(EcaEventOccurrence.Create<int>(ecaEvent, 3));
            Assert.That(first.States, Is.EqualTo(new object[] { 1 }));
            Assert.That(second.States, Is.EqualTo(new object[] { 3 }));
            Assert.That(source.Adds, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_DetachesOnceAndIgnoresAlreadyCapturedCallback()
        {
            var events = new EcaBaseEventRegistry();
            var source = new Source();
            var receiver = new EcaEventReceiver(events, source);
            var handler = new RecordingHandler();
            receiver.Subscribe(handler);
            var captured = source.Snapshot();
            receiver.Dispose();
            receiver.Dispose();
            Assert.That(source.Removes, Is.EqualTo(1));
            Assert.That(source.Snapshot(), Is.Null);
            var occurrence = EcaEventOccurrence.Create<int>(new BaseTestSupport.Event<int>(), 1);
            source.Emit(occurrence);
            captured(occurrence);
            Assert.That(handler.States, Is.Empty);
            Assert.Throws<ObjectDisposedException>(() => receiver.Subscribe(handler));
            Assert.Throws<ObjectDisposedException>(() => receiver.Unsubscribe(handler));
        }

        [Test]
        public void Receiver_RejectsNullDependenciesHandlerAndOccurrence()
        {
            var events = new EcaBaseEventRegistry();
            var source = new Source();
            Assert.Throws<ArgumentNullException>(() => new EcaEventReceiver(null, source));
            Assert.Throws<ArgumentNullException>(() => new EcaEventReceiver(events, null));
            Assert.That(source.Adds, Is.Zero);
            using var receiver = new EcaEventReceiver(events, source);
            Assert.Throws<ArgumentNullException>(() => receiver.Subscribe(null));
            Assert.Throws<ArgumentNullException>(() => receiver.Unsubscribe(null));
            Assert.Throws<ArgumentNullException>(() => source.Emit(null));
        }

        [Test]
        public void HandlerFailure_PropagatesUnchangedAndSubscriptionRemains()
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            var source = new Source();
            using var receiver = new EcaEventReceiver(events, source);
            var error = new InvalidOperationException("handler");
            var handler = new RecordingHandler { Callback = () => throw error };
            receiver.Subscribe(handler);
            Assert.That(Assert.Throws<InvalidOperationException>(() => source.Emit(EcaEventOccurrence.Create<int>(ecaEvent, 1))), Is.SameAs(error));
            handler.Callback = null;
            source.Emit(EcaEventOccurrence.Create<int>(ecaEvent, 2));
            Assert.That(handler.States, Is.EqualTo(new object[] { 1, 2 }));
        }
    }
}
