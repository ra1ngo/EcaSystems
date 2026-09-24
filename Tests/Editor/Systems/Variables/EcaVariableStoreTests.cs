using System;
using System.Collections.Generic;
using EcaSystems.Variables;
using NUnit.Framework;

namespace EcaSystems.Tests.Variables
{
    public sealed class EcaVariableStoreTests
    {
        [Test] public void IntDeclarationAndMutations() => VerifyValues(0, 10, 50, 60);
        [Test] public void FloatDeclarationAndMutations() => VerifyValues(0f, 1.25f, 2.5f, 3.75f);
        [Test] public void BoolDeclarationAndMutations() => VerifyValues(false, true, false, true);
        [Test] public void StringDeclarationAndMutations() => VerifyValues("default", "first", "direct", "last");
        [Test] public void NullStringDeclarationAndMutations() => VerifyValues<string>(null, "first", "direct", null);

        private static void VerifyValues<T>(T initial, T changed, T direct, T last)
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var events = new List<EcaVariableChanged>();
            store.VariableChanged += events.Add;
            var variable = store.Declare("id", initial);
            var definition = variable.Definition;
            Assert.That(definition.Id, Is.EqualTo("id"));
            Assert.That(definition.ValueType, Is.EqualTo(typeof(T)));
            Assert.That(definition.DefaultValue, Is.EqualTo(initial));
            Assert.That(variable.CurrentValue, Is.EqualTo(initial));
            Assert.That(variable.OldValue, Is.EqualTo(initial));
            Assert.That(store.GetValue<T>("id"), Is.EqualTo(initial));
            Assert.That(store.TryGetValue<T>("id", out var found), Is.True);
            Assert.That(found, Is.EqualTo(initial));
            Assert.That(events, Is.Empty);
            store.SetValue("id", initial);
            Assert.That(events, Is.Empty);
            store.SetValue("id", changed);
            Assert.That(variable.OldValue, Is.EqualTo(initial));
            Assert.That(variable.CurrentValue, Is.EqualTo(changed));
            Assert.That(events, Has.Count.EqualTo(1));
            var snapshot = events[0];
            Assert.That((object)snapshot, Is.Not.SameAs(variable));
            Assert.That(snapshot.Definition, Is.SameAs(definition));
            Assert.That(snapshot.OldValue, Is.EqualTo(initial));
            Assert.That(snapshot.CurrentValue, Is.EqualTo(changed));
            store.SetValue("id", changed);
            Assert.That(variable.OldValue, Is.EqualTo(initial));
            Assert.That(events, Has.Count.EqualTo(1));
            store.ForceSetValue("id", changed);
            Assert.That(variable.OldValue, Is.EqualTo(changed));
            Assert.That(variable.CurrentValue, Is.EqualTo(changed));
            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(events[1], Is.Not.SameAs(snapshot));
            Assert.That(events[1].OldValue, Is.EqualTo(changed));
            store.ForceSetValue("id", initial);
            Assert.That(variable.OldValue, Is.EqualTo(changed));
            Assert.That(variable.CurrentValue, Is.EqualTo(initial));
            Assert.That(events, Has.Count.EqualTo(3));
            Assert.That(events[2].OldValue, Is.EqualTo(changed));
            Assert.That(events[2].CurrentValue, Is.EqualTo(initial));
            store.SetCurrentValue("id", direct);
            Assert.That(variable.CurrentValue, Is.EqualTo(direct));
            Assert.That(variable.OldValue, Is.EqualTo(changed));
            Assert.That(events, Has.Count.EqualTo(3));
            store.SetValue("id", last);
            Assert.That(variable.CurrentValue, Is.EqualTo(last));
            Assert.That(variable.OldValue, Is.EqualTo(direct));
            Assert.That(store.GetValue<T>("id"), Is.EqualTo(last));
            Assert.That(events, Has.Count.EqualTo(4));
            Assert.That(events[3].OldValue, Is.EqualTo(direct));
            Assert.That(events[3].CurrentValue, Is.EqualTo(last));
            Assert.That(snapshot.OldValue, Is.EqualTo(initial));
            Assert.That(snapshot.CurrentValue, Is.EqualTo(changed));
            Assert.That(definition.DefaultValue, Is.EqualTo(initial));
            Assert.That(variable.Definition, Is.SameAs(definition));
        }

        [Test]
        public void IdsAreOrdinalAndDuplicateDeclarationsNeverChangeExistingVariable()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var variable = store.Declare("Money", 10);
            Assert.That(store.Contains("Money"), Is.True);
            Assert.That(store.Contains("money"), Is.False);
            store.Declare("money", "separate");
            Assert.Throws<InvalidOperationException>(() => store.Declare("Money", 10));
            Assert.Throws<InvalidOperationException>(() => store.Declare("Money", "wrong"));
            Assert.That(variable.CurrentValue, Is.EqualTo(10));
            Assert.That(store.GetValue<string>("money"), Is.EqualTo("separate"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void InvalidIdsAreRejectedBeforeOtherValidation(string id)
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            Assert.Throws<ArgumentException>(() => store.Declare(id, new object()));
            Assert.Throws<ArgumentException>(() => store.Contains(id));
            Assert.Throws<ArgumentException>(() => store.GetValue<object>(id));
            Assert.Throws<ArgumentException>(() => store.TryGetValue<object>(id, out _));
            Assert.Throws<ArgumentException>(() => store.SetValue(id, new object()));
            Assert.Throws<ArgumentException>(() => store.ForceSetValue(id, new object()));
            Assert.Throws<ArgumentException>(() => store.SetCurrentValue(id, new object()));
        }

        [Test]
        public void TryOnlySuppressesMissingIdAndReturnsDefault()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            Assert.That(store.Contains("missing"), Is.False);
            Assert.Throws<InvalidOperationException>(() => store.GetValue<int>("missing"));
            Assert.That(store.TryGetValue<int>("missing", out var number), Is.False);
            Assert.That(number, Is.Zero);
            Assert.That(store.TryGetValue<string>("missing", out var text), Is.False);
            Assert.That(text, Is.Null);
            store.Declare("int", 1);
            Assert.Throws<InvalidOperationException>(() => store.GetValue<float>("int"));
            Assert.Throws<InvalidOperationException>(() => store.TryGetValue<float>("int", out _));
        }

