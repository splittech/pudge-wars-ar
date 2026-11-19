using System;
using Game.Core.Geometry;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class RoomArenaTests
    {
        internal static float2[] Rectangle(float left, float bottom, float right, float top)
        {
            return new[] { new float2(left, bottom), new float2(right, bottom), new float2(right, top), new float2(left, top) };
        }

        [Test]
        public void Move_ThroughFurniture_StopsBeforeContact()
        {
            var room = new RoomArena(Rectangle(-10, -8, 10, 8), Rectangle(-1, -2, 1, 2));
            float2 result = room.Move(new float2(-4, 0), new float2(4, 0), 0.5f);
            Assert.That(result.x, Is.EqualTo(-1.501f).Within(0.002f));
            Assert.That(room.Contains(result, 0.5f), Is.True);
        }

        [Test]
        public void Move_OutsideRoom_RespectsRadius()
        {
            var room = new RoomArena(Rectangle(-10, -8, 10, 8));
            Assert.That(room.Move(float2.zero, new float2(30, 0), 0.5f).x, Is.EqualTo(9.499f).Within(0.002f));
            var target = new float2(2, 3);
            Assert.That(room.Move(float2.zero, target, 0.5f), Is.EqualTo(target));
        }

        [Test]
        public void Move_ConcaveBoundary_DoesNotCutAcrossMissingCorner()
        {
            var room = new RoomArena(new[]
            {
                new float2(-6, -6), new float2(6, -6), new float2(6, -1),
                new float2(-1, -1), new float2(-1, 6), new float2(-6, 6)
            });
            float2 result = room.Move(new float2(-4, 4), new float2(4, -4), 0.5f);
            Assert.That(result.x, Is.LessThan(-1.5f));
            Assert.That(room.Contains(result, 0.5f), Is.True);
        }

        [Test]
        public void Constructor_SelfIntersectingBoundary_Rejects()
        {
            Assert.Throws<ArgumentException>(() => new RoomArena(new[]
            {
                new float2(-2, -2), new float2(2, 2), new float2(-2, 2), new float2(2, -2)
            }));
        }

        [TestCase(-11, -2, -9, 2)]
        [TestCase(-10, -2, -8, 2)]
        [TestCase(-1, -1, 2, 2)]
        public void Constructor_InvalidFurniture_Rejects(float left, float bottom, float right, float top)
        {
            Assert.Throws<ArgumentException>(() => new RoomArena(
                Rectangle(-10, -8, 10, 8), Rectangle(-2, -2, 1, 1), Rectangle(left, bottom, right, top)));
        }

        [Test]
        public void Constructor_SourceMutation_DoesNotChangeMap()
        {
            float2[] boundary = Rectangle(-10, -8, 10, 8);
            var room = new RoomArena(boundary);
            boundary[0] = new float2(float.NaN);
            Assert.That(room.Contains(float2.zero, 0.5f), Is.True);
        }

        [Test]
        public void FindSpawns_AllPlayersFitWithoutOverlap()
        {
            var room = new RoomArena(Rectangle(-10, -8, 10, 8), Rectangle(-1, -2, 1, 2));
            Assert.That(room.TryFindSpawns(6, 0.5f, out float2[] spawns), Is.True);
            for (int i = 0; i < spawns.Length; i++)
            {
                Assert.That(room.Contains(spawns[i], 0.5f), Is.True);
                for (int j = 0; j < i; j++)
                {
                    Assert.That(math.distance(spawns[i], spawns[j]), Is.GreaterThan(1));
                }
            }

            var tiny = new RoomArena(Rectangle(0, 0, 0.8f, 0.8f));
            Assert.That(tiny.TryFindSpawns(2, 0.5f, out _), Is.False);
        }

        [Test]
        public void Codec_RoundTrip_PreservesFurniture()
        {
            var room = new RoomArena(Rectangle(-10, -8, 10, 8), Rectangle(-1, -2, 1, 2));
            Assert.That(RoomArenaCodec.TryDecode(RoomArenaCodec.Encode(room), out RoomArena copy, out _), Is.True);
            Assert.That(copy.GetPolygon(1), Is.EqualTo(room.GetPolygon(1)));
            Assert.That(copy.Contains(float2.zero, 0.5f), Is.False);
        }

        [Test]
        public void Codec_TruncatedOversizedTrailingAndNonfinitePackets_Reject()
        {
            byte[] bytes = RoomArenaCodec.Encode(new RoomArena(Rectangle(-10, -8, 10, 8)));
            for (int size = 0; size < bytes.Length; size++)
            {
                var truncated = new byte[size];
                Array.Copy(bytes, truncated, size);
                Assert.That(RoomArenaCodec.TryDecode(truncated, out _, out _), Is.False);
            }

            Assert.That(RoomArenaCodec.TryDecode(new byte[9001], out _, out _), Is.False);
            var trailing = new byte[bytes.Length + 1];
            Array.Copy(bytes, trailing, bytes.Length);
            Assert.That(RoomArenaCodec.TryDecode(trailing, out _, out _), Is.False);
            Array.Copy(BitConverter.GetBytes(float.NaN), 0, bytes, 3, 4);
            Assert.That(RoomArenaCodec.TryDecode(bytes, out _, out _), Is.False);
            bytes[0] = 2;
            Assert.That(RoomArenaCodec.TryDecode(bytes, out _, out _), Is.False);
        }
    }
}
