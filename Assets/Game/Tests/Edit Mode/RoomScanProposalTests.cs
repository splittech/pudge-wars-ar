using System;
using Game.AR;
using Game.Core.Geometry;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine.XR.ARSubsystems;

namespace Game.Tests.EditMode
{
    public sealed class RoomScanProposalTests
    {
        [Test]
        public void Create_FloorAndFurniture_ProposesRoomWithoutTaps()
        {
            var surfaces = new[]
            {
                Surface(1, 0, -8, -8, 8, 8),
                Surface(2, 0.8f, 1, 1, 3, 3)
            };

            Assert.That(RoomScanProposal.TryCreate(surfaces, out RoomArena room, out var selected, out int skipped), Is.True);
            Assert.That(room.PolygonCount, Is.EqualTo(2));
            Assert.That(room.Contains(new float2(2, 2), 0), Is.False);
            Assert.That(selected.Count, Is.EqualTo(1));
            Assert.That(skipped, Is.Zero);
        }

        [Test]
        public void Create_MultipleFloorPatches_UsesLargestConnectedToOrigin()
        {
            var surfaces = new[]
            {
                Surface(1, 0, -6, -6, 6, 6),
                Surface(2, 0.03f, -8, -8, 8, 8),
                Surface(3, 0, 20, 20, 60, 60),
                Surface(4, 0.8f, -9, -9, 9, 9)
            };

            Assert.That(RoomScanProposal.TryCreate(surfaces, out RoomArena room, out _, out _), Is.True);
            Assert.That(room.Bounds.Min, Is.EqualTo(new float2(-8, -8)));
            Assert.That(room.Bounds.Max, Is.EqualTo(new float2(8, 8)));
        }

        [TestCase(0)]
        [TestCase(0.13f)]
        [TestCase(2.6f)]
        [TestCase(float.NaN)]
        public void Create_SurfaceOutsideFurnitureHeightRange_DoesNotBecomeObstacle(float height)
        {
            var surfaces = new[]
            {
                Surface(1, 0, -8, -8, 8, 8),
                Surface(2, height, 1, 1, 3, 3)
            };

            Assert.That(RoomScanProposal.TryCreate(surfaces, out RoomArena room, out _, out _), Is.True);
            Assert.That(room.PolygonCount, Is.EqualTo(1));
        }

        [Test]
        public void Create_OverlappingAndOutsideFurniture_SkipsInvalidSurfaces()
        {
            var surfaces = new[]
            {
                Surface(1, 0, -8, -8, 8, 8),
                Surface(2, 0.8f, 1, 1, 4, 4),
                Surface(3, 1.2f, 2, 2, 3, 3),
                Surface(4, 0.8f, 7, 7, 10, 10),
                Surface(5, 0.8f, -3, -3, -2.9f, -2.9f)
            };

            Assert.That(RoomScanProposal.TryCreate(surfaces, out RoomArena room, out var selected, out int skipped), Is.True);
            Assert.That(room.PolygonCount, Is.EqualTo(2));
            Assert.That(selected[0], Is.EqualTo(new TrackableId(2, 0)));
            Assert.That(skipped, Is.EqualTo(3));
            Array.Reverse(surfaces);
            RoomScanProposal.TryCreate(surfaces, out RoomArena reversed, out _, out _);
            Assert.That(reversed.GetPolygon(1), Is.EqualTo(room.GetPolygon(1)));
        }

        [Test]
        public void Create_IncompleteFloor_DoesNotInventRoom()
        {
            var surfaces = new[]
            {
                Surface(1, 0, -2, -2, 2, 2),
                Surface(2, 0, 10, 10, 30, 30),
                Surface(3, 0.8f, -8, -8, 8, 8)
            };

            Assert.That(RoomScanProposal.TryCreate(surfaces, out _, out _, out _), Is.False);
            Assert.That(RoomScanProposal.TryCreate(null, out _, out _, out _), Is.False);
        }

        [Test]
        public void Create_DenseOrMalformedFurniture_IsSkipped()
        {
            var surfaces = new[]
            {
                Surface(1, 0, -8, -8, 8, 8),
                new RoomScanSurface(new TrackableId(2, 0), 0.8f, new float2[65]),
                new RoomScanSurface(new TrackableId(3, 0), 0.8f, new[]
                {
                    new float2(float.NaN, 0), new float2(1, 0), new float2(0, 1)
                })
            };

            Assert.That(RoomScanProposal.TryCreate(surfaces, out RoomArena room, out _, out int skipped), Is.True);
            Assert.That(room.PolygonCount, Is.EqualTo(1));
            Assert.That(skipped, Is.EqualTo(2));
        }

        [Test]
        public void Create_UpdatedFloorAndRemovedFurniture_RebuildsProposal()
        {
            RoomScanProposal.TryCreate(new[]
            {
                Surface(1, 0, -6, -6, 6, 6), Surface(2, 0.8f, 1, 1, 3, 3)
            }, out RoomArena original, out _, out _);
            RoomScanProposal.TryCreate(new[]
            {
                Surface(1, 0, -8, -8, 8, 8)
            }, out RoomArena updated, out _, out _);

            Assert.That(original.PolygonCount, Is.EqualTo(2));
            Assert.That(updated.PolygonCount, Is.EqualTo(1));
            Assert.That(updated.Bounds.Max.x, Is.EqualTo(8));
        }

        private static RoomScanSurface Surface(ulong id, float height, float left, float bottom, float right, float top)
        {
            return new RoomScanSurface(new TrackableId(id, 0), height,
                RoomArenaTests.Rectangle(left, bottom, right, top));
        }
    }
}