        [TestCase("normal")]
        [TestCase("force")]
        [TestCase("current")]
        public void AllSettersValidateBeforeMutationOrEvent(string setter)
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var variable = store.Declare("id", 1);
            store.SetValue("id", 2);
            var events = 0;
            store.VariableChanged += _ => events++;
            foreach (var id in new[] { null, "", " " })
                Assert.Throws<ArgumentException>(() => Set(store, setter, id, 3));
            Assert.Throws<InvalidOperationException>(() => Set(store, setter, "missing", 3));
            Assert.Throws<InvalidOperationException>(() => Set(store, setter, "id", 3f));
            Assert.Throws<NotSupportedException>(() => Set(store, setter, "id", 3d));
            Assert.That(variable.CurrentValue, Is.EqualTo(2));
            Assert.That(variable.OldValue, Is.EqualTo(1));
            Assert.That(variable.Definition.DefaultValue, Is.EqualTo(1));
            Assert.That(events, Is.Zero);
            Assert.That(store.Contains("missing"), Is.False);
        }

        private static void Set<T>(EcaVariableStore store, string setter, string id, T value)
        {
            switch (setter)
            {
                case "normal": store.SetValue(id, value); break;
                case "force": store.ForceSetValue(id, value); break;
                case "current": store.SetCurrentValue(id, value); break;
                default: throw new ArgumentException(nameof(setter));
            }
        }

        [Test]
        public void UnsupportedTypesAreRejectedForExistingVariablesAfterMissingLookup()
        {
            VerifyUnsupported<double>(); VerifyUnsupported<long>(); VerifyUnsupported<decimal>();
            VerifyUnsupported<DayOfWeek>(); VerifyUnsupported<object>(); VerifyUnsupported<DateTime>();
            VerifyUnsupported<EcaVariablesSystem>(); VerifyUnsupported<int[]>(); VerifyUnsupported<int?>();
            VerifyUnsupported<List<int>>();
        }

        private static void VerifyUnsupported<T>()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            Assert.Throws<NotSupportedException>(() => store.Declare<T>("id", default));
            Assert.That(store.Contains("id"), Is.False);
            store.Declare("existing", 1);
            Assert.Throws<NotSupportedException>(() => store.GetValue<T>("existing"));
            Assert.Throws<NotSupportedException>(() => store.TryGetValue<T>("existing", out _));
            Assert.Throws<NotSupportedException>(() => store.SetValue<T>("existing", default));
            Assert.Throws<NotSupportedException>(() => store.ForceSetValue<T>("existing", default));
            Assert.Throws<NotSupportedException>(() => store.SetCurrentValue<T>("existing", default));
            Assert.Throws<InvalidOperationException>(() => store.GetValue<T>("missing"));
            Assert.That(store.TryGetValue<T>("missing", out var value), Is.False);
            Assert.That(value, Is.EqualTo(default(T)));
            Assert.Throws<InvalidOperationException>(() => store.SetValue<T>("missing", default));
            Assert.Throws<InvalidOperationException>(() => store.ForceSetValue<T>("missing", default));
            Assert.Throws<InvalidOperationException>(() => store.SetCurrentValue<T>("missing", default));
        }

        [Test]
        public void FloatEqualityUsesDefaultComparerIncludingNaN()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var variable = store.Declare("float", float.NaN);
            var events = 0;
            store.VariableChanged += _ => events++;
            store.SetValue("float", float.NaN);
            Assert.That(events, Is.Zero);
            store.ForceSetValue("float", float.NaN);
            Assert.That(events, Is.EqualTo(1));
            store.SetValue("float", 0f);
            store.SetValue("float", -0f);
            Assert.That(events, Is.EqualTo(2));
            Assert.That(variable.OldValue, Is.NaN);
        }

        [Test]
        public void ReentrantMutationCannotChangeOuterSnapshotForLaterSubscriber()
        {
            var store = new EcaVariablesSystem().CreateStore("store");
            var variable = store.Declare("id", 0);
            var seen = new List<EcaVariableChanged>();
            store.VariableChanged += change =>
            {
                if ((int)change.CurrentValue == 1) store.SetValue("id", 2);
            };
            store.VariableChanged += seen.Add;
            store.SetValue("id", 1);
            Assert.That(variable.CurrentValue, Is.EqualTo(2));
            Assert.That(seen, Has.Count.EqualTo(2));
            Assert.That(seen[0].OldValue, Is.EqualTo(1));
            Assert.That(seen[0].CurrentValue, Is.EqualTo(2));
            Assert.That(seen[1].OldValue, Is.EqualTo(0));
            Assert.That(seen[1].CurrentValue, Is.EqualTo(1));
        }
    }
}
