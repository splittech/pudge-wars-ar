using Game.Core.Match;
using Game.Core.Commands;
using Game.Core.Hero;
using Game.Core.Hook;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class HookBounceTests
    {
        [TestCase(0, 1)]
        [TestCase(0, -1)]
        [TestCase(-1, 0)]
        public void Wall_ReflectsAndConsumesRemainingStep(float x, float y)
        {
            var simulation = Setup.MatchSimulation(new[] { Setup.Hero() });
            float2 direction = new(x, y);
            simulation.Enqueue(new ThrowHookIntent(1, direction, 0));
            Setup.Advance(simulation, 13);
            var hook = simulation.GetHook(0);
            Assert.That(hook.Phase, Is.EqualTo(HookPhase.Extending));
            Assert.That(math.dot(hook.Dir, direction), Is.EqualTo(-1).Within(0.0001));
            Assert.That(hook.Speed, Is.EqualTo(20).Within(0.0001));
            Assert.That(hook.Travelled, Is.EqualTo(13 * Setup.TickDeltaTime * 20).Within(0.0001));
            Assert.That(math.all(math.abs(hook.Tip) <= hook.HalfBounds + 0.0001f), Is.True);
        }

        [Test]
        public void Corner_ReflectsBothAxesWithoutResettingRange()
        {
            var simulation = Setup.MatchSimulation(new[] { Setup.Hero(team: Team.B, x: 4) });
            simulation.Enqueue(new ThrowHookIntent(1, new float2(7.65f, 6.65f), 0));
            Setup.Advance(simulation, 17);
            var hook = simulation.GetHook(0);
            Assert.That(hook.Dir.x, Is.LessThan(0));
            Assert.That(hook.Dir.y, Is.LessThan(0));
            Setup.Advance(simulation, 2);
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Retracting));
            Assert.That(simulation.GetHook(0).Travelled, Is.EqualTo(12).Within(0.0001));
        }

        [Test]
        public void FastHeadOnClash_IsSweptAndDoesNotRepeatForSeparatingPair()
        {
            var simulation = Setup.MatchSimulation(new[] { Setup.Hero(), Setup.Hero(2, Team.B, 4) }, 300);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), 0));
            simulation.Enqueue(new ThrowHookIntent(2, new float2(-1, 0), 0));
            simulation.Step(Setup.TickDeltaTime);
            Assert.That(simulation.Clashes.Count, Is.EqualTo(1));
            Assert.That(simulation.GetHook(0).Dir.x, Is.EqualTo(-1));
            Assert.That(simulation.GetHook(1).Dir.x, Is.EqualTo(1));
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Extending));
            Assert.That(simulation.Hits.Count, Is.Zero);
            simulation.Step(Setup.TickDeltaTime);
            Assert.That(simulation.Clashes.Count, Is.Zero);
        }

        [Test]
        public void LateSnapshot_AfterWallBounceMatchesServerAndClampsExtrapolation()
        {
            var simulation = Setup.MatchSimulation(new[] { Setup.Hero() });
            simulation.Enqueue(new ThrowHookIntent(1, new float2(0, 1), 0));
            Setup.Advance(simulation, 13);
            var hook = simulation.GetHook(0);
            var state = Game.Net.HookNetState.FromSimulation(hook, 0);
            var tip = state.EvaluateTip(simulation.Time, UnityEngine.Vector2.zero, hook.Speed, hook.Range);
            Assert.That(math.distance(new float2(tip.x, tip.y), hook.Tip), Is.LessThan(0.0001));
            tip = state.EvaluateTip(simulation.Time + 10, UnityEngine.Vector2.zero, hook.Speed, hook.Range);
            Assert.That(math.all(math.abs(new float2(tip.x, tip.y)) <= hook.HalfBounds), Is.True);
        }

        [Test]
        public void ObliqueClash_ConservesMomentumAndEnergy()
        {
            var simulation = Setup.MatchSimulation();
            float2 d1 = new(1, 0);
            float2 d2 = math.normalize(new float2(-1, 0.1f));
            simulation.Enqueue(new ThrowHookIntent(1, d1, 0));
            simulation.Enqueue(new ThrowHookIntent(2, d2, 0));
            bool collided = false;
            for (int i = 0; i < 10; i++)
            {
                simulation.Step(Setup.TickDeltaTime);
                if (simulation.Clashes.Count == 0) continue;
                var first = simulation.GetHook(0);
                var second = simulation.GetHook(1);
                float2 v1 = first.Dir * first.Speed, v2 = second.Dir * second.Speed;
                Assert.That(math.distance(v1 + v2, (d1 + d2) * 20), Is.LessThan(0.001));
                Assert.That(math.lengthsq(v1) + math.lengthsq(v2), Is.EqualTo(800).Within(0.01));
                collided = true;
                break;
            }
            Assert.That(collided, Is.True);
        }
    }
}
