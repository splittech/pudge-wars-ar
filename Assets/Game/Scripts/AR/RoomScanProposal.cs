using System;
using System.Collections.Generic;
using Game.Core.Geometry;
using Unity.Mathematics;
using UnityEngine.XR.ARSubsystems;

namespace Game.AR
{
    public static class RoomScanProposal
    {
        public const float FloorHeightToleranceMeters = 0.1f;
        public const float MinimumFurnitureHeightMeters = 0.15f;
        public const float MaximumFurnitureHeightMeters = 2.5f;
        private const float MinimumFloorArea = 100;
        private const float MinimumFurnitureArea = 1;

        public static bool TryCreate(
            IReadOnlyList<RoomScanSurface> surfaces,
            out RoomArena room,
            out IReadOnlyList<TrackableId> selectedFurniture,
            out int skippedFurniture)
        {
            room = null;
            selectedFurniture = Array.Empty<TrackableId>();
            skippedFurniture = 0;
            if (surfaces == null)
            {
                return false;
            }

            float largestArea = 0;
            var furniture = new List<RoomScanSurface>();
            foreach (RoomScanSurface surface in surfaces)
            {
                if (!math.isfinite(surface.HeightMeters))
                {
                    continue;
                }

                if (math.abs(surface.HeightMeters) <= FloorHeightToleranceMeters)
                {
                    float area = Area(surface.Boundary);
                    if (area < MinimumFloorArea || area <= largestArea)
                    {
                        continue;
                    }

                    try
                    {
                        var candidate = new RoomArena(surface.Boundary);
                        if (candidate.Contains(float2.zero, 0))
                        {
                            room = candidate;
                            largestArea = area;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Plane boundaries can be incomplete while ARCore refines them.
                    }
                }
                else if (surface.HeightMeters >= MinimumFurnitureHeightMeters
                    && surface.HeightMeters <= MaximumFurnitureHeightMeters)
                {
                    furniture.Add(surface);
                }
            }

            if (room == null)
            {
                return false;
            }

            furniture.Sort(CompareFurniture);
            float2[] boundary = Copy(room.GetPolygon(0));
            var obstacles = new List<float2[]>();
            var accepted = new List<TrackableId>();
            var seen = new HashSet<TrackableId>();
            foreach (RoomScanSurface surface in furniture)
            {
                if (!seen.Add(surface.Id))
                {
                    continue;
                }

                if (obstacles.Count >= RoomArena.MaximumObstacles || Area(surface.Boundary) < MinimumFurnitureArea)
                {
                    skippedFurniture++;
                    continue;
                }

                obstacles.Add(surface.Boundary);
                try
                {
                    room = new RoomArena(boundary, obstacles.ToArray());
                    accepted.Add(surface.Id);
                }
                catch (ArgumentException)
                {
                    obstacles.RemoveAt(obstacles.Count - 1);
                    skippedFurniture++;
                }
            }

            selectedFurniture = accepted.AsReadOnly();
            return true;
        }

        private static int CompareFurniture(RoomScanSurface first, RoomScanSurface second)
        {
            int areaOrder = Area(second.Boundary).CompareTo(Area(first.Boundary));
            if (areaOrder != 0)
            {
                return areaOrder;
            }

            return StringComparer.Ordinal.Compare(first.Id.ToString(), second.Id.ToString());
        }

        private static float Area(float2[] polygon)
        {
            if (polygon == null || polygon.Length < 3 || polygon.Length > RoomArena.MaximumVertices)
            {
                return 0;
            }

            float area = 0;
            for (int i = 0; i < polygon.Length; i++)
            {
                float2 a = polygon[i];
                float2 b = polygon[(i + 1) % polygon.Length];
                if (!math.all(math.isfinite(a)))
                {
                    return 0;
                }

                area += a.x * b.y - a.y * b.x;
            }

            return math.abs(area) * 0.5f;
        }

        private static float2[] Copy(IReadOnlyList<float2> points)
        {
            var copy = new float2[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                copy[i] = points[i];
            }

            return copy;
        }
    }
}
