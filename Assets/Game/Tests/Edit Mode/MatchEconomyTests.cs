using Game.Core.Combat;
using Game.Core.Commands;
using Game.Core.Economy;
using Game.Core.Match;
using Game.Net;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class MatchEconomyTests
    {
        [TestCase(0, 50)]
        [TestCase(11, 0)]
        public void Step_AssistedKill_AwardsOnlyRecentDamage(float delay, int assistGold)
        {
            // Arrange.
            var simulation = Setup.MatchWithRules(victimHp: 350, territoryDamage: 0);
            simulation.AddHero(Setup.Hero(id: 3, y: 3));
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), simulation.Time));
            Setup.Advance(simulation, 60);
            simulation.Enqueue(new MoveIntent(2, new float2(4, 0)));
            Setup.Advance(simulation, 90);
            if (delay > 0)
            {
                simulation.Step(0.01f, simulation.Time + delay);
            }

            float2 direction = simulation.GetHero(1).Pos - simulation.GetHero(2).Pos;
            simulation.Enqueue(new ThrowHookIntent(3, direction, simulation.Time));
            // Act.
            Setup.Advance(simulation, 30);
            // Assert.
            Assert.That(simulation.GetHero(2).Gold, Is.EqualTo(150));
            Assert.That(simulation.GetHero(0).Gold, Is.EqualTo(assistGold));
            Assert.That(simulation.GetHero(0).Assists, Is.EqualTo(assistGold > 0 ? 1 : 0));
        }

        [Test]
        public void TryBuyUpgrade_DuringFlight_LeavesActiveHookParametersUnchanged()
        {
            // Arrange.
            var simulation = Setup.MatchWithRules(startGold: 100);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(0, 1), simulation.Time));
            Setup.Advance(simulation, 1);
            // Act.
            simulation.TryBuyUpgrade(1, UpgradeId.Hook, out _);
            Setup.Advance(simulation, 1);
            // Assert.
            Assert.That(simulation.GetHero(0).HookSpeed, Is.EqualTo(24));
            Assert.That(simulation.GetHook(0).Speed, Is.EqualTo(20));
            Assert.That(simulation.GetHook(0).Length, Is.EqualTo(40f / 30).Within(0.0001f));
        }

        [TestCase(9.99, true)]
        [TestCase(10, true)]
        [TestCase(10.01, false)]
        public void Assists_DamageWindow_FiltersParticipants(double elapsed, bool eligible)
        {
            // Arrange.
            var history = new DamageHistory();
            history.Record(3, 1, 20);
            history.Record(3, 2, 20);
            // Act.
            var assistants = new System.Collections.Generic.List<uint>(history.Assists(3, 2, 20 + elapsed, 10));
            // Assert.
            Assert.That(assistants.Contains(1), Is.EqualTo(eligible));
            Assert.That(assistants.Contains(2), Is.False);
        }

        [Test]
        public void Step_KillingHook_AwardsGoldScoreAndStatistics()
        {
            // Arrange.
            var simulation = Setup.MatchWithRules(victimHp: 175);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), simulation.Time));
            // Act.
            Setup.Advance(simulation, 15);
            // Assert.
            Assert.That(simulation.GetHero(0).Gold, Is.EqualTo(150));
            Assert.That(simulation.GetHero(0).Kills, Is.EqualTo(1));
            Assert.That(simulation.GetHero(0).HookHits, Is.EqualTo(1));
            Assert.That(simulation.GetHero(1).Deaths, Is.EqualTo(1));
            Assert.That(simulation.GetHero(1).Alive, Is.False);
            Assert.That(simulation.Rules.ScoreA, Is.EqualTo(1));
        }

        [Test]
        public void Step_RespawnDeadline_RestoresFullHealthAtSpawn()
        {
            // Arrange.
            var simulation = Setup.MatchWithRules(victimHp: 175);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), simulation.Time));
            Setup.Advance(simulation, 15);
            double deadline = simulation.GetHero(1).RespawnAt;
            simulation.Step(0.001f, deadline - 0.01);
            Assert.That(simulation.GetHero(1).Alive, Is.False);
            // Act.
            simulation.Step(0.001f, deadline);
            // Assert.
            Assert.That(simulation.GetHero(1).Alive, Is.True);
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(1000));
            Assert.That(simulation.GetHero(1).Pos, Is.EqualTo(new float2(4, 0)));
            Assert.That(simulation.GetHero(1).Deaths, Is.EqualTo(1));
        }

        [Test]
        public void Respawn_UpgradedHook_PreservesCommonLevelAndAllStats()
        {
            var simulation = Setup.MatchWithRules(startGold: 100, victimHp: 175);
            var heroes = (Game.Core.Hero.HeroState[])typeof(MatchSimulation)
                .GetField("_heroes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(simulation);
            // Starting gold resets the round to full HP; arrange a one-hit victim for this lifecycle test.
            heroes[1].Hp = 175;
            Assert.That(simulation.TryBuyUpgrade(2, UpgradeId.Hook, out _), Is.True);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), simulation.Time));
            Setup.Advance(simulation, 15);
            Assert.That(simulation.GetHero(1).Alive, Is.False);
            simulation.Step(0.001f, simulation.GetHero(1).RespawnAt);
            var hero = simulation.GetHero(1);
            Assert.That(hero.Alive, Is.True);
            Assert.That(hero.HookLevel, Is.EqualTo(1));
            Assert.That(hero.HookRange, Is.EqualTo(13.5f));
            Assert.That(hero.HookSpeed, Is.EqualTo(24));
            Assert.That(hero.HookRadius, Is.EqualTo(0.43f).Within(0.00001f));
            Assert.That(hero.HookDamage, Is.EqualTo(250));
        }

        [TestCase(UpgradeId.Hook)]
        public void TryBuyUpgrade_FourLevels_RejectsFifthWithoutSpending(UpgradeId upgrade)
        {
            // Arrange.
            var simulation = Setup.MatchWithRules(startGold: 2000);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(simulation.TryBuyUpgrade(1, upgrade, out _), Is.True);
                var hero = simulation.GetHero(0);
                int level = i + 1;
                Assert.That(hero.HookLevel, Is.EqualTo(level));
                Assert.That(hero.HookRange, Is.EqualTo(12 + level * 1.5f));
                Assert.That(hero.HookSpeed, Is.EqualTo(20 + level * 4));
                Assert.That(hero.HookRadius, Is.EqualTo(0.35f + level * 0.08f).Within(0.00001f));
                Assert.That(hero.HookDamage, Is.EqualTo(175 + level * 75));
            }
            // Act.
            bool accepted = simulation.TryBuyUpgrade(1, upgrade, out _);
            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(simulation.UpgradeLevel(1, upgrade), Is.EqualTo(4));
            Assert.That(simulation.GetHero(0).Gold, Is.EqualTo(1000));
        }

        [Test]
        public void TryBuyUpgrade_InsufficientGold_LeavesStateUnchanged()
        {
            // Arrange.
            var simulation = Setup.MatchWithRules();
            // Act.
            bool accepted = simulation.TryBuyUpgrade(1, UpgradeId.Hook, out _);
            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(simulation.GetHero(0).HookLevel, Is.Zero);
            Assert.That(simulation.GetHero(0).HookRange, Is.EqualTo(12));
        }

        [Test]
        public void TryBuyUpgrade_Hook_RaisesAllFourStatsForOnePrice()
        {
            // Arrange.
            var simulation = Setup.MatchWithRules(startGold: 100);
            // Act.
            bool accepted = simulation.TryBuyUpgrade(1, UpgradeId.Hook, out _);
            // Assert.
            Assert.That(accepted, Is.True);
            Assert.That(simulation.GetHero(0).HookRange, Is.EqualTo(13.5f));
            Assert.That(simulation.GetHero(0).HookSpeed, Is.EqualTo(24));
            Assert.That(simulation.GetHero(0).HookRadius, Is.EqualTo(0.43f).Within(0.00001f));
            Assert.That(simulation.GetHero(0).HookDamage, Is.EqualTo(250));
            Assert.That(simulation.GetHero(0).HookLevel, Is.EqualTo(1));
            Assert.That(simulation.GetHero(0).Gold, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(int.MaxValue)]
        public void TryBuyUpgrade_UnknownOrRemovedOption_RejectsWithoutSpending(int upgrade)
        {
            var simulation = Setup.MatchWithRules(startGold: 1000);
            Assert.That(simulation.TryBuyUpgrade(1, (UpgradeId)upgrade, out _), Is.False);
            Assert.That(simulation.GetHero(0).Gold, Is.EqualTo(1000));
            Assert.That(simulation.GetHero(0).HookLevel, Is.Zero);
            Assert.That(simulation.GetHero(0).HookDamage, Is.EqualTo(175));
        }

        [Test]
        public void TryThrow_Results_RejectsBattleInput()
        {
            // Arrange.
            var simulation = Setup.MatchWithRules();
            simulation.Rules.Advance(500, 1, 1);
            var commands = new ServerPlayerCommands(simulation, Setup.Arena());
            // Act.
            bool accepted = commands.TryThrow(1, new float2(1, 0), 500, true, out _);
            // Assert.
            Assert.That(accepted, Is.False);
        }

        [Test]
        public void ResetRound_UpgradedHero_ClearsEconomyAndPendingCommands()
        {
            // Arrange.
            var simulation = Setup.MatchWithRules(startGold: 100);
            simulation.TryBuyUpgrade(1, UpgradeId.Hook, out _);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), simulation.Time));
            // Act.
            simulation.ResetRound();
            Setup.Advance(simulation, 1);
            // Assert.
            Assert.That(simulation.GetHero(0).Gold, Is.EqualTo(100));
            Assert.That(simulation.GetHero(0).HookLevel, Is.Zero);
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(Game.Core.Hook.HookPhase.Idle));
        }
    }
}
