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
    public sealed class EcaScopeTests : AsyncTestFixture
    {
        [Test]
        public void State_BelongsToScopeLifetimeEvenWhenIdIsReused()
        {
            using var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            var root = engine.CreateScope("root");
            var child = root.CreateScope();
            var oldState = root.State;
            Assert.That(oldState.ScopeId, Is.EqualTo(root.ScopeId));
            Assert.That(child.State.ScopeId, Is.EqualTo(child.ScopeId));
            Assert.That(child.State, Is.Not.SameAs(oldState));
            root.Dispose();
            var replacement = engine.CreateScope("root");
            Assert.That(replacement.State, Is.Not.SameAs(oldState));
            Assert.That(oldState.ScopeId, Is.EqualTo(replacement.State.ScopeId));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" \t")]
        public void State_RejectsMissingIdentity(string scopeId)
        {
            Assert.Throws<ArgumentException>(() => new EcaScopeState(scopeId));
        }

        [Test]
        public void Dispose_ManagesHierarchyIdentityAndClosedOperations()
        {
            var engine = new EcaScopeEngine(new EcaCommandRunner(new EcaCommandRegistry()));
            Assert.That(engine.ScopeCount == 0, Is.True, "Scope engine starts empty");
            var root = engine.CreateScope("scope-1");
            var child = root.CreateScope("child");
            var grandchild = child.CreateScope();
            var sibling = root.CreateScope();
            var other = engine.CreateScope("other");
            Assert.That(root.ScopeId == "scope-1" && root.ParentScopeId == null &&
                child.ParentScopeId == root.ScopeId && grandchild.ParentScopeId == child.ScopeId, Is.True, "Root and child identity");
            Assert.That(grandchild.ScopeId == "scope-2" &&
                sibling.ScopeId == "scope-3", Is.True, "Auto ids skip explicit ids and differ");
            Assert.That(engine.ScopeCount == 5 &&
                engine.TryGetScope(child.ScopeId, out var found) && ReferenceEquals(child, found), Is.True, "Scope count and lookup");
            Assert.Catch<InvalidOperationException>(() => engine.CreateScope("child"), "Duplicate root id rejected");
            Assert.Catch<InvalidOperationException>(() => root.CreateScope("other"), "Duplicate child id rejected");
            Assert.Catch<ArgumentException>(() => engine.CreateScope(""), "Empty scope id rejected");
            Assert.Catch<ArgumentException>(() => root.CreateScope(" \t"), "Whitespace child id rejected");
            Assert.That(engine.ScopeCount == 5, Is.True, "Invalid creation leaves registry intact");

            child.Dispose();
            child.Dispose();
            Assert.That(child.IsDisposed && grandchild.IsDisposed && !root.IsDisposed && !sibling.IsDisposed &&
                engine.ScopeCount == 3 && !engine.TryGetScope("child", out _) &&
                !engine.TryGetScope(grandchild.ScopeId, out _), Is.True, "Child disposal cascades without affecting parent or sibling");
            var replacement = root.CreateScope("child");
            child.Dispose();
            Assert.That(engine.TryGetScope("child", out found) &&
                ReferenceEquals(found, replacement), Is.True, "Stale disposal leaves reused id intact");
            var emptyEvent = new EcaEvent<EcaEventContextEmpty>("scope.empty", "Empty");
            Assert.Catch<ObjectDisposedException>(() => child.Register<TestEventContext>(null, null), "Disposed Register rejected");
            Assert.Catch<ObjectDisposedException>(() => child.Unregister<TestEventContext>(null), "Disposed Unregister rejected");
            Assert.Catch<ObjectDisposedException>(() => child.Fire(emptyEvent), "Disposed empty Fire rejected");
            Assert.Catch<ObjectDisposedException>(() => child.Fire(emptyEvent, EcaEventContextEmpty.Value), "Disposed typed Fire rejected");
            Assert.Catch<ObjectDisposedException>(() => child.CreateScope(), "Disposed child creation rejected");
            var nested = replacement.CreateScope();
            root.Dispose();
            Assert.That(root.IsDisposed && sibling.IsDisposed &&
                replacement.IsDisposed && nested.IsDisposed && engine.ScopeCount == 1 &&
                !engine.TryGetScope(root.ScopeId, out _) && !engine.TryGetScope(sibling.ScopeId, out _) &&
                !engine.TryGetScope(replacement.ScopeId, out _) && !engine.TryGetScope(nested.ScopeId, out _), Is.True, "Parent disposal removes all descendants");
            var otherChild = other.CreateScope();
            var anotherRoot = engine.CreateScope();
            engine.Dispose();
            engine.Dispose();
            Assert.That(other.IsDisposed && otherChild.IsDisposed &&
                anotherRoot.IsDisposed && engine.ScopeCount == 0 && !engine.TryGetScope("other", out _), Is.True, "Engine disposal clears roots and descendants");
            Assert.Catch<ObjectDisposedException>(() => engine.CreateScope(), "Disposed engine creation rejected");
            Assert.Catch<ObjectDisposedException>(() => other.Fire(emptyEvent), "Engine-disposed scope rejects Fire");
        }
    }
}
