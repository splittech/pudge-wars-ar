using Game.Core.Commands;
using Game.Core.Hero;
using Game.Core.Match;
using Game.Net;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HeroCollisionTests
    {
        [Test]
        public void SweptMovement_CannotTunnelThroughStandingHero()
        {
            var moving = Setup.Hero(x: -8);
            var blocker = Setup.Hero(2, x: -4);
            float2 result = HeroCollision.Move(moving, new float2(-2, 0), new[] { blocker });
            Assert.That(result.x, Is.EqualTo(-5).Within(0.0001));
        }

        [Test]
        public void Contact_AllowsSeparationAndTangentialMovement()
        {
            var moving = Setup.Hero(x: -5);
            var blocker = Setup.Hero(2, x: -4);
            Assert.That(HeroCollision.Move(moving, new float2(-6, 0), new[] { blocker }).x, Is.EqualTo(-6));
            Assert.That(HeroCollision.Move(moving, new float2(-5, 1), new[] { blocker }).y, Is.EqualTo(1));
        }

        [Test]
        public void DeadHero_DoesNotBlockMovement()
        {
            var moving = Setup.Hero(x: -8);
            var blocker = Setup.Hero(2, x: -4, alive: false);
            Assert.That(HeroCollision.Move(moving, new float2(-2, 0), new[] { blocker }).x, Is.EqualTo(-2));
        }

        [Test]
        public void Simulation_TwoPlayersMovingTowardEachOtherNeverOverlap()
        {
            var simulation = Setup.MatchSimulation(new[] { Setup.Hero(x: -8), Setup.Hero(2, x: -4) });
            simulation.Enqueue(new MoveIntent(1, new float2(-4, 0)));
            simulation.Enqueue(new MoveIntent(2, new float2(-8, 0)));
            for (int i = 0; i < 100; i++)
            {
                simulation.Step(Setup.TickDeltaTime);
                Assert.That(math.distance(simulation.GetHero(0).Pos, simulation.GetHero(1).Pos), Is.GreaterThanOrEqualTo(0.9999));
            }
            Assert.That(simulation.GetHero(0).Pos.x, Is.LessThan(simulation.GetHero(1).Pos.x));
        }

        [Test]
        public void CoincidentSpawn_IsSeparatedWithoutGivingIdlePlayersMovementTargets()
        {
            var simulation = Setup.MatchSimulation(new[] { Setup.Hero(x: -2.25f), Setup.Hero(2, x: -2.25f), Setup.Hero(3, x: -2.25f) });
            simulation.Step(Setup.TickDeltaTime);
            for (int i = 0; i < simulation.HeroCount; i++)
            {
                var hero = simulation.GetHero(i);
                Assert.That(hero.Pos.x, Is.LessThanOrEqualTo(-2.25f));
                Assert.That(hero.MoveTarget, Is.EqualTo(hero.Pos));
                for (int j = i + 1; j < simulation.HeroCount; j++)
                {
                    Assert.That(math.distance(hero.Pos, simulation.GetHero(j).Pos), Is.GreaterThanOrEqualTo(0.9999));
                }
            }
        }

        [Test]
        public void PulledHero_StopsAtOwnerWithoutOverlapping()
        {
            var simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Setup.Advance(simulation, 45);
            var owner = simulation.GetHero(0);
            var victim = simulation.GetHero(1);
            Assert.That(victim.IsPulled, Is.False);
            Assert.That(math.distance(owner.Pos, victim.Pos), Is.GreaterThanOrEqualTo(0.9999));
            Assert.That(victim.Pos.x, Is.LessThan(0));
        }

        [Test]
        public void PulledHero_IsReleasedAtThirdPlayerInsteadOfPassingThrough()
        {
            var simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Setup.Advance(simulation, 11);
            Assert.That(simulation.GetHero(1).IsPulled, Is.True);
            simulation.AddHero(Setup.Hero(3, x: -2.25f));
            Setup.Advance(simulation, 35);
            var victim = simulation.GetHero(1);
            Assert.That(victim.IsPulled, Is.False);
            Assert.That(victim.Pos.x, Is.EqualTo(-1.25f).Within(0.0001));
            Assert.That(math.distance(victim.Pos, simulation.GetHero(2).Pos), Is.GreaterThanOrEqualTo(0.9999));
        }

        [Test]
        public void Prediction_ReplayUsesHistoricalObstacles()
        {
            var predictor = new MovementPredictor();
            predictor.Reset(1, new Game.Core.Geometry.Rect2(new float2(-12, -7), new float2(-2.25f, 7)));
            var obstacles = new[] { Setup.Hero(2, x: -4) };
            predictor.SetCollisionContext(1, 0.5f, obstacles);
            predictor.Receive(new MovementSnapshot
            {
                SessionEpoch = 1,
                ServerTick = 0,
                Pos = new Vector2(-8, 0),
                MoveTarget = new Vector2(-2.25f, 0),
                MoveSpeed = 3.2f,
                Alive = true
            });
            for (int i = 0; i < 60; i++)
            {
                predictor.Step(out _);
            }

            Vector2 before = predictor.Position;
            obstacles[0].Pos = new float2(-10, 0);
            predictor.Receive(new MovementSnapshot
            {
                SessionEpoch = 1,
                ServerTick = 1,
                AckSequence = 1,
                Pos = new Vector2(-8 + 3.2f / 30, 0),
                MoveTarget = new Vector2(-2.25f, 0),
                MoveSpeed = 3.2f,
                Alive = true
            });
            Assert.That(before.x, Is.EqualTo(-5).Within(0.0001));
            Assert.That(Vector2.Distance(before, predictor.Position), Is.LessThan(0.0001));
        }
    }
}
