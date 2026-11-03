using Game.Core;
using Game.Core.Configs;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ConfigTests
    {
        [Test]
        public void Defaults_MatchStartingBalance()
        {
            var hook = ScriptableObject.CreateInstance<HookConfig>();
            var hero = ScriptableObject.CreateInstance<HeroConfig>();
            var economy = ScriptableObject.CreateInstance<EconomyConfig>();
            var arena = ScriptableObject.CreateInstance<ArenaConfig>();
            var match = ScriptableObject.CreateInstance<MatchConfig>();
            try
            {
                Assert.That(hook.ToData().Damage, Is.EqualTo(175));
                Assert.That(hook.ToData().Range, Is.EqualTo(12));
                Assert.That(hero.ToData().MaxHp, Is.EqualTo(1000));
                Assert.That(economy.ToData().AssistWindow, Is.EqualTo(10));
                CollectionAssert.AreEqual(new[] { 100, 200, 300, 400 }, economy.ToData().UpgradePrices);
                Assert.That(arena.ToData().Size, Is.EqualTo(new float2(24, 14)));
                Assert.That(arena.ToData().TeamASpawnPoints.Count, Is.EqualTo(3));
                Assert.That(arena.ToData().TeamBSpawnPoints[2], Is.EqualTo(new float2(8, 4)));
                Assert.That(match.ToData().TickRate, Is.EqualTo(30));
                Assert.That(match.ToData().MaxPlayers, Is.EqualTo(4));
                Assert.That(match.ToData().MatchDuration, Is.EqualTo(480));
                Assert.That(NetworkProtocol.ProtocolVersion, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(hook);
                Object.DestroyImmediate(hero);
                Object.DestroyImmediate(economy);
                Object.DestroyImmediate(arena);
                Object.DestroyImmediate(match);
            }
        }

        [Test]
        public void Snapshots_DoNotShareMutableArrays()
        {
            var prices = new[] { 100, 200 };
            var economy = new EconomyConfigData(150, 50, 10, 0, prices);
            prices[0] = 999;
            Assert.That(economy.UpgradePrices[0], Is.EqualTo(100));

            var spawns = new[] { new float2(-8, 0) };
            var arena = new ArenaConfigData(new float2(24, 14), 3.5f, spawns, spawns);
            spawns[0] = float2.zero;
            Assert.That(arena.TeamASpawnPoints[0], Is.EqualTo(new float2(-8, 0)));
            Assert.That(arena.TeamBSpawnPoints[0], Is.EqualTo(new float2(-8, 0)));
        }
    }
}
