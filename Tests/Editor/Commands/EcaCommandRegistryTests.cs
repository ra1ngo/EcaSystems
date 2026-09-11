using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcaSystems.Core;
using NUnit.Framework;
using EcaSystems.Tests.Support;
using static EcaSystems.Tests.Support.ExecutionRules;
using static EcaSystems.Tests.Support.AsyncAssert;

namespace EcaSystems.Tests
{
    [TestFixture]
    public sealed class EcaCommandRegistryTests : AsyncTestFixture
    {
        [Test]
        public async Task Run_BoundCommands_ValidateAndResolveAgainstCurrentRegistry()
        {
            var registry = new EcaCommandRegistry();
            var runner = new EcaCommandRunner(registry);
            var received = new List<IEcaActionContext>();
            var command = new TestCommand<IEcaActionContext, int>("record", (context, value) =>
            {
                received.Add(context);
                return Task.CompletedTask;
            });
            registry.Register(command);
            Assert.Catch<InvalidOperationException>(() => registry.Register(
                new TestCommand<IEcaActionContext, string>("record", (_, __) => Task.CompletedTask)), "Дубликат string Command ID отклонён");
            foreach (var id in new[] { null, "", " \t" })
                Assert.Catch<ArgumentException>(() => registry.Register(
                    new TestCommand<IEcaActionContext, int>(id, (_, __) => Task.CompletedTask)), "Пустой Command ID отклонён");
            Assert.Catch<ArgumentNullException>(() => runner.Bind(null), "Bind null отклонён");
            var contextA = new EcaActionContext<int>(1);
            var contextB = new EcaActionContext<string>("B");
            var boundA = runner.Bind(contextA);
            var boundB = runner.Bind(contextB);
            await boundA.Run("record", 1);
            await boundB.Run("record", 2);
            await boundA.Run("record", 3);
            Assert.That(received.Count == 3 &&
                ReferenceEquals(received[0], contextA) && ReferenceEquals(received[1], contextB) &&
                ReferenceEquals(received[2], contextA), Is.True, "Каждый bound API сохраняет свой current context");
            Assert.That(Assert.Throws<InvalidOperationException>(() => boundA.Run("missing", 1), "Неизвестный ID").Message, Does.Contain("missing"));
            Assert.That(Assert.Throws<ArgumentException>(() => boundA.Run("record", "wrong"), "Неверный тип args").Message, Does.Contain("record"));
            Assert.That(Assert.Throws<ArgumentException>(() => boundA.Run<string>("record", null), "Несовместимый null args").Message, Does.Contain("record"));
            var textValues = new List<string>();
            registry.Register(new TestCommand<IEcaActionContext<string>, string>("text", (_, value) =>
            {
                textValues.Add(value);
                return Task.CompletedTask;
            }));
            Assert.That(Assert.Throws<InvalidOperationException>(() => boundA.Run("text", "value"), "Несовместимый ActionContext").Message, Does.Contain("text"));
            await boundB.Run<string>("text", null);
            Assert.That(textValues, Is.EqualTo(new string[] { null }), "Допустимый null reference args передан");
            var nullableValues = new List<int?>();
            registry.Register(new TestCommand<IEcaActionContext, int?>("nullable", (_, value) =>
            {
                nullableValues.Add(value);
                return Task.CompletedTask;
            }));
            await boundA.Run<int?>("nullable", null);
            await boundA.Run<int?>("nullable", 5);
            Assert.That(nullableValues, Is.EqualTo(new int?[] { null, 5 }), "Nullable args допускают значение и null");
            Assert.That(registry.Unregister("record") && !registry.Unregister("record"), Is.True, "Unregister удаляет Command");
            Assert.That(Assert.Throws<InvalidOperationException>(() => boundA.Run("record", 1), "Старый bound API видит Unregister").Message, Does.Contain("record"));
            registry.Register(command);
            await boundA.Run("record", 4);
            Assert.That(received.Count == 4, Is.True, "ID доступен для повторной регистрации");
            registry.Register(new TestCommand<IEcaActionContext, int>("null-task", (_, __) => null));
            Assert.That(Assert.Throws<InvalidOperationException>(() => boundA.Run("null-task", 1), "Null Task диагностируется").Message, Does.Contain("null-task"));
        }
    }
}
