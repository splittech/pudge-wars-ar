using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Hero;
using Game.Core.Hook;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class SimulationDataTests
    {
        [TestCase(typeof(HeroState))]
        [TestCase(typeof(HookState))]
        [TestCase(typeof(MoveIntent))]
        [TestCase(typeof(ThrowHookIntent))]
        [TestCase(typeof(BuyUpgradeIntent))]
        [TestCase(typeof(HookThrown))]
        [TestCase(typeof(HookHit))]
        [TestCase(typeof(HookClash))]
        [TestCase(typeof(HeroDamaged))]
        [TestCase(typeof(HeroDied))]
        public void GetFields_SimulationDataType_ContainsOnlyValueData(Type type)
        {
            // Act.
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // Assert.
            Assert.That(type.IsValueType, Is.True);
            foreach (FieldInfo field in fields)
                AssertValueData(field.FieldType);
        }

        [Test]
        public void Copy_HeroStateIsChanged_OriginalStateIsUnchanged()
        {
            // Arrange.
            HeroState original = Setup.Hero(x: 2f, y: 3f);
            HeroState copy = original;

            // Act.
            copy.Pos.x = 99f;
            copy.Hp = 0;

            // Assert.
            Assert.That(original.Pos.x, Is.EqualTo(2f));
            Assert.That(original.Hp, Is.EqualTo(1000));
        }

        [Test]
        public void Default_HookState_HasNoTargetAndIsIdle()
        {
            // Act.
            HookState hook = default;

            // Assert.
            Assert.That(hook.TargetId, Is.Zero);
            Assert.That(hook.Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void GetUnderlyingType_HookPhase_UsesByteStorage()
        {
            // Act.
            Type storage = Enum.GetUnderlyingType(typeof(HookPhase));

            // Assert.
            Assert.That(storage, Is.EqualTo(typeof(byte)));
        }

        [Test]
        public void Clear_EventBuffer_CopiedEventKeepsDataAndBufferKeepsCapacity()
        {
            // Arrange.
            const double timestamp = 1000000.123456789;
            ThrowHookIntent intent = new(7, new float2(1f, 0f), timestamp);
            List<HookHit> buffer = new(capacity: 1)
            {
                new(intent.PlayerId, 9, new float2(4f, 2f), timestamp)
            };
            HookHit hit = buffer[0];

            // Act.
            buffer.Clear();

            // Assert.
            Assert.That(hit.OwnerId, Is.EqualTo(7));
            Assert.That(hit.TargetId, Is.EqualTo(9));
            Assert.That(hit.Position, Is.EqualTo(new float2(4f, 2f)));
            Assert.That(hit.Time, Is.EqualTo(intent.ClientTime));
            Assert.That(buffer.Capacity, Is.EqualTo(1));
        }

        private static void AssertValueData(Type type)
        {
            Assert.That(type.IsValueType, Is.True, type.FullName);
            if (type.IsPrimitive || type.IsEnum)
                return;

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                AssertValueData(field.FieldType);
        }
    }
}