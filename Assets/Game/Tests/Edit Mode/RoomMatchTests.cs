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
    public sealed class RoomMatchTests
    {
        [Test]
        public void Rules_SameTeamParticipants_CanStartAndRematch()
        {
            MatchRules rules = PlayingRules(1);
            rules.AddKill(Team.A, 7);
            rules.AdvanceRoom(4, 2, 0);
            Assert.That(rules.WinnerPlayerId, Is.EqualTo(7));
            Assert.That(rules.Rematch(10, true, 2, 0), Is.True);
            Assert.That(rules.WinnerPlayerId, Is.Zero);
        }

        [Test]
        public void Rules_EqualIndividualScores_DrawAtDeadline()
        {
            MatchRules rules = PlayingRules();
            rules.AddKill(Team.A, 7);
            rules.AddKill(Team.A, 9);
            rules.AdvanceRoom(63, 2, 0);
            Assert.That(rules.Phase, Is.EqualTo(MatchPhase.Results));
            Assert.That(rules.WinnerPlayerId, Is.Zero);
        }

        [Test]
        public void Rules_LastParticipant_Wins()
        {
            MatchRules rules = PlayingRules();
            rules.AdvanceRoom(4, 1, 9);
            Assert.That(rules.WinnerPlayerId, Is.EqualTo(9));
            Assert.That(rules.SetMode(GameMode.TeamDuel), Is.False);
        }

        [Test]
        public void Simulation_SameTeamHook_AwardsIndividualWin()
        {
            MatchSimulation simulation = CreateSimulation(1);
            simulation.Rules.ArenaPlaced(0);
            simulation.Step(1f / 30, 3);
            simulation.Enqueue(new ThrowHookIntent(1, simulation.GetHero(1).Pos - simulation.GetHero(0).Pos, simulation.Time));
            Setup.Advance(simulation, 60);
            Assert.That(simulation.GetHero(0).Kills, Is.EqualTo(1));
            Assert.That(simulation.GetHero(0).Gold, Is.EqualTo(150));
            Assert.That(simulation.Rules.WinnerPlayerId, Is.EqualTo(1));
        }

        [Test]
        public void Simulation_Movement_CrossesFormerRiverWithoutTerritoryDamage()
        {
            MatchSimulation simulation = CreateSimulation();
            simulation.Rules.ArenaPlaced(0);
            simulation.Step(1f / 30, 3);
            simulation.Enqueue(new MoveIntent(1, new float2(5, 0)));
            Setup.Advance(simulation, 240);
            Assert.That(simulation.GetHero(0).Pos.x, Is.EqualTo(5).Within(0.01f));
            Assert.That(simulation.GetHero(0).Hp, Is.EqualTo(100));
            Assert.That(simulation.GetHero(0).EnemyTerritoryExposed, Is.False);
            Assert.That(simulation.TrySetRoom(new RoomArena(RoomArenaTests.Rectangle(-8, -6, 8, 6))), Is.False);
        }

        [Test]
        public void Prediction_UsesSameFurnitureGeometry()
        {
            var room = new RoomArena(RoomArenaTests.Rectangle(-10, -8, 10, 8), RoomArenaTests.Rectangle(-1, -2, 1, 2));
            var predictor = new MovementPredictor();
            predictor.Reset(1, new Rect2(new float2(-10, -8), new float2(10, 8)));
            predictor.SetRoom(room);
            predictor.SetCollisionContext(1, 0.5f, null);
            predictor.Receive(new MovementSnapshot
            {
                SessionEpoch = 1,
                Pos = new Vector2(-4, 0),
                MoveTarget = new Vector2(-4, 0),
                MoveSpeed = 1000,
                Alive = true
            });
            predictor.Move(new Vector2(4, 0));
            predictor.Step(out _);
            Assert.That(predictor.Position.x, Is.EqualTo(-1.501f).Within(0.002f));
        }

        [Test]
        public void Simulation_PlayerLeaves_OtherSpawnRemainsStable()
        {
            MatchSimulation simulation = CreateSimulation();
            float2 spawn = simulation.GetHero(1).Pos;
            simulation.RemoveHero(1);
            simulation.ResetRound();
            Assert.That(simulation.GetHero(0).Id, Is.EqualTo(2));
            Assert.That(simulation.GetHero(0).Pos, Is.EqualTo(spawn));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ServerCommands_RoomTarget_IsNotClampedToTeamBank(bool frame)
        {
            MatchSimulation simulation = CreateSimulation();
            simulation.Rules.ArenaPlaced(0);
            simulation.Step(1f / 30, 3);
            var commands = new ServerPlayerCommands(simulation, Setup.Arena());
            if (frame)
            {
                Assert.That(commands.TryMovement(1, new MovementFrame
                {
                    Sequence = 1,
                    ClientTick = simulation.TickCount,
                    SessionEpoch = 1,
                    HasMoveTarget = true,
                    Target = new Vector2(5, 0)
                }, 1, simulation.Time, true, out _), Is.True);
            }
            else
            {
                Assert.That(commands.TryMove(1, new float2(5, 0), simulation.Time, true, out _), Is.True);
            }

            commands.Flush(simulation.Time);
            simulation.Step(1f / 30);
            Assert.That(simulation.GetHero(0).MoveTarget.x, Is.EqualTo(5));
        }

        private static MatchRules PlayingRules(int kills = 15)
        {
            var rules = new MatchRules(Setup.MatchConfig(kills, 60));
            Assert.That(rules.SetMode(GameMode.RoomFreeForAll), Is.True);
            Assert.That(rules.Begin(0, true, true, 2, 0), Is.True);
            rules.ArenaPlaced(0);
            rules.Advance(3, 2, 0);
            return rules;
        }

        private static MatchSimulation CreateSimulation(int kills = 15)
        {
            var arena = new ArenaConfigData(new float2(24, 14), 3.5f,
                new[] { new float2(-4, 0), new float2(-4, 3) }, new[] { new float2(4, 0) });
            var simulation = new MatchSimulation(arena, new HeroConfigData(100, 3.2f, 0.5f, 4), Setup.HookConfig(),
                new[] { Setup.Hero(), Setup.Hero(id: 2, y: 3) });
            simulation.ConfigureRules(Setup.MatchConfig(kills), new EconomyConfigData(150, 50, 10, 0, new[] { 100 }));
            simulation.Rules.SetMode(GameMode.RoomFreeForAll);
            simulation.Rules.Begin(0, true, true, 2, 0);
            Assert.That(simulation.TrySetRoom(new RoomArena(RoomArenaTests.Rectangle(-10, -7, 10, 7))), Is.True);
            return simulation;
        }
    }
}
