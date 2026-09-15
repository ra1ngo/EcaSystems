using System;
using System.Collections.Generic;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.EventTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaEventDispatcherTests
    {
        [Test]
        public void Fire_SequentialUnknownTypesKeepEventAndStateInFreshOccurrences()
        {
            var events = new EcaBaseEventRegistry();
            var dispatcher = new EcaEventDispatcher(events);
            var messageEvent = new BaseTestSupport.Event<UserMessage> { Id = "message" };
            var scoreEvent = new BaseTestSupport.Event<UserScore> { Id = "score" };
            events.Register(messageEvent);
            events.Register(scoreEvent);
            var occurrences = new List<EcaEventOccurrence>();
            IEcaEventOccurrenceSource source = dispatcher;
            source.Fired += occurrences.Add;
            var message = new UserMessage { Text = "hello" };
            dispatcher.Fire(messageEvent, message);
            dispatcher.Fire(scoreEvent, new UserScore(42));
            dispatcher.Fire(messageEvent, message);
            Assert.That(occurrences.Count, Is.EqualTo(3));
            Assert.That(occurrences[0], Is.Not.SameAs(occurrences[2]));
            Assert.That(occurrences[0].Event, Is.SameAs(messageEvent));
            Assert.That(occurrences[1].Event, Is.SameAs(scoreEvent));
            var handler = new RecordingHandler();
            var replay = new Source();
            using var receiver = new EcaEventReceiver(events, replay);
            receiver.Subscribe(handler);
            foreach (var occurrence in occurrences) replay.Emit(occurrence);
            Assert.That(handler.Types, Is.EqualTo(new[] { typeof(UserMessage), typeof(UserScore), typeof(UserMessage) }));
            Assert.That(handler.Events, Is.EqualTo(new IEcaEvent[] { messageEvent, scoreEvent, messageEvent }));
            Assert.That(handler.States[0], Is.SameAs(message));
            Assert.That(((UserScore)handler.States[1]).Value, Is.EqualTo(42));
            Assert.That(handler.States[2], Is.SameAs(message));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Fire_ThroughReceiverPreservesDeclaredBaseTypeForDerivedOrNullState(bool nullState)
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<UserMessage>();
            events.Register(ecaEvent);
            var dispatcher = new EcaEventDispatcher(events);
            using var receiver = new EcaEventReceiver(events, dispatcher);
            var handler = new RecordingHandler();
            receiver.Subscribe(handler);
            UserMessage state = nullState ? null : new DerivedMessage { Text = "derived" };
            dispatcher.Fire(ecaEvent, state);
            Assert.That(handler.Types, Is.EqualTo(new[] { typeof(UserMessage) }));
            Assert.That(handler.Events[0], Is.SameAs(ecaEvent));
            Assert.That(handler.States[0], Is.SameAs(state));
        }

        [Test]
        public void Fire_ValidatesEvenWithoutListenersAndSeesLaterRegistration()
        {
            var events = new EcaBaseEventRegistry();
            var dispatcher = new EcaEventDispatcher(events);
            var ecaEvent = new BaseTestSupport.Event<int>();
            Assert.Throws<InvalidOperationException>(() => dispatcher.Fire(ecaEvent, 7));
            events.Register(ecaEvent);
            Assert.DoesNotThrow(() => dispatcher.Fire(ecaEvent, 7));
        }

        [Test]
        public void Fire_UsesRegistryIdAndTypeSemanticsForEquivalentDeclaration()
        {
            var events = new EcaBaseEventRegistry();
            events.Register(new BaseTestSupport.Event<int>());
            var equivalent = new BaseTestSupport.Event<int>();
            var dispatcher = new EcaEventDispatcher(events);
            EcaEventOccurrence seen = null;
            ((IEcaEventOccurrenceSource)dispatcher).Fired += occurrence => seen = occurrence;
            dispatcher.Fire(equivalent, 12);
            Assert.That(seen.Event, Is.SameAs(equivalent));
        }

        [Test]
        public void Fire_RejectsLyingMetadataEvenWhenRegistryCheckPasses()
        {
            var events = new EcaBaseEventRegistry();
            events.Register(new BaseTestSupport.Event<int>());
            var liar = new BaseTestSupport.Event<string> { EventStateType = typeof(int) };
            Assert.That(events.CheckRegistered(liar), Is.True);
            var dispatcher = new EcaEventDispatcher(events);
            var calls = 0;
            ((IEcaEventOccurrenceSource)dispatcher).Fired += occurrence => calls++;
            Assert.Throws<ArgumentException>(() => dispatcher.Fire(liar, "wrong"));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void ConstructorFireAndFactory_RejectNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaEventDispatcher(null));
            var events = new EcaBaseEventRegistry();
            var dispatcher = new EcaEventDispatcher(events);
            Assert.Throws<ArgumentNullException>(() => dispatcher.Fire<int>(null, 0));
            Assert.Throws<ArgumentNullException>(() => EcaEventOccurrence.Create<int>(null, 0));
        }

        [Test]
        public void SourceEvent_RemoveStopsDelivery()
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            var dispatcher = new EcaEventDispatcher(events);
            IEcaEventOccurrenceSource source = dispatcher;
            var calls = 0;
            Action<EcaEventOccurrence> listener = occurrence => calls++;
            source.Fired += listener;
            dispatcher.Fire(ecaEvent, 1);
            source.Fired -= listener;
            dispatcher.Fire(ecaEvent, 2);
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
