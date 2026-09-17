using System;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.EventTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaEventEmitterTests
    {
        [Test]
        public void ConstructionAndBind_RejectNullAndAllowFirstValidBinding()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaEventEmitter(null));
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            var emitter = new EcaEventEmitter(events);
            Assert.Throws<ArgumentNullException>(() => emitter.Bind(null));
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            IEcaEventEmitter externalEmitter = emitter;
            externalEmitter.Fire(ecaEvent, 7);
            Assert.That(handler.States, Is.EqualTo(new object[] { 7 }));
        }

        [Test]
        public void Fire_BeforeBindFailsFast()
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            IEcaEventEmitter emitter = new EcaEventEmitter(events);
            Assert.Throws<InvalidOperationException>(() => emitter.Fire(ecaEvent, 1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Bind_RejectsSecondBindingWithoutReplacingHandler(bool sameInstance)
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            var emitter = new EcaEventEmitter(events);
            var first = new RecordingHandler();
            var second = sameInstance ? first : new RecordingHandler();
            emitter.Bind(first);
            Assert.Throws<InvalidOperationException>(() => emitter.Bind(second));
            emitter.Fire(ecaEvent, 2);
            Assert.That(first.States, Is.EqualTo(new object[] { 2 }));
            if (!sameInstance) Assert.That(second.States, Is.Empty);
        }

        [Test]
        public void Fire_RejectsNullAndUnregisteredEventsBeforeCallbackAndSeesLaterRegistration()
        {
            var events = new EcaBaseEventRegistry();
            var emitter = new EcaEventEmitter(events);
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            var ecaEvent = new BaseTestSupport.Event<int>();
            Assert.Throws<ArgumentNullException>(() => emitter.Fire<int>(null, 1));
            Assert.Throws<InvalidOperationException>(() => emitter.Fire(ecaEvent, 1));
            Assert.That(handler.States, Is.Empty);
            events.Register(ecaEvent);
            emitter.Fire(ecaEvent, 3);
            Assert.That(handler.Events[0], Is.SameAs(ecaEvent));
            Assert.That(handler.States, Is.EqualTo(new object[] { 3 }));
        }

        [Test]
        public void Fire_RejectsLyingMetadataEvenWhenRegistryCheckPasses()
        {
            var events = new EcaBaseEventRegistry();
            events.Register(new BaseTestSupport.Event<int>());
            var liar = new BaseTestSupport.Event<string> { EventStateType = typeof(int) };
            Assert.That(events.CheckRegistered(liar), Is.True);
            var emitter = new EcaEventEmitter(events);
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            Assert.Throws<ArgumentException>(() => emitter.Fire(liar, "wrong"));
            Assert.That(handler.States, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Fire_PreservesDeclaredBaseTypeAndReferenceIncludingNull(bool nullState)
        {
            var events = new EcaBaseEventRegistry();
            IEcaEvent<UserMessage> ecaEvent = new BaseTestSupport.Event<UserMessage>();
            events.Register(ecaEvent);
            var emitter = new EcaEventEmitter(events);
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            UserMessage state = nullState ? null : new DerivedMessage { Text = "derived" };
            IEcaEventEmitter externalEmitter = emitter;
            externalEmitter.Fire<UserMessage>(ecaEvent, state);
            Assert.That(handler.Types, Is.EqualTo(new[] { typeof(UserMessage) }));
            Assert.That(handler.Events[0], Is.SameAs(ecaEvent));
            Assert.That(handler.States[0], Is.SameAs(state));
        }

        [Test]
        public void OneBinding_HandlesThreeUnrelatedTypesAndPreservesValues()
        {
            var events = new EcaBaseEventRegistry();
            var messageEvent = new BaseTestSupport.Event<UserMessage> { Id = "message" };
            var scoreEvent = new BaseTestSupport.Event<UserScore> { Id = "score" };
            var numberEvent = new BaseTestSupport.Event<int> { Id = "number" };
            events.Register(messageEvent);
            events.Register(scoreEvent);
            events.Register(numberEvent);
            var emitter = new EcaEventEmitter(events);
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            IEcaEventEmitter externalEmitter = emitter;
            var message = new UserMessage { Text = "hello" };
            externalEmitter.Fire(messageEvent, message);
            externalEmitter.Fire(scoreEvent, new UserScore(42));
            externalEmitter.Fire(numberEvent, 17);
            Assert.That(handler.Types, Is.EqualTo(new[] { typeof(UserMessage), typeof(UserScore), typeof(int) }));
            Assert.That(handler.Events, Is.EqualTo(new IEcaEvent[] { messageEvent, scoreEvent, numberEvent }));
            Assert.That(handler.States[0], Is.SameAs(message));
            Assert.That(((UserScore)handler.States[1]).Value, Is.EqualTo(42));
            Assert.That(handler.States[2], Is.EqualTo(17));
        }

        [Test]
        public void Fire_PropagatesHandlerFailureUnchangedAndKeepsBinding()
        {
            var events = new EcaBaseEventRegistry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            var emitter = new EcaEventEmitter(events);
            var error = new InvalidOperationException("handler");
            var handler = new RecordingHandler { Callback = () => throw error };
            emitter.Bind(handler);
            Assert.That(Assert.Throws<InvalidOperationException>(() => emitter.Fire(ecaEvent, 1)), Is.SameAs(error));
            handler.Callback = null;
            emitter.Fire(ecaEvent, 2);
            Assert.That(handler.States, Is.EqualTo(new object[] { 1, 2 }));
        }

        private sealed class Registry : IEcaEventRegistry
        {
            public IEcaEvent Registered;
            public IEcaEvent Checked;
            public void Register(IEcaEvent ecaEvent) => Registered = ecaEvent;
            public void Register<E>(IEcaEvent<E> ecaEvent) => Registered = ecaEvent;
            public bool Contains(string eventId)
            {
                if (string.IsNullOrWhiteSpace(eventId)) throw new ArgumentException(nameof(eventId));
                return Registered != null && Registered.Id == eventId;
            }
            public bool Unregister(string eventId)
            {
                if (!Contains(eventId)) return false;
                Registered = null;
                return true;
            }
            public bool CheckRegistered(IEcaEvent ecaEvent) { Checked = ecaEvent; return ReferenceEquals(Registered, ecaEvent); }
        }

        [Test]
        public void Fire_UsesSuppliedRegistryContractForEachCall()
        {
            var events = new Registry();
            var ecaEvent = new BaseTestSupport.Event<int>();
            events.Register(ecaEvent);
            var emitter = new EcaEventEmitter(events);
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            emitter.Fire(ecaEvent, 1);
            Assert.That(events.Checked, Is.SameAs(ecaEvent));
            Assert.That(handler.Events[0], Is.SameAs(events.Checked));
            events.Registered = null;
            Assert.Throws<InvalidOperationException>(() => emitter.Fire(ecaEvent, 2));
            Assert.That(handler.States.Count, Is.EqualTo(1));
        }
    }
}
