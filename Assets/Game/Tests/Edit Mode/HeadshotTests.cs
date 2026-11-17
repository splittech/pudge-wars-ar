using Game.Core.Commands;
using Game.Core.Hook;
using Game.Core.Match;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class HeadshotTests
    {
        private static MatchSimulation CarryingVictim()
        {
            var simulation = Setup.MatchWithRules(territoryDamage: 0);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), simulation.Time));
            Setup.Advance(simulation, 11);
            Assert.That(simulation.GetHero(1).IsPulled, Is.True);
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(825));
            return simulation;
        }

        [Test]
        public void EnemyAlreadyOnHook_IsKilledInstantlyWithNormalRewardsAndAssist()
        {
            var simulation = CarryingVictim();
            simulation.AddHero(Setup.Hero(3, Team.A, -4, 3));
            simulation.Enqueue(new ThrowHookIntent(3, new float2(3.5f, -3), simulation.Time));
            bool killed = false;
            for (int tick = 0; tick < 20; tick++)
            {
                simulation.Step(Setup.TickDeltaTime);
                if (simulation.Deaths.Count == 0)
                {
                    continue;
                }

                Assert.That(simulation.Deaths.Count, Is.EqualTo(1));
                Assert.That(simulation.Deaths[0].KillerId, Is.EqualTo(3));
                Assert.That(simulation.Damage[0].Damage, Is.EqualTo(825));
                Assert.That(simulation.GetHero(1).Hp, Is.Zero);
                Assert.That(simulation.GetHero(1).IsPulled, Is.False);
                Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Retracting));
                Assert.That(simulation.GetHook(2).Phase, Is.EqualTo(HookPhase.Retracting));
                Assert.That(simulation.GetHook(0).TargetId, Is.Zero);
                Assert.That(simulation.GetHook(2).TargetId, Is.Zero);
                Assert.That(simulation.GetHero(2).Kills, Is.EqualTo(1));
                Assert.That(simulation.GetHero(2).Gold, Is.EqualTo(150));
                Assert.That(simulation.GetHero(0).Assists, Is.EqualTo(1));
                Assert.That(simulation.GetHero(0).Gold, Is.EqualTo(50));
                Assert.That(simulation.Rules.ScoreA, Is.EqualTo(1));
                killed = true;
                break;
            }
            Assert.That(killed, Is.True);
            simulation.Step(Setup.TickDeltaTime);
            Assert.That(simulation.Deaths.Count, Is.Zero);
            Assert.That(simulation.GetHero(1).Deaths, Is.EqualTo(1));
        }

        [Test]
        public void Headshot_AlsoWorksWithoutMatchRulesAndDoesNotLeaveBodyAttached()
        {
            var simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Setup.Advance(simulation, 11);
            simulation.AddHero(Setup.Hero(3, Team.A, -4, 3));
            simulation.Enqueue(new ThrowHookIntent(3, new float2(3.5f, -3), simulation.Time));
            for (int tick = 0; tick < 20 && simulation.GetHero(1).Alive; tick++)
            {
                simulation.Step(Setup.TickDeltaTime);
            }

            Assert.That(simulation.GetHero(1).Hp, Is.Zero);
            Assert.That(simulation.GetHero(1).IsPulled, Is.False);
            Assert.That(simulation.GetHook(1).Phase, Is.EqualTo(HookPhase.Idle));
            Assert.That(simulation.GetHook(0).TargetId, Is.Zero);
            Assert.That(simulation.GetHook(2).TargetId, Is.Zero);
        }

        [Test]
        public void AlliedInterception_TransfersPassengerWithoutDamageOrDuplicateOwnership()
        {
            var simulation = CarryingVictim();
            simulation.AddHero(Setup.Hero(3, Team.B, 2.25f, 3));
            // Aim ahead of the moving passenger, across its current carrier's path.
            simulation.Enqueue(new ThrowHookIntent(3, new float2(-3, -4), simulation.Time));
            bool rescued = false;
            for (int tick = 0; tick < 20; tick++)
            {
                simulation.Step(Setup.TickDeltaTime);
                if (simulation.GetHook(2).Phase != HookPhase.Pulling)
                {
                    continue;
                }

                Assert.That(simulation.GetHook(2).TargetId, Is.EqualTo(2));
                Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Retracting));
                Assert.That(simulation.GetHook(0).TargetId, Is.Zero);
                Assert.That(simulation.GetHero(1).Alive, Is.True);
                Assert.That(simulation.GetHero(1).IsPulled, Is.True);
                Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(825));
                Assert.That(simulation.Damage.Count, Is.Zero);
                Assert.That(simulation.Deaths.Count, Is.Zero);
                Assert.That(simulation.Rules.ScoreA, Is.Zero);
                rescued = true;
                break;
            }
            Assert.That(rescued, Is.True);
            Setup.Advance(simulation, 45);
            Assert.That(simulation.GetHero(1).IsPulled, Is.False);
            Assert.That(simulation.GetHero(1).Pos.x, Is.GreaterThan(0));
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(825));
        }
    }
}
