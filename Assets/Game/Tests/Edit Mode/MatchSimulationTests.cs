using Game.Core.Commands;
using Game.Core.Hero;
using Game.Core.Hook;
using Game.Core.Match;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class MatchSimulationTests
    {
        [Test]
        public void AddHero_EmptySimulation_CanAdvanceAndRegisterHero()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation(System.Array.Empty<HeroState>());

            // Act.
            simulation.Step(Setup.TickDeltaTime);
            simulation.AddHero(Setup.Hero());

            // Assert.
            Assert.That(simulation.HeroCount, Is.EqualTo(1));
            Assert.That(simulation.GetHook(0).OwnerId, Is.EqualTo(1));
        }

        [Test]
        public void AddHero_ActiveHook_PreservesExistingHookAndTime()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            simulation.Step(Setup.TickDeltaTime);
            HookState hook = simulation.GetHook(0);
            double time = simulation.Time;

            // Act.
            simulation.AddHero(Setup.Hero(id: 3, y: 3));

            // Assert.
            Assert.That(simulation.GetHook(0), Is.EqualTo(hook));
            Assert.That(simulation.Time, Is.EqualTo(time));
            Assert.Throws<System.ArgumentException>(() => simulation.AddHero(Setup.Hero(id: 3)));
        }

        [Test]
        public void StopMovement_QueuedMove_DiscardsDisconnectedInput()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new MoveIntent(1, new float2(-3, 0)));

            // Act.
            simulation.StopMovement(1);
            simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(simulation.GetHero(0).Pos, Is.EqualTo(new float2(-4, 0)));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RemoveHero_ActivePull_ReleasesRelationship(bool removeOwner)
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            Setup.Advance(simulation, ticks: 11);
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Pulling));

            // Act.
            bool removed = simulation.RemoveHero(removeOwner ? 1u : 2u);

            // Assert.
            Assert.That(removed, Is.True);
            Assert.That(simulation.HeroCount, Is.EqualTo(1));
            Assert.That(simulation.GetHero(0).IsPulled, Is.False);
            Assert.That(simulation.GetHook(0).Phase,
                Is.EqualTo(removeOwner ? HookPhase.Idle : HookPhase.Retracting));
            Assert.DoesNotThrow(() => Setup.Advance(simulation, ticks: 60));
        }

        [Test]
        public void Step_ExplicitTime_UsesTimestampAndFixedMovementDelta()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new MoveIntent(1, new float2(-3, 0)));
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));

            // Act.
            simulation.Step(Setup.TickDeltaTime, 100);

            // Assert.
            Assert.That(simulation.GetHook(0).StartTime, Is.EqualTo(100));
            Assert.That(simulation.Time, Is.EqualTo(100 + (double)Setup.TickDeltaTime));
            Assert.That(simulation.GetHero(0).Pos.x,
                Is.EqualTo(-4 + Setup.HeroConfig().MoveSpeed * Setup.TickDeltaTime).Within(0.00001));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => simulation.Step(Setup.TickDeltaTime, 99));
        }

        [Test]
        public void Step_MoveTargetIsReachable_HeroStopsAtTarget()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            float2 target = new(-3f, 1f);

            // Act.
            simulation.Enqueue(new MoveIntent(1, target));
            Setup.Advance(simulation, ticks: 60);

            // Assert.
            Assert.That(simulation.GetHero(0).Pos, Is.EqualTo(target));
        }

        [Test]
        public void Step_MoveTargetIsOutsideOwnHalf_TargetIsClamped()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();

            // Act.
            simulation.Enqueue(new MoveIntent(1, new float2(100f, 100f)));
            Setup.Advance(simulation, ticks: 300);

            // Assert.
            Assert.That(simulation.GetHero(0).Pos, Is.EqualTo(new float2(-2.25f, 6.5f)));
        }

        [Test]
        public void Step_HookHitsEnemy_EnemyLosesHpAndIsAttachedToTip()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 11);

            // Assert.
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Pulling));
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(825));
            Assert.That(simulation.GetHero(1).IsPulled, Is.True);
            Assert.That(simulation.GetHero(1).Pos, Is.EqualTo(simulation.GetHook(0).Tip));
            Assert.That(simulation.Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void Step_TargetIsPulled_MoveAndThrowIntentsAreIgnored()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 11);

            // Act.
            simulation.Enqueue(new MoveIntent(2, new float2(10f, 6f)));
            simulation.Enqueue(new ThrowHookIntent(2, new float2(-1f, 0f), 0));
            simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(simulation.GetHook(1).Phase, Is.EqualTo(HookPhase.Idle));
            Assert.That(simulation.GetHero(1).Pos, Is.EqualTo(simulation.GetHook(0).Tip));
        }

        [Test]
        public void Step_PullHasFinished_TargetIsReleasedAtOwner()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 40);

            // Assert.
            HeroState target = simulation.GetHero(1);
            Assert.That(target.IsPulled, Is.False);
            Assert.That(target.Pos, Is.EqualTo(simulation.GetHero(0).Pos));
            Assert.That(target.MoveTarget, Is.EqualTo(target.Pos));
        }

        [Test]
        public void Step_ReleasedTargetHasNoNewMoveIntent_TargetStaysAtReleasePosition()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 40);
            float2 releasePosition = simulation.GetHero(1).Pos;

            // Act.
            Setup.Advance(simulation, ticks: 60);

            // Assert.
            Assert.That(simulation.GetHero(1).Pos, Is.EqualTo(releasePosition));
        }

        [Test]
        public void Step_ReleasedTargetReceivesMoveIntent_TargetMovesAtNormalSpeed()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 40);
            float2 releasePosition = simulation.GetHero(1).Pos;

            // Act.
            simulation.Enqueue(new MoveIntent(2, new float2(4f, 0f)));
            simulation.Step(Setup.TickDeltaTime);

            // Assert.
            float distance = math.distance(simulation.GetHero(1).Pos, releasePosition);
            Assert.That(distance, Is.EqualTo(Setup.HeroConfig().MoveSpeed * Setup.TickDeltaTime).Within(1e-5f));
            Assert.That(simulation.GetHero(1).Pos.x, Is.LessThan(0f));
        }

        [Test]
        public void Step_ReleasedTargetMovesBackToOwnHalf_TargetReachesDestination()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 40);
            float2 target = new(4f, 0f);

            // Act.
            simulation.Enqueue(new MoveIntent(2, target));
            Setup.Advance(simulation, ticks: 101);

            // Assert.
            Assert.That(simulation.GetHero(1).Pos, Is.EqualTo(target));
        }

        [Test]
        public void Step_OwnerMovesDuringPull_HookReturnsAndStartsCooldown()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 11);

            // Act.
            simulation.Enqueue(new MoveIntent(1, new float2(-7f, 1f)));
            Setup.Advance(simulation, ticks: 30);

            // Assert.
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
            Assert.That(simulation.GetHero(1).IsPulled, Is.False);
            Assert.That(simulation.GetHero(0).HookCooldownEnd, Is.GreaterThan(simulation.Time));
        }

        [Test]
        public void Step_ThrowRequestedDuringCooldown_HookRemainsIdle()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 11);
            simulation.Enqueue(new MoveIntent(1, new float2(-7f, 1f)));
            Setup.Advance(simulation, ticks: 30);

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void Step_ThrowRequestedAfterCooldown_HookStartsExtending()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 11);
            simulation.Enqueue(new MoveIntent(1, new float2(-7f, 1f)));
            Setup.Advance(simulation, ticks: 30);
            Setup.Advance(simulation, ticks: 100);

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Extending));
        }

        [Test]
        public void Step_HookMissesAndReachesRange_HookStartsRetracting()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(0f, 1f), 0));
            Setup.Advance(simulation, ticks: 19);

            // Assert.
            Assert.That(simulation.GetHook(0).Length, Is.EqualTo(12f));
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Retracting));
        }

        [Test]
        public void Step_EmptyHookHasRetracted_HookIsIdle()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();
            simulation.Enqueue(new ThrowHookIntent(1, new float2(0f, 1f), 0));
            Setup.Advance(simulation, ticks: 19);

            // Act.
            Setup.Advance(simulation, ticks: 30);

            // Assert.
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void Step_HookHitsAlly_AllyIsPulledWithoutDamage()
        {
            // Arrange.
            HeroState[] heroes = { Setup.Hero(x: -8f), Setup.Hero(id: 2, x: -4f) };
            MatchSimulation simulation = Setup.MatchSimulation(heroes);

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 5);

            // Assert.
            Assert.That(simulation.GetHero(1).IsPulled, Is.True);
            Assert.That(simulation.GetHero(1).Hp, Is.EqualTo(1000));
        }

        [Test]
        public void Step_FastHookCrossesTwoTargets_NearestTargetIsSelected()
        {
            // Arrange.
            HeroState[] heroes =
            {
                Setup.Hero(),
                Setup.Hero(id: 2, team: Team.B, x: 8f),
                Setup.Hero(id: 3, team: Team.B, x: 4f)
            };
            MatchSimulation simulation = Setup.MatchSimulation(heroes, hookSpeed: 600f);

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(simulation.GetHook(0).TargetId, Is.EqualTo(3));
        }

        [Test]
        public void Step_HitDamageExceedsHp_DeathIsEmittedAndBodyRemainsPulled()
        {
            // Arrange.
            HeroState[] heroes = { Setup.Hero(), Setup.Hero(id: 2, team: Team.B, x: 4f, hp: 100) };
            MatchSimulation simulation = Setup.MatchSimulation(heroes);

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            Setup.Advance(simulation, ticks: 11);

            // Assert.
            Assert.That(simulation.GetHero(1).Alive, Is.False);
            Assert.That(simulation.GetHero(1).Hp, Is.Zero);
            Assert.That(simulation.GetHero(1).IsPulled, Is.True);
            Assert.That(simulation.Deaths.Count, Is.EqualTo(1));
        }

        [Test]
        public void Step_OpposingHookTipsMeet_BothHooksRetractAndClashIsEmitted()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();

            // Act.
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
            simulation.Enqueue(new ThrowHookIntent(2, new float2(-1f, 0f), 0));
            Setup.Advance(simulation, ticks: 6);

            // Assert.
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Retracting));
            Assert.That(simulation.GetHook(1).Phase, Is.EqualTo(HookPhase.Retracting));
            Assert.That(simulation.Clashes.Count, Is.EqualTo(1));
        }

        [Test]
        public void Step_InvalidIntentsAreQueued_HeroDoesNotMoveOrThrow()
        {
            // Arrange.
            MatchSimulation simulation = Setup.MatchSimulation();

            // Act.
            simulation.Enqueue(new MoveIntent(999, float2.zero));
            simulation.Enqueue(new MoveIntent(1, new float2(float.NaN)));
            simulation.Enqueue(new ThrowHookIntent(1, float2.zero, 0));
            simulation.Enqueue(new ThrowHookIntent(1, new float2(float.PositiveInfinity), 0));
            simulation.Step(Setup.TickDeltaTime);

            // Assert.
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
            Assert.That(simulation.GetHero(0).Pos, Is.EqualTo(new float2(-4f, 0f)));
        }

        [Test]
        public void Step_IdenticalIntentSequences_StatesMatchForEveryTick()
        {
            // Arrange.
            MatchSimulation first = Setup.MatchSimulation();
            MatchSimulation second = Setup.MatchSimulation();
            HeroState[] firstHeroes = new HeroState[300 * first.HeroCount];
            HeroState[] secondHeroes = new HeroState[firstHeroes.Length];
            HookState[] firstHooks = new HookState[firstHeroes.Length];
            HookState[] secondHooks = new HookState[firstHeroes.Length];

            // Act.
            for (int tick = 0; tick < 300; tick++)
            {
                if (tick % 80 == 0)
                {
                    float2 destination = new(-4f, (tick % 3) - 1);
                    first.Enqueue(new MoveIntent(1, destination));
                    second.Enqueue(new MoveIntent(1, destination));
                    first.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
                    second.Enqueue(new ThrowHookIntent(1, new float2(1f, 0f), 0));
                }

                first.Step(Setup.TickDeltaTime);
                second.Step(Setup.TickDeltaTime);
                for (int hero = 0; hero < first.HeroCount; hero++)
                {
                    int index = tick * first.HeroCount + hero;
                    firstHeroes[index] = first.GetHero(hero);
                    secondHeroes[index] = second.GetHero(hero);
                    firstHooks[index] = first.GetHook(hero);
                    secondHooks[index] = second.GetHook(hero);
                }
            }

            // Assert.
            Assert.That(firstHeroes, Is.EqualTo(secondHeroes));
            Assert.That(firstHooks, Is.EqualTo(secondHooks));
        }
    }
}
