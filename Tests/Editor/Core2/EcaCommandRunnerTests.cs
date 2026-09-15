using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core2;
using NUnit.Framework;
using static EcaSystems.Tests.Core2.CommandTestSupport;

namespace EcaSystems.Tests.Core2
{
    [TestFixture]
    public sealed class EcaCommandRunnerTests
    {
        [Test]
        public void ConstructorAndBind_RejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => new EcaCommandRunner(null));
            IEcaCommandRunner runner = new EcaCommandRunner(new EcaCommandRegistry());
            Assert.Throws<ArgumentNullException>(() => runner.Bind(null));
        }

        [Test]
        public async Task Bind_IndependentContextsAndExactReferenceArgsWithOneCommandInstance()
        {
            var registry = new EcaCommandRegistry();
            var seen = new List<Context>();
            var argsSeen = new List<Args>();
            registry.Register(new Command<Context, Args>
            {
                Handler = (context, args) => { seen.Add(context); argsSeen.Add(args); context.Value += args.Value; return Task.CompletedTask; }
            });
            var runner = new EcaCommandRunner(registry);
            var a = new Context();
            var b = new Context();
            var boundA = runner.Bind(a);
            var boundB = runner.Bind(b);
            var args = new Args { Value = 3 };
            await boundA.Run("command", args);
            await boundB.Run("command", args);
            await boundA.Run("command", args);
            Assert.That(seen, Is.EqualTo(new[] { a, b, a }));
            Assert.That(argsSeen, Is.EqualTo(new[] { args, args, args }));
            Assert.That(a.Value, Is.EqualTo(6));
            Assert.That(b.Value, Is.EqualTo(3));
        }

        [Test]
        public async Task Run_AcceptsDerivedContextAndArgs()
        {
            var registry = new EcaCommandRegistry();
            Context seenContext = null;
            Args seenArgs = null;
            registry.Register(new Command<Context, Args>
            {
                Handler = (c, a) => { seenContext = c; seenArgs = a; return Task.CompletedTask; }
            });
            var context = new DerivedContext();
            var args = new DerivedArgs();
            await new EcaCommandRunner(registry).Bind(context).Run("command", args);
            Assert.That(seenContext, Is.SameAs(context));
            Assert.That(seenArgs, Is.SameAs(args));
        }

        [Test]
        public void Run_RejectsWrongContextBeforeCommandInvocation()
        {
            var calls = 0;
            var registry = new EcaCommandRegistry();
            registry.Register(new Command<DerivedContext, int> { Handler = (c, a) => { calls++; return Task.CompletedTask; } });
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            Assert.Throws<InvalidOperationException>(() => commands.Run("command", 1));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void Run_ValidatesDeclaredArgsTypeEvenIfValueOrNullCouldHideMismatch()
        {
            var calls = 0;
            var registry = new EcaCommandRegistry();
            registry.Register(new Command<Context, Args> { Handler = (c, a) => { calls++; return Task.CompletedTask; } });
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            Assert.Throws<ArgumentException>(() => commands.Run("command", "wrong"));
            Assert.Throws<ArgumentException>(() => commands.Run<string>("command", null));
            Assert.Throws<ArgumentException>(() => commands.Run<object>("command", new Args()));
            Assert.Throws<ArgumentException>(() => commands.Run<object>("command", null));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public async Task Run_AllowsTypedNullReferenceArgs()
        {
            var registry = new EcaCommandRegistry();
            var called = false;
            Args seen = new Args();
            registry.Register(new Command<Context, Args> { Handler = (c, a) => { called = true; seen = a; return Task.CompletedTask; } });
            await new EcaCommandRunner(registry).Bind(new Context()).Run<Args>("command", null);
            Assert.That(called, Is.True);
            Assert.That(seen, Is.Null);
        }

        [Test]
        public async Task Run_ValueArgsRequireCompatibleDeclaredType()
        {
            var registry = new EcaCommandRegistry();
            var seen = -1;
            registry.Register(new Command<Context, int> { Handler = (c, a) => { seen = a; return Task.CompletedTask; } });
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            await commands.Run("command", 42);
            Assert.That(seen, Is.EqualTo(42));
            Assert.Throws<ArgumentException>(() => commands.Run<object>("command", 42));
            Assert.Throws<ArgumentException>(() => commands.Run<int?>("command", null));
            Assert.Throws<ArgumentException>(() => commands.Run("command", 42L));
        }

        [TestCase(null)]
        [TestCase(17)]
        public async Task Run_NullableArgsPreserveValue(int? value)
        {
            var registry = new EcaCommandRegistry();
            int? seen = -1;
            registry.Register(new Command<Context, int?> { Handler = (c, a) => { seen = a; return Task.CompletedTask; } });
            await new EcaCommandRunner(registry).Bind(new Context()).Run("command", value);
            Assert.That(seen, Is.EqualTo(value));
        }

        [Test]
        public void Run_ReturnsExactCompletedTask()
        {
            var task = Task.FromResult(12);
            var registry = new EcaCommandRegistry();
            registry.Register(new Command<Context, int> { Handler = (c, a) => task });
            Assert.That(new EcaCommandRunner(registry).Bind(new Context()).Run("command", 1), Is.SameAs(task));
        }

        [Test]
        public async Task Async_BindContextsRemainIndependentAcrossOverlappingCalls()
        {
            var gate = new TaskCompletionSource<bool>();
            var registry = new EcaCommandRegistry();
            registry.Register(new Command<Context, int>
            {
                Handler = async (c, a) => { await gate.Task; c.Value += a; }
            });
            var runner = new EcaCommandRunner(registry);
            var a = new Context();
            var b = new Context();
            var first = runner.Bind(a).Run("command", 3);
            var second = runner.Bind(b).Run("command", 5);
            try
            {
                Assert.That(first.IsCompleted || second.IsCompleted, Is.False);
                Assert.That(a.Value + b.Value, Is.Zero);
                gate.SetResult(true);
                var both = Task.WhenAll(first, second);
                Assert.That(await Task.WhenAny(both, Task.Delay(5000)), Is.SameAs(both));
                await both;
                Assert.That(a.Value, Is.EqualTo(3));
                Assert.That(b.Value, Is.EqualTo(5));
            }
            finally { gate.TrySetResult(true); }
        }

        [Test]
        public async Task Run_PreservesUnfinishedTaskEvenAfterUnregister()
        {
            var source = new TaskCompletionSource<bool>();
            var registry = new EcaCommandRegistry();
            registry.Register(new Command<Context, int> { Handler = (c, a) => source.Task });
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            var task = commands.Run("command", 1);
            try
            {
                Assert.That(task, Is.SameAs(source.Task));
                Assert.That(task.IsCompleted, Is.False);
                Assert.That(registry.Unregister("command"), Is.True);
                Assert.Throws<InvalidOperationException>(() => commands.Run("command", 1));
                source.SetResult(true);
                await task;
            }
            finally { source.TrySetResult(true); }
        }

        [Test]
        public void Run_PropagatesSynchronousExceptionUnchanged()
        {
            var error = new InvalidOperationException("sync");
            var registry = new EcaCommandRegistry();
            registry.Register(new Command<Context, int> { Handler = (c, a) => throw error });
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            Assert.That(Assert.Throws<InvalidOperationException>(() => commands.Run("command", 1)), Is.SameAs(error));
        }

        [Test]
        public async Task Run_ReturnsFaultedTaskAndPreservesException()
        {
            var error = new InvalidOperationException("async");
            var source = new TaskCompletionSource<bool>();
            var registry = new EcaCommandRegistry();
            registry.Register(new Command<Context, int> { Handler = (c, a) => source.Task });
            var task = new EcaCommandRunner(registry).Bind(new Context()).Run("command", 1);
            source.SetException(error);
            Assert.That(task, Is.SameAs(source.Task));
            try { await task; Assert.Fail("Expected command failure."); }
            catch (InvalidOperationException actual) { Assert.That(actual, Is.SameAs(error)); }
            Assert.That(task.IsFaulted, Is.True);
        }

        [Test]
        public void Run_RejectsNullTask()
        {
            var registry = new EcaCommandRegistry();
            registry.Register(new Command<Context, int> { Handler = (c, a) => null });
            var commands = new EcaCommandRunner(registry).Bind(new Context());
            Assert.Throws<InvalidOperationException>(() => commands.Run("command", 1));
        }
    }
}
