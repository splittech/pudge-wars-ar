using System.Collections.Generic;
using Game.Core;
using Game.Core.Configs;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class ConfigTests
    {
        private readonly List<ScriptableObject> _configs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ScriptableObject config in _configs)
                Object.DestroyImmediate(config);
            _configs.Clear();
        }

        [Test]
        public void ToData_DefaultHookConfig_ReturnsStartingHookBalance()
        {
            // Arrange.
            HookConfig config = CreateConfig<HookConfig>();

            // Act.
            HookConfigData data = config.ToData();

            // Assert.
            Assert.That(data.Damage, Is.EqualTo(175));
            Assert.That(data.Range, Is.EqualTo(12f));
        }

        [Test]
        public void ToData_DefaultHeroConfig_ReturnsStartingHp()
        {
            // Arrange.
            HeroConfig config = CreateConfig<HeroConfig>();

            // Act.
            HeroConfigData data = config.ToData();

            // Assert.
            Assert.That(data.MaxHp, Is.EqualTo(1000));
        }

        [Test]
        public void ToData_DefaultEconomyConfig_ReturnsAssistWindowAndPrices()
        {
            // Arrange.
            EconomyConfig config = CreateConfig<EconomyConfig>();
            int[] expectedPrices = { 100, 200, 300, 400 };

            // Act.
            EconomyConfigData data = config.ToData();

            // Assert.
            Assert.That(data.AssistWindow, Is.EqualTo(10f));
            Assert.That(data.UpgradePrices, Is.EqualTo(expectedPrices));
        }

        [Test]
        public void ToData_DefaultArenaConfig_ReturnsSizeAndSpawnPoints()
        {
            // Arrange.
            ArenaConfig config = CreateConfig<ArenaConfig>();

            // Act.
            ArenaConfigData data = config.ToData();

            // Assert.
            Assert.That(data.Size, Is.EqualTo(new float2(24f, 14f)));
            Assert.That(data.TeamASpawnPoints.Count, Is.EqualTo(3));
            Assert.That(data.TeamBSpawnPoints[2], Is.EqualTo(new float2(8f, 4f)));
        }

        [Test]
        public void ToData_DefaultMatchConfig_ReturnsStartingMatchRules()
        {
            // Arrange.
            MatchConfig config = CreateConfig<MatchConfig>();

            // Act.
            MatchConfigData data = config.ToData();

            // Assert.
            Assert.That(data.TickRate, Is.EqualTo(30));
            Assert.That(data.MaxPlayers, Is.EqualTo(4));
            Assert.That(data.MatchDuration, Is.EqualTo(480f));
        }

        [Test]
        public void ProtocolVersion_InitialProtocol_IsOne()
        {
            // Act.
            int version = NetworkProtocol.ProtocolVersion;

            // Assert.
            Assert.That(version, Is.EqualTo(1));
        }

        [Test]
        public void EconomyConfigData_SourcePricesChanged_SnapshotKeepsOriginalPrices()
        {
            // Arrange.
            int[] prices = { 100, 200 };
            EconomyConfigData data = new(150, 50, 10f, 0, prices);

            // Act.
            prices[0] = 999;

            // Assert.
            Assert.That(data.UpgradePrices[0], Is.EqualTo(100));
        }

        [Test]
        public void ArenaConfigData_SourceSpawnsChanged_BothTeamsKeepOriginalPositions()
        {
            // Arrange.
            float2[] spawns = { new(-8f, 0f) };
            ArenaConfigData data = new(new float2(24f, 14f), 3.5f, spawns, spawns);

            // Act.
            spawns[0] = float2.zero;

            // Assert.
            Assert.That(data.TeamASpawnPoints[0], Is.EqualTo(new float2(-8f, 0f)));
            Assert.That(data.TeamBSpawnPoints[0], Is.EqualTo(new float2(-8f, 0f)));
        }

        private T CreateConfig<T>() where T : ScriptableObject
        {
            T config = ScriptableObject.CreateInstance<T>();
            _configs.Add(config);
            return config;
        }
    }
}