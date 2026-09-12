using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EcaSystems.Core1;
using NUnit.Framework;
using static EcaSystems.Tests.Core1.LayerRules;

namespace EcaSystems.Tests.Core1
{
    [TestFixture]
    public sealed class EcaLayerCompatibilityTests
    {
        [TestCase("base", "base")]
        [TestCase("base", "execution")]
        [TestCase("base", "scope")]
        [TestCase("execution", "base")]
        [TestCase("execution", "execution")]
        [TestCase("execution", "scope")]
        [TestCase("scope", "base")]
        [TestCase("scope", "execution")]
        [TestCase("scope", "scope")]
        public void CanonicalRegister_ValidatesCurrentRunnerBeforeStorage(string runtime, string specialization)
        {
            var groups = new EcaRuleExecutionRegistry();
            EcaRuleRunner runner = runtime == "base" ? new EcaRuleRunner() : runtime == "execution"
                ? new EcaExecutionRuleRunner(groups) : new EcaScopeRuleRunner(groups, new EcaScopeState("scope"));
            using var f = new BaseFixture(runner);
            var evt = f.Event<int>("event");
            var calls = 0;
            var baseRule = new EcaRule<int>("base", "Base", evt,
                new TestAction<EcaRuleState<int>>((_, __) => { calls++; return Task.CompletedTask; }));
            var executionRule = Execution("execution", evt, _ => { calls++; return Task.CompletedTask; });
            var scopeRule = Scope("scope", evt, _ => { calls++; return Task.CompletedTask; });
            Action register = specialization == "base" ? () => f.Engine.Register(baseRule) : specialization == "execution"
                ? () => f.Engine.Register(executionRule) : () => f.Engine.Register(scopeRule);
            if (runtime != specialization)
            {
                Assert.Throws<ArgumentException>(() => register());
                Assert.That(f.Rules.GetByEvent(evt), Is.Empty);
                f.Dispatcher.Fire(evt, 1);
                Assert.That(calls, Is.Zero);
            }
            else
            {
                register();
                groups.Register(f.Rules.GetByEvent(evt)[0], new EcaExecutionMode(EcaOverlap.Allow));
                f.Dispatcher.Fire(evt, 1);
                Assert.That(calls, Is.EqualTo(1));
            }
        }

        [Test]
        public void PublicSurface_HidesFiredAndHasExactStandaloneSpecializations()
        {
            Assert.That(typeof(EcaEventDispatcher).GetEvent("Fired"), Is.Null);
            var fired = typeof(EcaEventDispatcher).GetEvent("Fired", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(fired.GetAddMethod(true).IsAssembly, Is.True);
            Assert.That(typeof(IEcaRuleRun).GetMembers(), Is.Empty);
            var executionRegister = typeof(EcaExecutionEngine).GetMethods().Single(m => m.Name == "Register");
            var scopeRegister = typeof(EcaScope).GetMethods().Single(m => m.Name == "Register");
            Assert.That(executionRegister.GetGenericArguments().Length, Is.EqualTo(1));
            Assert.That(executionRegister.GetParameters()[0].ParameterType.GetGenericArguments()[1].GetGenericTypeDefinition(),
                Is.EqualTo(typeof(EcaExecutionRuleState<>)));
            Assert.That(scopeRegister.GetParameters()[0].ParameterType.GetGenericArguments()[1].GetGenericTypeDefinition(),
                Is.EqualTo(typeof(EcaScopeRuleState<>)));
        }

        [Test]
        public void ExecutionWrapper_RejectsForeignOwnerBeforeConditionOrAdmission()
        {
            var groups = new EcaRuleExecutionRegistry();
            var runner = new CaptureRunner(groups);
            using var f = new BaseFixture(runner);
            var evt = f.Event<int>("event");
            var rule = Execution("r", evt, _ => Task.CompletedTask);
            groups.Register(rule, new EcaExecutionMode(EcaOverlap.Allow));
            f.Engine.Register(rule);
            f.Dispatcher.Fire(evt, 0);
            var other = new EcaExecutionRuleRunner(groups);
            var scopeRunner = new EcaScopeRuleRunner(groups, new EcaScopeState("scope"));
            Assert.Throws<ArgumentException>(() => other.Check(runner.Last));
            Assert.Throws<ArgumentException>(() => other.RunAction(runner.Last));
            Assert.Throws<ArgumentException>(() => scopeRunner.RunAction(runner.Last));
            Assert.Throws<ArgumentException>(() => new EcaRuleRunner().Check(runner.Last));
            Assert.That(groups.Get("r").State.TotalStarted, Is.EqualTo(1));
        }

        private sealed class CaptureRunner : EcaExecutionRuleRunner
        {
            internal IEcaRuleRun Last;
            internal CaptureRunner(EcaRuleExecutionRegistry groups) : base(groups) { }
            public override IEcaRuleRun CreateRun(IEcaRule rule, EcaEventOccurrence occurrence)
                => Last = base.CreateRun(rule, occurrence);
        }
    }
}
