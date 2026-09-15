using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.CommandTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaCommandRegistryTests
    {
        [Test]
        public async Task Register_MultipleCommandsResolveByOrdinalId()
        {
            var registry = new EcaCommandRegistry();
            var calls = new List<string>();
            registry.Register(new Command<Context, int> { Id = "one", Handler = (c, a) => { calls.Add("one"); return Task.CompletedTask; } });
            registry.Register(new Command<Context, int> { Id = "One", Handler = (c, a) => { calls.Add("One"); return Task.CompletedTask; } });
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            await commands.Run("One", 1);
            await commands.Run("one", 1);
            Assert.That(calls, Is.EqualTo(new[] { "One", "one" }));
            Assert.Throws<InvalidOperationException>(() => commands.Run("missing", 1));
        }

        [Test]
        public void Register_DuplicateIdRejectedRegardlessOfInstanceOrSpecialization()
        {
            var registry = new EcaCommandRegistry();
            var command = new Command<Context, int>();
            registry.Register(command);
            Assert.Throws<InvalidOperationException>(() => registry.Register(command));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new Command<Context, int>()));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new Command<DerivedContext, string>()));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void RegistryAndLookup_RejectInvalidIds(string id)
        {
            var registry = new EcaCommandRegistry();
            Assert.Throws<ArgumentException>(() => registry.Register(new Command<Context, int> { Id = id }));
            Assert.Throws<ArgumentException>(() => registry.Unregister(id));
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            Assert.Throws<ArgumentException>(() => commands.Run(id, 1));
        }

        [Test]
        public void Register_RejectsNullCommand()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaCommandRegistry().Register<Context, int>(null));
        }

        [Test]
        public async Task BoundCommands_ResolveCurrentRegistrationOnEveryRun()
        {
            var registry = new EcaCommandRegistry();
            var context = new Context();
            var commands = new EcaCommandRunner(registry).Bind(context);
            Assert.That(registry.Unregister("command"), Is.False);
            Assert.Throws<InvalidOperationException>(() => commands.Run("command", 1));
            registry.Register(new Command<Context, int> { Handler = (c, a) => { c.Value += a; return Task.CompletedTask; } });
            await commands.Run("command", 2);
            Assert.That(context.Value, Is.EqualTo(2));
            Assert.That(registry.Unregister("command"), Is.True);
            Assert.That(registry.Unregister("command"), Is.False);
            Assert.Throws<InvalidOperationException>(() => commands.Run("command", 2));
            registry.Register(new Command<Context, int> { Handler = (c, a) => { c.Value += a * 10; return Task.CompletedTask; } });
            await commands.Run("command", 3);
            Assert.That(context.Value, Is.EqualTo(32));
        }
    }
}
