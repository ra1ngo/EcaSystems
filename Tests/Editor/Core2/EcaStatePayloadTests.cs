using System;
using EcaSystems.Core2;
using NUnit.Framework;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaStatePayloadTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void PayloadForwardsExactReferencesAndDoesNotCache(bool nullPayload)
        {
            var registry = new EcaStateRegistry();
            IEcaStateResolver resolver = new EcaStateResolver(registry);
            var state = new BaseTestSupport.State();
            object payload = nullPayload ? null : new object();
            var calls = 0;
            registry.Register<object>("state", (actualState, actualPayload) =>
            {
                Assert.That(actualState, Is.SameAs(state));
                Assert.That(actualPayload, Is.SameAs(payload));
                calls++;
                return new object();
            });
            var first = resolver.Resolve<object>("state", state, payload);
            Assert.That(resolver.Resolve<object>("state", state, payload), Is.Not.SameAs(first));
            Assert.That(calls, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WrongOverloadRejectsShapeBeforeRuleStateOrCallback(bool payloadRegistration)
        {
            var registry = new EcaStateRegistry();
            var calls = 0;
            if (payloadRegistration) registry.Register<int>("state", (state, payload) => ++calls);
            else registry.Register<int>("state", state => ++calls);
            var resolver = new EcaStateResolver(registry);
            if (payloadRegistration)
                Assert.That(Assert.Throws<InvalidOperationException>(() => resolver.Resolve<int>("state", null)).Message, Does.Contain("shape"));
            else
                Assert.That(Assert.Throws<InvalidOperationException>(() => resolver.Resolve<int>("state", null, null)).Message, Does.Contain("shape"));
            Assert.That(calls, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IdCannotBeReusedAcrossTypesOrShapes(bool payloadFirst)
        {
            var registry = new EcaStateRegistry();
            if (payloadFirst) registry.Register<int>("id", (state, payload) => 1);
            else registry.Register<int>("id", state => 1);
            Assert.Throws<InvalidOperationException>(() => registry.Register<int>("id", state => 2));
            Assert.Throws<InvalidOperationException>(() => registry.Register<int>("id", (state, payload) => 2));
            Assert.Throws<InvalidOperationException>(() => registry.Register<string>("id", (state, payload) => "other"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void PayloadValidatesIdFirst(string id)
        {
            var registry = new EcaStateRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register<int>(id, (Func<IEcaRuleState, object, int>)null));
            Assert.Throws<ArgumentException>(() => new EcaStateResolver(registry).Resolve<int>(id, null, null));
        }

        [Test]
        public void PayloadValidationOrderIsLookupTypeShapeThenRuleState()
        {
            var registry = new EcaStateRegistry();
            var resolver = new EcaStateResolver(registry);
            Assert.Throws<ArgumentNullException>(() => registry.Register<int>("id", (Func<IEcaRuleState, object, int>)null));
            Assert.That(Assert.Throws<InvalidOperationException>(() => resolver.Resolve<int>("missing", null, null)).Message, Does.Contain("not registered"));
            registry.Register<IDisposable>("typed", (state, payload) => throw new Exception("Must not call"));
            Assert.That(Assert.Throws<InvalidOperationException>(() => resolver.Resolve<object>("typed", null)).Message, Does.Contain("declares"));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<object>("typed", new BaseTestSupport.State(), null));
            Assert.Throws<InvalidOperationException>(() => resolver.Resolve<System.IO.MemoryStream>("typed", new BaseTestSupport.State(), null));
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve<IDisposable>("typed", null, null));
        }

        [Test]
        public void PayloadAllowsNullResultAndPropagatesOriginalExternalException()
        {
            var registry = new EcaStateRegistry();
            registry.Register<string>("null", (state, payload) => null);
            var error = new InvalidOperationException("external");
            registry.Register<string>("error", (state, payload) => throw error);
            var resolver = new EcaStateResolver(registry);
            var state = new BaseTestSupport.State();
            Assert.That(resolver.Resolve<string>("null", state, null), Is.Null);
            Assert.That(Assert.Throws<InvalidOperationException>(() => resolver.Resolve<string>("error", state, null)), Is.SameAs(error));
        }
    }
}
