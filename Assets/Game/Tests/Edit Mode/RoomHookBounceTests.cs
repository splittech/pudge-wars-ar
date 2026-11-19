using Game.Core.Commands;
using Game.Core.Configs;
using Game.Core.Geometry;
using Game.Core.Hook;
using Game.Core.Match;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class RoomHookBounceTests
    {
        [Test]
        public void Sweep_ObliqueWall_ReflectsAlongItsNormal()
        {
            var room = new RoomArena(new[]
            {
                new float2(-5, -5),
                new float2(5, -5),
                new float2(5, 5)
            });
            float fraction = room.Sweep(new float2(0, -2), new float2(-4, -2), 0.35f, out float2 normal);
            var velocity = new float2(-4, 0);
            float2 reflected = velocity - 2 * math.dot(velocity, normal) * normal;

            Assert.That(fraction, Is.InRange(0.3f, 0.5f));
            Assert.That(reflected.x, Is.EqualTo(0).Within(0.0001f));
            Assert.That(reflected.y, Is.EqualTo(-4).Within(0.0001f));
        }

        [Test]
        public void Sweep_InsideRoomCorner_CombinesSimultaneousWallNormals()
        {
            var room = new RoomArena(RoomArenaTests.Rectangle(-5, -5, 5, 5));
            float fraction = room.Sweep(float2.zero, new float2(10, 10), 0.5f, out float2 normal);
            float2 expected = math.normalize(new float2(-1, -1));

            Assert.That(fraction, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(math.distance(normal, expected), Is.LessThan(0.0001f));
        }

        [Test]
        public void Sweep_FurnitureVertex_UsesRadialContactNormal()
        {
            var room = new RoomArena(
                RoomArenaTests.Rectangle(-5, -5, 5, 5),
                RoomArenaTests.Rectangle(1, 1, 3, 3));
            float fraction = room.Sweep(float2.zero, new float2(3, 3), 0.5f, out float2 normal);

            Assert.That(fraction, Is.EqualTo((math.sqrt(2) - 0.5f) / math.sqrt(18)).Within(0.0001f));
            Assert.That(math.distance(normal, math.normalize(new float2(-1, -1))), Is.LessThan(0.0001f));
        }

        [Test]
        public void Sweep_LeavingContact_DoesNotCollideAgain()
        {
            var room = new RoomArena(
                RoomArenaTests.Rectangle(-5, -5, 5, 5),
                RoomArenaTests.Rectangle(-1, -2, 1, 2));
            float fraction = room.Sweep(new float2(-1.5f, 0), new float2(-4, 0), 0.5f, out float2 normal);

            Assert.That(fraction, Is.EqualTo(1));
            Assert.That(normal, Is.EqualTo(float2.zero));
        }

        [Test]
        public void Simulation_WallHit_ContinuesFlightAndReturnsAlongSavedPath()
        {
            var room = new RoomArena(RoomArenaTests.Rectangle(-10, -7, 10, 7));
            MatchSimulation simulation = CreateSimulation(room, 12, 20);
            float directionX = simulation.GetHero(0).Pos.x <= 0 ? -1 : 1;
            simulation.Enqueue(new ThrowHookIntent(1, new float2(directionX, 0), simulation.Time));
            for (int i = 0; i < 30; i++)
            {
                simulation.Step(1f / 30);
                if (simulation.GetHook(0).Dir.x * directionX < 0)
                {
                    break;
                }
            }

            var hook = simulation.GetHook(0);
            Assert.That(hook.Phase, Is.EqualTo(HookPhase.Extending));
            Assert.That(hook.Dir.x * directionX, Is.LessThan(0));
            Assert.That(hook.Speed, Is.EqualTo(20).Within(0.0001f));
            Assert.That(hook.Path.Count, Is.EqualTo(2));
            Assert.That(room.Contains(hook.Tip, hook.Radius), Is.True);
            Setup.Advance(simulation, 10);
            hook = simulation.GetHook(0);
            Assert.That(hook.Phase, Is.EqualTo(HookPhase.Retracting));
            Assert.That(hook.Travelled, Is.EqualTo(12).Within(0.0001f));
            Assert.That(hook.Path.Count, Is.EqualTo(2));
            Setup.Advance(simulation, 90);
            Assert.That(simulation.GetHook(0).Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void Simulation_FurnitureHit_ReflectsIntoFreeSpace()
        {
            var room = new RoomArena(
                RoomArenaTests.Rectangle(-8, -8, 8, 8),
                RoomArenaTests.Rectangle(-1, -1, 1, 1));
            MatchSimulation simulation = CreateSimulation(room, 30, 20);
            float2 origin = simulation.GetHero(0).Pos;
            float2 direction = math.normalize(-origin);
            room.Sweep(origin, origin + direction * 30, 0.35f, out float2 normal);
            simulation.Enqueue(new ThrowHookIntent(1, direction, simulation.Time));
            for (int i = 0; i < 60; i++)
            {
                simulation.Step(1f / 30);
                if (simulation.GetHook(0).Path.Count > 1)
                {
                    break;
                }
            }

            var hook = simulation.GetHook(0);
            Assert.That(hook.Phase, Is.EqualTo(HookPhase.Extending));
            Assert.That(math.dot(hook.Dir, normal), Is.GreaterThan(0));
            Assert.That(hook.Speed, Is.EqualTo(20).Within(0.0001f));
            Assert.That(room.Contains(hook.Tip, hook.Radius), Is.True);
        }

        [Test]
        public void Simulation_FastHook_HandlesSeveralBouncesInOneTick()
        {
            var room = new RoomArena(RoomArenaTests.Rectangle(-3, -3, 3, 3));
            MatchSimulation simulation = CreateSimulation(room, 80, 1000);
            simulation.Enqueue(new ThrowHookIntent(1, new float2(1, 0), simulation.Time));
            simulation.Step(0.02f);
            var hook = simulation.GetHook(0);

            Assert.That(hook.Phase, Is.EqualTo(HookPhase.Extending));
            Assert.That(hook.Path.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(hook.Travelled, Is.EqualTo(20).Within(0.001f));
            Assert.That(hook.Speed, Is.EqualTo(1000).Within(0.001f));
            Assert.That(room.Contains(hook.Tip, hook.Radius), Is.True);
        }

        private static MatchSimulation CreateSimulation(RoomArena room, float range, float speed)
        {
            var arena = new ArenaConfigData(
                new float2(24, 14),
                3.5f,
                new[] { new float2(-4, 0), new float2(-4, 3) },
                new[] { new float2(4, 0) });
            var hook = new HookConfigData(range, speed, 0.35f, 175, 3, 0, 0, 0, 0, 4);
            var simulation = new MatchSimulation(
                arena,
                Setup.HeroConfig(),
                hook,
                new[] { Setup.Hero(), Setup.Hero(id: 2, y: 3) });
            simulation.ConfigureRules(
                Setup.MatchConfig(),
                new EconomyConfigData(150, 50, 10, 0, new[] { 100 }));
            simulation.Rules.SetMode(GameMode.RoomFreeForAll);
            simulation.Rules.Begin(0, true, true, 2, 0);
            Assert.That(simulation.TrySetRoom(room), Is.True);
            simulation.Rules.ArenaPlaced(0);
            simulation.Step(1f / 30, 3);
            return simulation;
        }
    }
}
