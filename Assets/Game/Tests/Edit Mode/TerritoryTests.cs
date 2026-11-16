using Game.Core.Commands;
using Game.Core.Configs;
using Game.Core.Geometry;
using Game.Core.Match;
using Game.Net;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class TerritoryTests
    {
        private static MatchSimulation Captured(int damage = 100)
        {
            var arena = new ArenaConfigData(new float2(24, 14), 3.5f,
                new[] { new float2(-4, 0) }, new[] { new float2(4, 0) });
            var simulation = new MatchSimulation(arena, new HeroConfigData(1000, 3.2f, 0.5f, 4, 2, 0.5f, damage),
                Setup.HookConfig(), new[] { Setup.Hero(), Setup.Hero(2, Team.B, 4) });
            simulation.ConfigureRules(Setup.MatchConfig(), new EconomyConfigData(150, 50, 10, 0, new[] { 100, 200, 300, 400 }));
            simulation.Rules.Begin(0, true, true, 1, 1);
            simulation.Rules.ArenaPlaced(0);
            simulation.Step(Setup.TickDeltaTime, 3);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), simulation.Time));
            Setup.Advance(simulation, 45);
            Assert.That(simulation.GetHero(1).IsPulled, Is.False);
            Assert.That(simulation.GetHero(1).Pos.x, Is.LessThan(0));
            return simulation;
        }

        [Test]
        public void CapturedHero_CanMoveOnForeignBankButCannotWalkHome()
        {
            var simulation = Captured(0);
            var commands = new ServerPlayerCommands(simulation, Setup.Arena());
            Assert.That(commands.TryMove(2, new float2(-3, 2), simulation.Time, true, out _), Is.True);
            commands.Flush(simulation.Time);
            Setup.Advance(simulation, 60);
            simulation.Enqueue(new MoveIntent(2, new float2(-5, 2)));
            Setup.Advance(simulation, 30);
            Assert.That(math.distance(simulation.GetHero(1).Pos, new float2(-5, 2)), Is.LessThan(0.001));
            simulation.Enqueue(new MoveIntent(2, new float2(8, 2)));
            Setup.Advance(simulation, 120);
            Assert.That(simulation.GetHero(1).Pos.x, Is.EqualTo(-2.25f).Within(0.0001));
        }

        [Test]
        public void TerritoryDamage_RespectsGraceIntervalAndMissedIntervals()
        {
            var simulation = Captured();
            var victim = simulation.GetHero(1);
            Assert.That(victim.EnemyTerritoryExposed, Is.True);
            double due = victim.EnemyTerritoryDamageAt;
            int hp = victim.Hp;
            simulation.Step(0.001f, due - 0.01);
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(hp));
            simulation.Step(0.001f, due);
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(hp - 100));
            simulation.Step(0.001f, due + 1);
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(hp - 300));
            Assert.That(simulation.Damage[0].AttackerId, Is.Zero);
        }

        [Test]
        public void TerritoryDeath_RespawnsWithoutKillGoldOrTeamScore()
        {
            var simulation = Captured(1000);
            simulation.Step(0.001f, simulation.GetHero(1).EnemyTerritoryDamageAt);
            var victim = simulation.GetHero(1);
            Assert.That(victim.Alive, Is.False);
            Assert.That(victim.Hp, Is.Zero);
            Assert.That(victim.Deaths, Is.EqualTo(1));
            Assert.That(simulation.Deaths[0].KillerId, Is.Zero);
            Assert.That(simulation.GetHero(0).Kills, Is.Zero);
            Assert.That(simulation.GetHero(0).Gold, Is.Zero);
            Assert.That(simulation.Rules.ScoreA, Is.Zero);
            simulation.Step(0.001f, victim.RespawnAt);
            victim = simulation.GetHero(1);
            Assert.That(victim.Alive, Is.True);
            Assert.That(victim.Hp, Is.EqualTo(1000));
            Assert.That(victim.EnemyTerritoryExposed, Is.False);
            Assert.That(victim.EnemyTerritoryDamageAt, Is.Zero);
            Assert.That(victim.Pos.x, Is.GreaterThan(0));
        }

        [Test]
        public void AlliedRescue_LeavingForeignBankClearsExposure()
        {
            var simulation = Captured();
            simulation.AddHero(Setup.Hero(3, Team.B, 8, 2));
            float2 direction = simulation.GetHero(1).Pos - simulation.GetHero(2).Pos;
            simulation.Enqueue(new ThrowHookIntent(3, direction, simulation.Time));
            Setup.Advance(simulation, 60);
            var rescued = simulation.GetHero(1);
            Assert.That(rescued.Pos.x, Is.GreaterThan(1.75f));
            Assert.That(rescued.EnemyTerritoryExposed, Is.False);
            Assert.That(rescued.EnemyTerritoryDamageAt, Is.Zero);
        }

        [Test]
        public void DisabledTerritoryDamage_DoesNotArmDeadline()
        {
            var simulation = Captured(0);
            int hp = simulation.GetHero(1).Hp;
            simulation.Step(0.001f, simulation.Time + 20);
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(hp));
            Assert.That(simulation.GetHero(1).EnemyTerritoryExposed, Is.False);
        }

        [Test]
        public void Prediction_UsesForeignBankAfterAuthoritativeTransfer()
        {
            var predictor = new MovementPredictor();
            predictor.Reset(1, ArenaBounds.ForTeam(Setup.Arena(), Team.B, 0.5f));
            predictor.Receive(new MovementSnapshot { SessionEpoch = 1, Alive = true, Pos = new Vector2(-3, 0),
                MoveTarget = new Vector2(-3, 0), MoveSpeed = 3.2f });
            Assert.That(predictor.Move(new Vector2(-7, 2)), Is.True);
            Assert.That(predictor.Target, Is.EqualTo(new Vector2(-7, 2)));
            predictor.Step(out _);
            Assert.That(predictor.Position.x, Is.LessThan(-3));
            predictor.Move(new Vector2(7, 2));
            Assert.That(predictor.Target.x, Is.EqualTo(-2.25f));
        }

        [TestCase(-1f, 1f, 50)]
        [TestCase(1f, 0f, 50)]
        [TestCase(1f, 1f, -1)]
        public void TerritoryConfig_RejectsInvalidSettings(float grace, float interval, int damage)
            => Assert.Throws<System.ArgumentOutOfRangeException>(() => new HeroConfigData(1000, 3.2f, 0.5f, 4, grace, interval, damage));
    }
}
