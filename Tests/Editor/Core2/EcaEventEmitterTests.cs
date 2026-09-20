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
        public void Bind_RejectsNullAndAllowsFirstValidBinding()
        {
            var ecaEvent = new BaseTestSupport.Event<int>();
            var emitter = new EcaEventEmitter();
            Assert.Throws<ArgumentNullException>(() => emitter.Bind(null));
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            IEcaEventEmitter externalEmitter = emitter;
            externalEmitter.Fire(ecaEvent, 7);
            Assert.That(handler.States, Is.EqualTo(new object[] { 7 }));
        }

        [Test]
        public void Fire_ForwardsExactEventStateAndBothContextReferences()
        {
            var first = new BaseTestSupport.Event<UserMessage>();
            var second = new BaseTestSupport.Event<UserMessage> { Id = first.Id };
            var state = new DerivedMessage();
            var condition = new BaseTestSupport.ConditionContext();
            var action = new BaseTestSupport.ActionContext();
            var emitter = new EcaEventEmitter();
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            emitter.Fire<UserMessage>(first, state, condition, action);
            emitter.Fire<UserMessage>(second, state, condition, action);
            Assert.That(handler.Events[0], Is.SameAs(first));
            Assert.That(handler.Events[1], Is.SameAs(second));
            for (var i = 0; i < 2; i++)
            {
                Assert.That(handler.States[i], Is.SameAs(state));
                Assert.That(handler.Types[i], Is.EqualTo(typeof(UserMessage)));
                Assert.That(handler.ConditionContexts[i], Is.SameAs(condition));
                Assert.That(handler.ActionContexts[i], Is.SameAs(action));
            }
        }

        [Test]
        public void Fire_BeforeBindFailsFast()
        {
            var ecaEvent = new BaseTestSupport.Event<int>();
            IEcaEventEmitter emitter = new EcaEventEmitter();
            Assert.Throws<InvalidOperationException>(() => emitter.Fire(ecaEvent, 1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Bind_RejectsSecondBindingWithoutReplacingHandler(bool sameInstance)
        {
            var ecaEvent = new BaseTestSupport.Event<int>();
            var emitter = new EcaEventEmitter();
            var first = new RecordingHandler();
            var second = sameInstance ? first : new RecordingHandler();
            emitter.Bind(first);
            Assert.Throws<InvalidOperationException>(() => emitter.Bind(second));
            emitter.Fire(ecaEvent, 2);
            Assert.That(first.States, Is.EqualTo(new object[] { 2 }));
            if (!sameInstance) Assert.That(second.States, Is.Empty);
        }

        [Test]
        public void Fire_ForwardsNullEventWithoutSemanticValidation()
        {
            var emitter = new EcaEventEmitter();
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            emitter.Fire<int>(null, 3);
            Assert.That(handler.Events, Is.EqualTo(new IEcaEvent[] { null }));
            Assert.That(handler.States, Is.EqualTo(new object[] { 3 }));
            Assert.That(handler.Types, Is.EqualTo(new[] { typeof(int) }));
        }

        [Test]
        public void Fire_DoesNotInspectEventMetadata()
        {
            var declaration = new BaseTestSupport.Event<string> { EventStateType = typeof(int) };
            var emitter = new EcaEventEmitter();
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            emitter.Fire(declaration, "payload");
            Assert.That(handler.Events[0], Is.SameAs(declaration));
            Assert.That(handler.Types, Is.EqualTo(new[] { typeof(string) }));
            Assert.That(handler.States, Is.EqualTo(new object[] { "payload" }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Fire_PreservesDeclaredBaseTypeAndReferenceIncludingNull(bool nullState)
        {
            IEcaEvent<UserMessage> ecaEvent = new BaseTestSupport.Event<UserMessage>();
            var emitter = new EcaEventEmitter();
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
            var messageEvent = new BaseTestSupport.Event<UserMessage> { Id = "message" };
            var scoreEvent = new BaseTestSupport.Event<UserScore> { Id = "score" };
            var numberEvent = new BaseTestSupport.Event<int> { Id = "number" };
            var emitter = new EcaEventEmitter();
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
            var ecaEvent = new BaseTestSupport.Event<int>();
            var emitter = new EcaEventEmitter();
            var error = new InvalidOperationException("handler");
            var handler = new RecordingHandler { Callback = () => throw error };
            emitter.Bind(handler);
            Assert.That(Assert.Throws<InvalidOperationException>(() => emitter.Fire(ecaEvent, 1)), Is.SameAs(error));
            handler.Callback = null;
            emitter.Fire(ecaEvent, 2);
            Assert.That(handler.States, Is.EqualTo(new object[] { 1, 2 }));
        }

        [Test]
        public void Fire_ForwardsOmittedAndExplicitNullContexts()
        {
            var emitter = new EcaEventEmitter();
            var handler = new RecordingHandler();
            emitter.Bind(handler);
            var ecaEvent = new BaseTestSupport.Event<int>();
            emitter.Fire(ecaEvent, 1);
            emitter.Fire(ecaEvent, 2, null, null);
            Assert.That(handler.States, Is.EqualTo(new object[] { 1, 2 }));
            Assert.That(handler.ConditionContexts, Is.EqualTo(new IEcaConditionContext[] { null, null }));
            Assert.That(handler.ActionContexts, Is.EqualTo(new IEcaActionContext[] { null, null }));
        }
    }
}
