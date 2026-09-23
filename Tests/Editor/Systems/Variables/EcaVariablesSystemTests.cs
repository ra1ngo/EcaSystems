using System;
using System.Collections.Generic;
using EcaSystems.Variables;
using NUnit.Framework;

namespace EcaSystems.Tests.Variables
{
    public sealed class EcaVariablesSystemTests
    {
        [Test] public void IntDeclarationAndMutations() => VerifyValues(0, 10, 50, 60);
        [Test] public void FloatDeclarationAndMutations() => VerifyValues(0f, 1.25f, 2.5f, 3.75f);
        [Test] public void BoolDeclarationAndMutations() => VerifyValues(false, true, false, true);
        [Test] public void StringDeclarationAndMutations() => VerifyValues("default", "first", "direct", "last");
        [Test] public void NullStringDeclarationAndMutations() => VerifyValues<string>(null, "first", "direct", null);

        private static void VerifyValues<T>(T initial, T changed, T direct, T last)
        {
            var system = new EcaVariablesSystem();
            var events = new List<EcaVariableChanged>();
            system.VariableChanged += events.Add;
            var variable = system.Declare("id", initial);
            var definition = variable.Definition;
            Assert.That(definition.Id, Is.EqualTo("id"));
            Assert.That(definition.ValueType, Is.EqualTo(typeof(T)));
            Assert.That(definition.DefaultValue, Is.EqualTo(initial));
            Assert.That(variable.CurrentValue, Is.EqualTo(initial));
            Assert.That(variable.OldValue, Is.EqualTo(initial));
            Assert.That(system.GetValue<T>("id"), Is.EqualTo(initial));
            Assert.That(system.TryGetValue<T>("id", out var found), Is.True);
            Assert.That(found, Is.EqualTo(initial));
            Assert.That(events, Is.Empty);
            system.SetValue("id", initial);
            Assert.That(events, Is.Empty);
            system.SetValue("id", changed);
            Assert.That(variable.OldValue, Is.EqualTo(initial));
            Assert.That(variable.CurrentValue, Is.EqualTo(changed));
            Assert.That(events, Has.Count.EqualTo(1));
            var snapshot = events[0];
            Assert.That((object)snapshot, Is.Not.SameAs(variable));
            Assert.That(snapshot.Definition, Is.SameAs(definition));
            Assert.That(snapshot.OldValue, Is.EqualTo(initial));
            Assert.That(snapshot.CurrentValue, Is.EqualTo(changed));
            system.SetValue("id", changed);
            Assert.That(variable.OldValue, Is.EqualTo(initial));
            Assert.That(events, Has.Count.EqualTo(1));
            system.ForceSetValue("id", changed);
            Assert.That(variable.OldValue, Is.EqualTo(changed));
            Assert.That(variable.CurrentValue, Is.EqualTo(changed));
            Assert.That(events, Has.Count.EqualTo(2));
            Assert.That(events[1], Is.Not.SameAs(snapshot));
            Assert.That(events[1].OldValue, Is.EqualTo(changed));
            system.ForceSetValue("id", initial);
            Assert.That(variable.OldValue, Is.EqualTo(changed));
            Assert.That(variable.CurrentValue, Is.EqualTo(initial));
            Assert.That(events, Has.Count.EqualTo(3));
            Assert.That(events[2].OldValue, Is.EqualTo(changed));
            Assert.That(events[2].CurrentValue, Is.EqualTo(initial));
            system.SetCurrentValue("id", direct);
            Assert.That(variable.CurrentValue, Is.EqualTo(direct));
            Assert.That(variable.OldValue, Is.EqualTo(changed));
            Assert.That(events, Has.Count.EqualTo(3));
            system.SetValue("id", last);
            Assert.That(variable.CurrentValue, Is.EqualTo(last));
            Assert.That(variable.OldValue, Is.EqualTo(direct));
            Assert.That(system.GetValue<T>("id"), Is.EqualTo(last));
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
            var system = new EcaVariablesSystem();
            var variable = system.Declare("Money", 10);
            Assert.That(system.Contains("Money"), Is.True);
            Assert.That(system.Contains("money"), Is.False);
            system.Declare("money", "separate");
            Assert.Throws<InvalidOperationException>(() => system.Declare("Money", 10));
            Assert.Throws<InvalidOperationException>(() => system.Declare("Money", "wrong"));
            Assert.That(variable.CurrentValue, Is.EqualTo(10));
            Assert.That(system.GetValue<string>("money"), Is.EqualTo("separate"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void InvalidIdsAreRejectedBeforeOtherValidation(string id)
        {
            var system = new EcaVariablesSystem();
            Assert.Throws<ArgumentException>(() => system.Declare(id, new object()));
            Assert.Throws<ArgumentException>(() => system.Contains(id));
            Assert.Throws<ArgumentException>(() => system.GetValue<object>(id));
            Assert.Throws<ArgumentException>(() => system.TryGetValue<object>(id, out _));
            Assert.Throws<ArgumentException>(() => system.SetValue(id, new object()));
            Assert.Throws<ArgumentException>(() => system.ForceSetValue(id, new object()));
            Assert.Throws<ArgumentException>(() => system.SetCurrentValue(id, new object()));
        }

        [Test]
        public void TryOnlySuppressesMissingIdAndReturnsDefault()
        {
            var system = new EcaVariablesSystem();
            Assert.That(system.Contains("missing"), Is.False);
            Assert.Throws<InvalidOperationException>(() => system.GetValue<int>("missing"));
            Assert.That(system.TryGetValue<int>("missing", out var number), Is.False);
            Assert.That(number, Is.Zero);
            Assert.That(system.TryGetValue<string>("missing", out var text), Is.False);
            Assert.That(text, Is.Null);
            system.Declare("int", 1);
            Assert.Throws<InvalidOperationException>(() => system.GetValue<float>("int"));
            Assert.Throws<InvalidOperationException>(() => system.TryGetValue<float>("int", out _));
        }

        [TestCase("normal")]
        [TestCase("force")]
        [TestCase("current")]
        public void AllSettersValidateBeforeMutationOrEvent(string setter)
        {
            var system = new EcaVariablesSystem();
            var variable = system.Declare("id", 1);
            system.SetValue("id", 2);
            var events = 0;
            system.VariableChanged += _ => events++;
            foreach (var id in new[] { null, "", " " })
                Assert.Throws<ArgumentException>(() => Set(system, setter, id, 3));
            Assert.Throws<InvalidOperationException>(() => Set(system, setter, "missing", 3));
            Assert.Throws<InvalidOperationException>(() => Set(system, setter, "id", 3f));
            Assert.Throws<NotSupportedException>(() => Set(system, setter, "id", 3d));
            Assert.That(variable.CurrentValue, Is.EqualTo(2));
            Assert.That(variable.OldValue, Is.EqualTo(1));
            Assert.That(variable.Definition.DefaultValue, Is.EqualTo(1));
            Assert.That(events, Is.Zero);
            Assert.That(system.Contains("missing"), Is.False);
        }

        private static void Set<T>(EcaVariablesSystem system, string setter, string id, T value)
        {
            switch (setter)
            {
                case "normal": system.SetValue(id, value); break;
                case "force": system.ForceSetValue(id, value); break;
                case "current": system.SetCurrentValue(id, value); break;
                default: throw new ArgumentException(nameof(setter));
            }
        }

        [Test]
        public void UnsupportedTypesAreRejectedByEveryGenericOperationEvenForMissingIds()
        {
            VerifyUnsupported<double>(); VerifyUnsupported<long>(); VerifyUnsupported<decimal>();
            VerifyUnsupported<DayOfWeek>(); VerifyUnsupported<object>(); VerifyUnsupported<DateTime>();
            VerifyUnsupported<EcaVariablesSystem>(); VerifyUnsupported<int[]>(); VerifyUnsupported<int?>();
            VerifyUnsupported<List<int>>();
        }

        private static void VerifyUnsupported<T>()
        {
            var system = new EcaVariablesSystem();
            Assert.Throws<NotSupportedException>(() => system.Declare<T>("id", default));
            Assert.That(system.Contains("id"), Is.False);
            Assert.Throws<NotSupportedException>(() => system.GetValue<T>("missing"));
            Assert.Throws<NotSupportedException>(() => system.TryGetValue<T>("missing", out _));
            Assert.Throws<NotSupportedException>(() => system.SetValue<T>("missing", default));
            Assert.Throws<NotSupportedException>(() => system.ForceSetValue<T>("missing", default));
            Assert.Throws<NotSupportedException>(() => system.SetCurrentValue<T>("missing", default));
        }

        [Test]
        public void FloatEqualityUsesDefaultComparerIncludingNaN()
        {
            var system = new EcaVariablesSystem();
            var variable = system.Declare("float", float.NaN);
            var events = 0;
            system.VariableChanged += _ => events++;
            system.SetValue("float", float.NaN);
            Assert.That(events, Is.Zero);
            system.ForceSetValue("float", float.NaN);
            Assert.That(events, Is.EqualTo(1));
            system.SetValue("float", 0f);
            system.SetValue("float", -0f);
            Assert.That(events, Is.EqualTo(2));
            Assert.That(variable.OldValue, Is.NaN);
        }

        [Test]
        public void ReentrantMutationCannotChangeOuterSnapshotForLaterSubscriber()
        {
            var system = new EcaVariablesSystem();
            var variable = system.Declare("id", 0);
            var seen = new List<EcaVariableChanged>();
            system.VariableChanged += change =>
            {
                if ((int)change.CurrentValue == 1) system.SetValue("id", 2);
            };
            system.VariableChanged += seen.Add;
            system.SetValue("id", 1);
            Assert.That(variable.CurrentValue, Is.EqualTo(2));
            Assert.That(seen, Has.Count.EqualTo(2));
            Assert.That(seen[0].OldValue, Is.EqualTo(1));
            Assert.That(seen[0].CurrentValue, Is.EqualTo(2));
            Assert.That(seen[1].OldValue, Is.EqualTo(0));
            Assert.That(seen[1].CurrentValue, Is.EqualTo(1));
        }
    }
}
