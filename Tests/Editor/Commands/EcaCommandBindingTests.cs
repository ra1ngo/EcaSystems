using System;
using System.Reflection;
using EcaSystems.Core;
using NUnit.Framework;

namespace EcaSystems.Tests
{
    [TestFixture]
    public sealed class EcaCommandBindingTests
    {
        [Test]
        public void BindCommands_RejectsNullAndRebindingWithoutReplacingCommands()
        {
            var state = new EcaRuleExecutionGroupState();
            var context = (EcaExecutionActionContext<int>)Activator.CreateInstance(typeof(EcaExecutionActionContext<int>),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 42, state }, null);
            Assert.That(context.EventContext, Is.EqualTo(42));
            Assert.That(context.RuleExecutionGroupState, Is.SameAs(state));
            Assert.Throws<InvalidOperationException>(() => { _ = context.Commands; });

            // Migrated from the old same-assembly .NET harness. No friend assembly
            // or public setter is added just to expose the internal binding guard.
            var bind = typeof(EcaExecutionActionContext<int>).GetMethod("BindCommands",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bind, Is.Not.Null);
            var nullError = Assert.Throws<TargetInvocationException>(() => bind.Invoke(context, new object[] { null }));
            Assert.That(nullError.InnerException, Is.TypeOf<ArgumentNullException>());
            var runner = new EcaCommandRunner(new EcaCommandRegistry());
            var commands = runner.Bind(context);
            bind.Invoke(context, new object[] { commands });
            Assert.That(context.Commands, Is.SameAs(commands));
            var repeated = Assert.Throws<TargetInvocationException>(() => bind.Invoke(context, new object[] { runner.Bind(context) }));
            Assert.That(repeated.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(context.Commands, Is.SameAs(commands));
        }
    }
}
