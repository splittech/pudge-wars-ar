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
        [Test]
        public void StatesIntentsAndEvents_ContainOnlyValueData()
        {
            Type[] types = { typeof(HeroState), typeof(HookState), typeof(MoveIntent),
                typeof(ThrowHookIntent), typeof(BuyUpgradeIntent), typeof(HookThrown),
                typeof(HookHit), typeof(HookClash), typeof(HeroDamaged), typeof(HeroDied) };
            foreach (Type type in types)
                AssertValueData(type);
        }

        [Test]
        public void HeroState_IsCopiedByValue()
        {
            var original = new HeroState { Id = 1, Pos = new float2(2, 3), Hp = 1000, Alive = true };
            var copy = original;
            copy.Pos.x = 99;
            copy.Hp = 0;
            Assert.That(original.Pos.x, Is.EqualTo(2));
            Assert.That(original.Hp, Is.EqualTo(1000));
        }

        [Test]
        public void Hook_DefaultHasNoTargetAndIsIdle()
        {
            HookState hook = default;
            Assert.That(hook.TargetId, Is.Zero);
            Assert.That(hook.Phase, Is.EqualTo(HookPhase.Idle));
            Assert.That(Enum.GetUnderlyingType(typeof(HookPhase)), Is.EqualTo(typeof(byte)));
        }

        [Test]
        public void ThrowAndHit_PreserveArenaDataAndDoubleTimestamps()
        {
            const double time = 1000000.123456789;
            var intent = new ThrowHookIntent(7, new float2(1, 0), time);
            var buffer = new List<HookHit>(1) { new HookHit(intent.PlayerId, 9, new float2(4, 2), time) };
            HookHit hit = buffer[0];
            buffer.Clear();
            Assert.That(hit.OwnerId, Is.EqualTo(7));
            Assert.That(hit.TargetId, Is.EqualTo(9));
            Assert.That(hit.Position, Is.EqualTo(new float2(4, 2)));
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
