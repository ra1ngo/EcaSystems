using System;
using EcaSystems.Core2;
using NUnit.Framework;

namespace EcaSystems.Tests.Core2
{
    public sealed class EcaSimpleRegistryTests
    {
        private static IEcaEvent Event(string id) => new BaseTestSupport.Event<int> { Id = id };

        [Test]
        public void EventRegistry_LiveReadOnlyViewAndCanonicalIdentity()
        {
            var registry = new EcaBaseEventRegistry();
            var view = registry.Events;
            var item = Event("item");
            Assert.That(view, Is.Empty);
            Assert.That(registry.CheckRegistered(null), Is.False);
            Assert.That(registry.CheckRegistered(item), Is.False);
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<InvalidOperationException>(() => registry.Resolve("item"));
            Assert.That(registry.Contains("item"), Is.False);
            registry.Register(item);
            Assert.That(registry.Events, Is.SameAs(view));
            Assert.That(view, Is.EqualTo(new[] { item }));
            Assert.That(registry.Resolve("item"), Is.SameAs(item));
            Assert.That(registry.Contains("item"), Is.True);
            Assert.That(registry.CheckRegistered(item), Is.True);
            Assert.That(registry.CheckRegistered(Event("item")), Is.False);
            Assert.Throws<InvalidOperationException>(() => registry.Register(item));
            Assert.Throws<InvalidOperationException>(() => registry.Register(Event("item")));
            registry.Register(Event("Item"));
            Assert.That(view.Count, Is.EqualTo(2));
            Assert.That(registry.Unregister("item"), Is.True);
            Assert.That(registry.Unregister("item"), Is.False);
            Assert.That(registry.CheckRegistered(item), Is.False);
            Assert.That(registry.Contains("item"), Is.False);
            Assert.That(view.Count, Is.EqualTo(1));
            var replacement = Event("item");
            registry.Register(replacement);
            Assert.That(registry.Resolve("item"), Is.SameAs(replacement));
            Assert.That(registry.CheckRegistered(item), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void EventRegistry_ValidatesIds(string id)
        {
            var registry = new EcaBaseEventRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register(Event(id)));
            Assert.Throws<ArgumentException>(() => registry.Contains(id));
            Assert.Throws<ArgumentException>(() => registry.Resolve(id));
            Assert.Throws<ArgumentException>(() => registry.Unregister(id));
            Assert.That(registry.CheckRegistered(Event(id)), Is.False);
        }

        private static AEcaCommand Command(string id) => new CommandTestSupport.Command<CommandTestSupport.Context, int> { CommandId = id };

        [Test]
        public void CommandRegistry_LiveReadOnlyViewAndCanonicalIdentity()
        {
            var registry = new EcaCommandRegistry();
            var view = registry.Commands;
            var item = Command("item");
            Assert.That(view, Is.Empty);
            Assert.That(registry.CheckRegistered(null), Is.False);
            Assert.That(registry.CheckRegistered(item), Is.False);
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<InvalidOperationException>(() => registry.Resolve("item"));
            Assert.That(registry.Contains("item"), Is.False);
            registry.Register(item);
            Assert.That(registry.Commands, Is.SameAs(view));
            Assert.That(view, Is.EqualTo(new[] { item }));
            Assert.That(registry.Resolve("item"), Is.SameAs(item));
            Assert.That(registry.Contains("item"), Is.True);
            Assert.That(registry.CheckRegistered(item), Is.True);
            Assert.That(registry.CheckRegistered(Command("item")), Is.False);
            Assert.Throws<InvalidOperationException>(() => registry.Register(item));
            Assert.Throws<InvalidOperationException>(() => registry.Register(Command("item")));
            registry.Register(Command("Item"));
            Assert.That(view.Count, Is.EqualTo(2));
            Assert.That(registry.Unregister("item"), Is.True);
            Assert.That(registry.Unregister("item"), Is.False);
            Assert.That(registry.CheckRegistered(item), Is.False);
            Assert.That(registry.Contains("item"), Is.False);
            Assert.That(view.Count, Is.EqualTo(1));
            var replacement = Command("item");
            registry.Register(replacement);
            Assert.That(registry.Resolve("item"), Is.SameAs(replacement));
            Assert.That(registry.CheckRegistered(item), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void CommandRegistry_ValidatesIds(string id)
        {
            var registry = new EcaCommandRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register(Command(id)));
            Assert.Throws<ArgumentException>(() => registry.Contains(id));
            Assert.Throws<ArgumentException>(() => registry.Resolve(id));
            Assert.Throws<ArgumentException>(() => registry.Unregister(id));
            Assert.That(registry.CheckRegistered(Command(id)), Is.False);
        }

        private static EcaSystem System(string id) => new EcaSystem(id, new EcaSystemNamespace("ns"), new EcaBaseEventRegistry(), new EcaCommandRegistry());

        [Test]
        public void SystemRegistry_LiveReadOnlyViewAndCanonicalIdentity()
        {
            var registry = new EcaSystemRegistry();
            var view = registry.Systems;
            var item = System("item");
            Assert.That(view, Is.Empty);
            Assert.That(registry.CheckRegistered(null), Is.False);
            Assert.That(registry.CheckRegistered(item), Is.False);
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<InvalidOperationException>(() => registry.Resolve("item"));
            Assert.That(registry.Contains("item"), Is.False);
            registry.Register(item);
            Assert.That(registry.Systems, Is.SameAs(view));
            Assert.That(view, Is.EqualTo(new[] { item }));
            Assert.That(registry.Resolve("item"), Is.SameAs(item));
            Assert.That(registry.Contains("item"), Is.True);
            Assert.That(registry.CheckRegistered(item), Is.True);
            Assert.That(registry.CheckRegistered(System("item")), Is.False);
            Assert.Throws<InvalidOperationException>(() => registry.Register(item));
            Assert.Throws<InvalidOperationException>(() => registry.Register(System("item")));
            registry.Register(System("Item"));
            Assert.That(view.Count, Is.EqualTo(2));
            Assert.That(registry.Unregister("item"), Is.True);
            Assert.That(registry.Unregister("item"), Is.False);
            Assert.That(registry.CheckRegistered(item), Is.False);
            Assert.That(registry.Contains("item"), Is.False);
            Assert.That(view.Count, Is.EqualTo(1));
            var replacement = System("item");
            registry.Register(replacement);
            Assert.That(registry.Resolve("item"), Is.SameAs(replacement));
            Assert.That(registry.CheckRegistered(item), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void SystemRegistry_ValidatesIds(string id)
        {
            var registry = new EcaSystemRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register(System(id)));
            Assert.Throws<ArgumentException>(() => registry.Contains(id));
            Assert.Throws<ArgumentException>(() => registry.Resolve(id));
            Assert.Throws<ArgumentException>(() => registry.Unregister(id));
            Assert.That(registry.CheckRegistered(System(id)), Is.False);
        }

        private static EcaSystemNamespace Namespace(string id) => new EcaSystemNamespace(id);

        [Test]
        public void NamespaceRegistry_LiveReadOnlyViewAndCanonicalIdentity()
        {
            var registry = new EcaSystemNamespaceRegistry();
            var view = registry.Namespaces;
            var item = Namespace("item");
            Assert.That(view, Is.Empty);
            Assert.That(registry.CheckRegistered(null), Is.False);
            Assert.That(registry.CheckRegistered(item), Is.False);
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<InvalidOperationException>(() => registry.Resolve("item"));
            Assert.That(registry.Contains("item"), Is.False);
            registry.Register(item);
            Assert.That(registry.Namespaces, Is.SameAs(view));
            Assert.That(view, Is.EqualTo(new[] { item }));
            Assert.That(registry.Resolve("item"), Is.SameAs(item));
            Assert.That(registry.Contains("item"), Is.True);
            Assert.That(registry.CheckRegistered(item), Is.True);
            Assert.That(registry.CheckRegistered(Namespace("item")), Is.False);
            Assert.Throws<InvalidOperationException>(() => registry.Register(item));
            Assert.Throws<InvalidOperationException>(() => registry.Register(Namespace("item")));
            registry.Register(Namespace("Item"));
            Assert.That(view.Count, Is.EqualTo(2));
            Assert.That(registry.Unregister("item"), Is.True);
            Assert.That(registry.Unregister("item"), Is.False);
            Assert.That(registry.CheckRegistered(item), Is.False);
            Assert.That(registry.Contains("item"), Is.False);
            Assert.That(view.Count, Is.EqualTo(1));
            var replacement = Namespace("item");
            registry.Register(replacement);
            Assert.That(registry.Resolve("item"), Is.SameAs(replacement));
            Assert.That(registry.CheckRegistered(item), Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void NamespaceRegistry_ValidatesIds(string id)
        {
            var registry = new EcaSystemNamespaceRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register(Namespace(id)));
            Assert.Throws<ArgumentException>(() => registry.Contains(id));
            Assert.Throws<ArgumentException>(() => registry.Resolve(id));
            Assert.Throws<ArgumentException>(() => registry.Unregister(id));
            Assert.That(registry.CheckRegistered(Namespace(id)), Is.False);
        }

    }
}
