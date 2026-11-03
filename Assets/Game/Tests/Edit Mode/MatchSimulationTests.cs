using System;
using Game.Core.Commands;
using Game.Core.Configs;
using Game.Core.Geometry;
using Game.Core.Hero;
using Game.Core.Hook;
using Game.Core.Match;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class MatchSimulationTests
    {
        private const float Dt = 1f / 30f;
        private static readonly ArenaConfigData Arena = new(new float2(24, 14), 3.5f,
            Array.Empty<float2>(), Array.Empty<float2>());
        private static readonly HeroConfigData Hero = new(1000, 3.2f, 0.5f, 4);
        private static readonly HookConfigData Hook = new(12, 20, 0.35f, 175, 3, 1.5f, 4, 0.08f, 75, 4);
        private static HeroState Player(uint id, Team team, float x, float y = 0) => new()
        { Id = id, Team = team, Pos = new float2(x, y), Radius = 0.5f, Hp = 1000, Alive = true };
        private static MatchSimulation Create() => new(Arena, Hero, Hook,
            new[] { Player(1, Team.A, -4), Player(2, Team.B, 4) });
        private static void Tick(MatchSimulation simulation, int count)
        { for (int i = 0; i < count; i++) simulation.Step(Dt); }

        [Test]
        public void ReleasedTarget_StaysAtOwnerUntilNewMoveCommand()
        {
            var sim = Create();
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Tick(sim, 40);
            var released = sim.GetHero(1);
            Assert.That(released.IsPulled, Is.False);
            Assert.That(released.Pos, Is.EqualTo(sim.GetHero(0).Pos));
            Assert.That(released.MoveTarget, Is.EqualTo(released.Pos));
            Tick(sim, 60);
            Assert.That(sim.GetHero(1).Pos, Is.EqualTo(released.Pos));

            sim.Enqueue(new MoveIntent(2, new float2(4, 0)));
            sim.Step(Dt);
            Assert.That(math.distance(sim.GetHero(1).Pos, released.Pos),
                Is.EqualTo(Hero.MoveSpeed * Dt).Within(1e-5f));
            Assert.That(sim.GetHero(1).Pos.x, Is.LessThan(0));
            Tick(sim, 100);
            Assert.That(sim.GetHero(1).Pos, Is.EqualTo(new float2(4, 0)));
        }

        [Test]
        public void Movement_StopsAtTargetWithoutOvershoot()
        {
            var sim = Create();
            sim.Enqueue(new MoveIntent(1, new float2(-3, 1)));
            Tick(sim, 60);
            Assert.That(sim.GetHero(0).Pos, Is.EqualTo(new float2(-3, 1)));
        }

        [Test]
        public void Movement_ClampsToOwnHalf()
        {
            var sim = Create();
            sim.Enqueue(new MoveIntent(1, new float2(100, 100)));
            Tick(sim, 300);
            Assert.That(sim.GetHero(0).Pos, Is.EqualTo(new float2(-2.25f, 6.5f)));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void Movement_DeadOrPulledHeroStaysStill(bool alive, bool pulled)
        {
            var hero = Player(1, Team.A, -4);
            hero.Alive = alive; hero.IsPulled = pulled; hero.MoveTarget = new float2(-3, 0);
            HeroMovement.Step(ref hero, 3.2f, ArenaBounds.ForTeam(Arena, Team.A, 0.5f), Dt);
            Assert.That(hero.Pos, Is.EqualTo(new float2(-4, 0)));
        }

        [Test]
        public void Hit_DamagesEnemyAndPullsAtTip()
        {
            var sim = Create();
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Tick(sim, 11);
            Assert.That(sim.GetHook(0).Phase, Is.EqualTo(HookPhase.Pulling));
            Assert.That(sim.GetHero(1).Hp, Is.EqualTo(825));
            Assert.That(sim.GetHero(1).IsPulled, Is.True);
            Assert.That(sim.GetHero(1).Pos, Is.EqualTo(sim.GetHook(0).Tip));
            Assert.That(sim.Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void Pulling_IgnoresMoveAndThrow()
        {
            var sim = Create();
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Tick(sim, 11);
            sim.Enqueue(new MoveIntent(2, new float2(10, 6)));
            sim.Enqueue(new ThrowHookIntent(2, new float2(-1, 0), 0));
            sim.Step(Dt);
            Assert.That(sim.GetHook(1).Phase, Is.EqualTo(HookPhase.Idle));
            Assert.That(sim.GetHero(1).Pos, Is.EqualTo(sim.GetHook(0).Tip));
        }

        [Test]
        public void Return_UsesMovingOwnerAndStartsCooldownOnCompletion()
        {
            var sim = Create();
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Tick(sim, 11);
            sim.Enqueue(new MoveIntent(1, new float2(-7, 1)));
            Tick(sim, 30);
            Assert.That(sim.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
            Assert.That(sim.GetHero(1).IsPulled, Is.False);
            Assert.That(sim.GetHero(0).HookCooldownEnd, Is.GreaterThan(sim.Time));
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            sim.Step(Dt);
            Assert.That(sim.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
            Tick(sim, 100);
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            sim.Step(Dt);
            Assert.That(sim.GetHook(0).Phase, Is.EqualTo(HookPhase.Extending));
        }

        [Test]
        public void Miss_ReachesRangeAndRetracts()
        {
            var sim = Create();
            sim.Enqueue(new ThrowHookIntent(1, new float2(0, 1), 0));
            Tick(sim, 19);
            Assert.That(sim.GetHook(0).Length, Is.EqualTo(12));
            Assert.That(sim.GetHook(0).Phase, Is.EqualTo(HookPhase.Retracting));
            Tick(sim, 30);
            Assert.That(sim.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void FriendlyHit_PullsWithoutDamage()
        {
            var sim = new MatchSimulation(Arena, Hero, Hook,
                new[] { Player(1, Team.A, -8), Player(2, Team.A, -4) });
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Tick(sim, 5);
            Assert.That(sim.GetHero(1).IsPulled, Is.True);
            Assert.That(sim.GetHero(1).Hp, Is.EqualTo(1000));
        }

        [Test]
        public void Sweep_SelectsNearestTarget()
        {
            var fast = new HookConfigData(12, 600, 0.35f, 175, 3, 0, 0, 0, 0, 4);
            var sim = new MatchSimulation(Arena, Hero, fast,
                new[] { Player(1, Team.A, -4), Player(2, Team.B, 8), Player(3, Team.B, 4) });
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            sim.Step(Dt);
            Assert.That(sim.GetHook(0).TargetId, Is.EqualTo(3));
        }

        [Test]
        public void LethalHit_EmitsDeathAndKeepsPullingBody()
        {
            var target = Player(2, Team.B, 4); target.Hp = 100;
            var sim = new MatchSimulation(Arena, Hero, Hook, new[] { Player(1, Team.A, -4), target });
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Tick(sim, 11);
            Assert.That(sim.GetHero(1).Alive, Is.False);
            Assert.That(sim.GetHero(1).Hp, Is.Zero);
            Assert.That(sim.GetHero(1).IsPulled, Is.True);
            Assert.That(sim.Deaths.Count, Is.EqualTo(1));
        }

        [Test]
        public void OpposingHooks_ClashAndRetract()
        {
            var sim = Create();
            sim.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            sim.Enqueue(new ThrowHookIntent(2, new float2(-1, 0), 0));
            Tick(sim, 6);
            Assert.That(sim.GetHook(0).Phase, Is.EqualTo(HookPhase.Retracting));
            Assert.That(sim.GetHook(1).Phase, Is.EqualTo(HookPhase.Retracting));
            Assert.That(sim.Clashes.Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidCommands_AreIgnored()
        {
            var sim = Create();
            sim.Enqueue(new MoveIntent(999, float2.zero));
            sim.Enqueue(new MoveIntent(1, new float2(float.NaN)));
            sim.Enqueue(new ThrowHookIntent(1, float2.zero, 0));
            sim.Enqueue(new ThrowHookIntent(1, new float2(float.PositiveInfinity), 0));
            sim.Step(Dt);
            Assert.That(sim.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
            Assert.That(sim.GetHero(0).Pos, Is.EqualTo(new float2(-4, 0)));
        }

        [Test]
        public void SameIntents_ProduceSameStateAfter300Ticks()
        {
            var a = Create(); var b = Create();
            for (int i = 0; i < 300; i++)
            {
                if (i % 80 == 0)
                {
                    a.Enqueue(new MoveIntent(1, new float2(-4, (i % 3) - 1)));
                    b.Enqueue(new MoveIntent(1, new float2(-4, (i % 3) - 1)));
                    a.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
                    b.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
                }
                a.Step(Dt); b.Step(Dt);
                for (int h = 0; h < a.HeroCount; h++)
                {
                    Assert.That(a.GetHero(h), Is.EqualTo(b.GetHero(h)));
                    Assert.That(a.GetHook(h), Is.EqualTo(b.GetHook(h)));
                }
            }
        }
    }
}
