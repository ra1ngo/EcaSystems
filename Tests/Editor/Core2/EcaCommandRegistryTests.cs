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
        public async Task Register_NonGenericCommandKeepsOriginalInstanceAndRuns()
        {
            AEcaCommand<Context, int> typed = new Command<Context, int>
            {
                Handler = (c, a) => { c.Value += a; return Task.CompletedTask; }
            };
            AEcaCommand command = typed;
            var registry = new EcaCommandRegistry();
            registry.Register(command);
            Assert.That(registry.Resolve(command.Id), Is.SameAs(command));
            var context = new Context();
            await new EcaCommandRunner(registry).Bind(context).Run(command.Id, 7);
            Assert.That(context.Value, Is.EqualTo(7));
        }

        [Test]
        public void NonGenericCommand_DefaultMetadataAndBridgePreserveDeclaredTypesAndTask()
        {
            Context seenContext = null;
            Args seenArgs = null;
            var task = Task.FromResult(1);
            AEcaCommand command = new Command<Context, Args>
            {
                Handler = (c, a) => { seenContext = c; seenArgs = a; return task; }
            };
            var context = new DerivedContext();
            var args = new DerivedArgs();
            Assert.That(command.ContextType, Is.EqualTo(typeof(Context)));
            Assert.That(command.ArgsType, Is.EqualTo(typeof(Args)));
            Assert.That(command.Run(context, args), Is.SameAs(task));
            Assert.That(seenContext, Is.SameAs(context));
            Assert.That(seenArgs, Is.SameAs(args));
        }

        [Test]
        public async Task Register_HeterogeneousNonGenericArrayRunsDifferentContextsAndArgs()
        {
            AEcaCommand[] commands =
            {
                new Command<Context, int> { CommandId = "number", Handler = (c, a) => { c.Value += a; return Task.CompletedTask; } },
                new Command<CommandsContext, Args> { CommandId = "args", Handler = (c, a) => { c.Total += a.Value; return Task.CompletedTask; } }
            };
            var registry = new EcaCommandRegistry();
            foreach (AEcaCommand command in commands) registry.Register(command);
            Assert.That(commands[0].ContextType, Is.EqualTo(typeof(Context)));
            Assert.That(commands[0].ArgsType, Is.EqualTo(typeof(int)));
            Assert.That(commands[1].ContextType, Is.EqualTo(typeof(CommandsContext)));
            Assert.That(commands[1].ArgsType, Is.EqualTo(typeof(Args)));
            var runner = new EcaCommandRunner(registry);
            var first = new Context();
            var second = new CommandsContext(runner);
            var boundFirst = runner.Bind(first);
            await boundFirst.Run("number", 5);
            await second.Commands.Run("args", new Args { Value = 9 });
            Assert.That(first.Value, Is.EqualTo(5));
            Assert.That(second.Total, Is.EqualTo(9));
            Assert.Throws<InvalidOperationException>(() => boundFirst.Run("args", new Args()));
            Assert.Throws<ArgumentException>(() => second.Commands.Run("args", 9));
            Assert.That(first.Value, Is.EqualTo(5));
            Assert.That(second.Total, Is.EqualTo(9));
        }

        [Test]
        public async Task Register_MultipleCommandsResolveByOrdinalId()
        {
            var registry = new EcaCommandRegistry();
            var calls = new List<string>();
            registry.Register(new Command<Context, int> { CommandId = "one", Handler = (c, a) => { calls.Add("one"); return Task.CompletedTask; } });
            registry.Register(new Command<Context, int> { CommandId = "One", Handler = (c, a) => { calls.Add("One"); return Task.CompletedTask; } });
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
            Assert.Throws<ArgumentException>(() => registry.Register(new Command<Context, int> { CommandId = id }));
            Assert.Throws<ArgumentException>(() => registry.Unregister(id));
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            Assert.Throws<ArgumentException>(() => commands.Run(id, 1));
        }

        [Test]
        public void Register_RejectsNullCommand()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaCommandRegistry().Register(null));
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
