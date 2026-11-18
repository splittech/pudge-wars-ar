using System;
using System.IO;
using Game.Core.Commands;
using Game.Core.Hook;
using Game.Net;
using Game.Presentation;
using Mirror;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class HookPathTests
    {
        [Test]
        public void Return_CrossesCornerUsingRemainingDistance()
        {
            var hook = BentHook();
            var owner = Setup.Hero(x: 0);
            owner.Radius = 0;
            bool complete = HookSimulation.Return(ref hook, owner, Setup.HookConfig(speed: 2), 2);

            Assert.That(complete, Is.False);
            Assert.That(math.distance(hook.Tip, new float2(3, 0)), Is.LessThan(0.0001f));
            Assert.That(hook.ReturnIndex, Is.Zero);
        }

        [Test]
        public void Return_MovingOwner_FollowsOriginalPathBeforeChasingOwner()
        {
            var hook = BentHook();
            var owner = Setup.Hero(x: -2);
            owner.Radius = 0;
            HookSimulation.Return(ref hook, owner, Setup.HookConfig(speed: 1), 7);
            Assert.That(math.length(hook.Tip), Is.LessThan(0.0001f));
            Assert.That(hook.ReturnIndex, Is.EqualTo(-1));
            Assert.That(HookSimulation.Return(ref hook, owner, Setup.HookConfig(speed: 1), 2), Is.True);
            Assert.That(math.distance(hook.Tip, owner.Pos), Is.LessThan(0.0001f));
        }

        [Test]
        public void Simulation_AfterWallBounce_RetracesBounceBeforeReachingOwner()
        {
            var simulation = Setup.MatchSimulation(new[] { Setup.Hero() });
            simulation.Enqueue(new ThrowHookIntent(1, new float2(0, 1), 0));
            for (int i = 0; i < 30 && simulation.GetHook(0).Phase != HookPhase.Retracting; i++)
            {
                simulation.Step(Setup.TickDeltaTime);
            }

            var before = simulation.GetHook(0);
            Assert.That(before.Phase, Is.EqualTo(HookPhase.Retracting));
            Assert.That(before.Path.Count, Is.EqualTo(2));
            simulation.Step(Setup.TickDeltaTime);
            var after = simulation.GetHook(0);
            Assert.That(after.Tip.y, Is.GreaterThan(before.Tip.y));
            Assert.That(before.Path.Get(1).y, Is.EqualTo(before.HalfBounds.y).Within(0.0001));
        }

        [Test]
        public void Path_IsValueSnapshotAndRejectsOverflow()
        {
            var path = new HookPath();
            path.TryAdd(float2.zero);
            HookPath previous = path;
            for (int i = 1; i < HookPath.Capacity; i++)
            {
                Assert.That(path.TryAdd(new float2(i, 0)), Is.True);
            }

            Assert.That(previous.Count, Is.EqualTo(1));
            Assert.That(path.TryAdd(new float2(100, 0)), Is.False);
            Assert.That(path.Count, Is.EqualTo(HookPath.Capacity));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Pulling_CrossesCorner_ValidatesEachLegAndReleasesAtContact(bool blockerAlive)
        {
            var simulation = Setup.MatchSimulation(new[]
            {
                Setup.Hero(x: -8, y: -3),
                Setup.Hero(2, x: -4, y: 3, isPulled: true),
                Setup.Hero(3, x: -4, y: 0, alive: blockerAlive)
            });
            var path = new HookPath();
            path.TryAdd(new float2(-8, -3));
            path.TryAdd(new float2(-4, -3));
            var hooks = (HookState[])typeof(Game.Core.Match.MatchSimulation)
                .GetField("_hooks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(simulation);
            hooks[0] = new HookState
            {
                OwnerId = 1,
                TargetId = 2,
                Phase = HookPhase.Pulling,
                Tip = new float2(-4, 3),
                Dir = new float2(0, 1),
                Speed = 300,
                Range = 50,
                Radius = 0.35f,
                Path = path,
                ReturnIndex = 1
            };

            simulation.Step(Setup.TickDeltaTime);
            var victim = simulation.GetHero(1);
            Assert.That(victim.IsPulled, Is.False);
            if (blockerAlive)
            {
                Assert.That(victim.Pos.x, Is.EqualTo(-4).Within(0.0001));
                Assert.That(victim.Pos.y, Is.EqualTo(1).Within(0.0001));
            }
            else
            {
                Assert.That(victim.Pos.x, Is.EqualTo(-7).Within(0.0001));
                Assert.That(victim.Pos.y, Is.EqualTo(-3).Within(0.0001));
            }
        }

        [Test]
        public void LateReturnSnapshot_PreservesPathAndRemovesConsumedCorners()
        {
            var hook = BentHook();
            hook.SampleTime = 100;
            var state = HookNetState.FromSimulation(hook, 0);
            Vector2 tip = state.EvaluateTip(102, Vector2.zero, 2, 12);
            Assert.That(Vector2.Distance(tip, new Vector2(3, 0)), Is.LessThan(0.0001f));
            Assert.That(state.RemainingPathCount(102, Vector2.zero, 2), Is.EqualTo(1));
            Assert.That(state.EvaluateTip(110, new Vector2(-2, 0), 2, 12), Is.EqualTo(new Vector2(-2, 0)));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(HookPath.Capacity)]
        public void Serialize_Path_RoundTripsBoundedPayload(int count)
        {
            var state = new HookNetState { Phase = HookPhase.Extending, Dir = Vector2.right };
            for (int i = 0; i < count; i++)
            {
                state.Path.TryAdd(new float2(i, i * 0.5f));
            }

            using (var writer = NetworkWriterPool.Get())
            {
                writer.WriteHookNetState(state);
                Assert.That(writer.Position, Is.EqualTo(62 + count * 8));
                using (var reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                {
                    HookNetState actual = reader.ReadHookNetState();
                    Assert.That(actual.Path.Count, Is.EqualTo(count));
                    for (int i = 0; i < count; i++)
                    {
                        Assert.That(actual.Path.Get(i), Is.EqualTo(state.Path.Get(i)));
                    }

                    Assert.That(reader.Remaining, Is.Zero);
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Deserialize_InvalidPath_RejectsPayload(bool invalidCoordinate)
        {
            var state = new HookNetState { Phase = HookPhase.Extending, Dir = Vector2.right };
            state.Path.TryAdd(float2.zero);
            using (var writer = NetworkWriterPool.Get())
            {
                writer.WriteHookNetState(state);
                byte[] bytes = writer.ToArray();
                if (invalidCoordinate)
                {
                    Array.Copy(BitConverter.GetBytes(float.NaN), 0, bytes, 62, 4);
                }
                else
                {
                    bytes[61] = HookPath.Capacity + 1;
                }

                using (var reader = NetworkReaderPool.Get(bytes))
                {
                    Assert.That(() => reader.ReadHookNetState(), Throws.TypeOf<InvalidDataException>());
                }
            }
        }

        [TestCase(0.02f)]
        [TestCase(0.04f)]
        [TestCase(0.08f)]
        [TestCase(1f)]
        public void Render_ScaledRotatedArena_PreservesWidthAndCorners(float scale)
        {
            var arena = new GameObject("Path render test");
            var lineObject = new GameObject("Line");
            try
            {
                arena.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(0, 40, 0));
                arena.transform.localScale = Vector3.one * scale;
                lineObject.transform.SetParent(arena.transform, false);
                var line = lineObject.AddComponent<LineRenderer>();
                ArenaLineRenderer.Initialize(line);
                var hook = BentHook();
                ArenaLineRenderer.Draw(
                    line,
                    arena.transform,
                    Vector2.zero,
                    hook.Path,
                    2,
                    new Vector2(4, 3),
                    0.5f,
                    0.12f);
                Assert.That(line.positionCount, Is.EqualTo(3));
                Assert.That(line.startWidth / scale, Is.EqualTo(0.12f).Within(0.00001f));
                Vector3 corner = arena.transform.TransformPoint(new Vector3(4, 0.5f, 0));
                Assert.That(Vector3.Distance(line.GetPosition(1), corner), Is.LessThan(0.00001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(lineObject);
                UnityEngine.Object.DestroyImmediate(arena);
            }
        }

        private static HookState BentHook()
        {
            var path = new HookPath();
            path.TryAdd(float2.zero);
            path.TryAdd(new float2(4, 0));
            return new HookState
            {
                Phase = HookPhase.Retracting,
                Tip = new float2(4, 3),
                Dir = new float2(0, 1),
                Path = path,
                ReturnIndex = 1
            };
        }
    }
}
