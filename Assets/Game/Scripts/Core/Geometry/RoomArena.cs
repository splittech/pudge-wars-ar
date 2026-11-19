using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Game.Core.Geometry
{
    public sealed class RoomArena
    {
        public const int MaximumObstacles = 16;
        public const int MaximumVertices = 64;
        public const float MetersPerUnit = 0.1f;
        private readonly IReadOnlyList<float2>[] _polygons;

        public RoomArena(float2[] boundary, params float2[][] obstacles)
        {
            if (boundary == null || obstacles == null || obstacles.Length > MaximumObstacles)
            {
                throw new ArgumentException("Room polygon limits exceeded.");
            }

            _polygons = new IReadOnlyList<float2>[obstacles.Length + 1];
            _polygons[0] = CopyPolygon(boundary);
            for (int i = 0; i < obstacles.Length; i++)
            {
                _polygons[i + 1] = CopyPolygon(obstacles[i]);
            }

            float2 minimum = boundary[0];
            float2 maximum = minimum;
            foreach (float2 point in boundary)
            {
                minimum = math.min(minimum, point);
                maximum = math.max(maximum, point);
            }

            Bounds = new Rect2(minimum, maximum);
            for (int i = 1; i < _polygons.Length; i++)
            {
                foreach (float2 point in _polygons[i])
                {
                    if (!Inside(_polygons[0], point) || DistanceToEdges(_polygons[0], point) < 0.001f)
                    {
                        throw new ArgumentException("Furniture must be strictly inside the room.");
                    }
                }

                for (int j = 0; j < i; j++)
                {
                    if (Crosses(_polygons[i], _polygons[j]) || (j > 0 &&
                        (Inside(_polygons[j], _polygons[i][0]) || Inside(_polygons[i], _polygons[j][0]))))
                    {
                        throw new ArgumentException("Room polygons must not intersect or overlap.");
                    }
                }
            }
        }

        public Rect2 Bounds { get; }
        public int PolygonCount => _polygons.Length;
        public IReadOnlyList<float2> GetPolygon(int index) => _polygons[index];

        public bool Contains(float2 point, float radius)
        {
            if (!math.all(math.isfinite(point)) || !math.isfinite(radius) || radius < 0 || !Inside(_polygons[0], point))
            {
                return false;
            }

            for (int i = 0; i < _polygons.Length; i++)
            {
                if ((i > 0 && Inside(_polygons[i], point)) || DistanceToEdges(_polygons[i], point) < radius + 0.0001f)
                {
                    return false;
                }
            }

            return true;
        }

        public float2 Move(float2 start, float2 destination, float radius)
        {
            if (!Contains(start, radius))
            {
                return start;
            }

            float fraction = Sweep(start, destination, radius);
            if (fraction >= 1 && Contains(destination, radius))
            {
                return destination;
            }

            float distance = math.distance(start, destination);
            float safeFraction = math.max(0, fraction - 0.001f / math.max(distance, 0.001f));
            return math.lerp(start, destination, safeFraction);
        }

        public float Sweep(float2 start, float2 end, float radius)
        {
            return Sweep(start, end, radius, out _);
        }

        public float Sweep(
            float2 start,
            float2 end,
            float radius,
            out float2 contactNormal)
        {
            float earliest = 1;
            float2 normals = float2.zero;
            float2 velocity = end - start;
            foreach (IReadOnlyList<float2> polygon in _polygons)
            {
                for (int i = 0; i < polygon.Count; i++)
                {
                    float2 a = polygon[i];
                    float2 b = polygon[(i + 1) % polygon.Count];
                    if (Geo2D.SweptCircle(start, end, radius, a, 0, out float endpoint))
                    {
                        float2 endpointNormal = math.normalizesafe(
                            start + velocity * endpoint - a,
                            -math.normalizesafe(velocity));
                        RegisterContact(endpoint, endpointNormal, velocity, ref earliest, ref normals);
                    }

                    float2 edge = b - a;
                    float length = math.length(edge);
                    float2 tangent = edge / length;
                    var normal = new float2(-tangent.y, tangent.x);
                    float speed = math.dot(velocity, normal);
                    if (math.abs(speed) < 1e-8f)
                    {
                        continue;
                    }

                    float distance = math.dot(start - a, normal);
                    float side = distance >= 0 ? 1 : -1;
                    if (speed * side >= 0)
                    {
                        continue;
                    }

                    float time = (side * radius - distance) / speed;
                    float projection = math.dot(start + velocity * time - a, tangent);
                    if (projection >= 0 && projection <= length)
                    {
                        RegisterContact(time, normal * side, velocity, ref earliest, ref normals);
                    }
                }
            }

            contactNormal = math.normalizesafe(normals);
            return earliest;
        }

        private static void RegisterContact(
            float time,
            float2 normal,
            float2 velocity,
            ref float earliest,
            ref float2 normals)
        {
            const float tolerance = 0.000001f;
            if (time < 0 || time > 1 || time > earliest + tolerance || math.dot(velocity, normal) >= -0.0000001f)
            {
                return;
            }

            if (time < earliest - tolerance)
            {
                earliest = time;
                normals = normal;
            }
            else
            {
                earliest = math.min(earliest, time);
                normals += normal;
            }
        }

        public bool TryFindSpawns(int count, float radius, out float2[] positions)
        {
            positions = new float2[count];
            for (int slot = 0; slot < count; slot++)
            {
                float best = -1;
                float2 selected = default;
                for (int y = 0; y < 48; y++)
                {
                    for (int x = 0; x < 48; x++)
                    {
                        float2 point = math.lerp(Bounds.Min, Bounds.Max, new float2((x + 0.5f) / 48, (y + 0.5f) / 48));
                        if (!Contains(point, radius + 0.01f))
                        {
                            continue;
                        }

                        float score = DistanceToEdges(_polygons[0], point);
                        for (int i = 1; i < _polygons.Length; i++)
                        {
                            score = math.min(score, DistanceToEdges(_polygons[i], point));
                        }

                        for (int previous = 0; previous < slot; previous++)
                        {
                            float distance = math.distance(point, positions[previous]);
                            if (distance < radius * 2 + 0.02f)
                            {
                                score = -1;
                                break;
                            }

                            score = math.min(score, distance * 0.5f);
                        }

                        if (score > best)
                        {
                            best = score;
                            selected = point;
                        }
                    }
                }

                if (best < 0)
                {
                    positions = Array.Empty<float2>();
                    return false;
                }

                positions[slot] = selected;
            }

            return true;
        }

        private static IReadOnlyList<float2> CopyPolygon(float2[] points)
        {
            if (points == null || points.Length < 3 || points.Length > MaximumVertices)
            {
                throw new ArgumentException("A polygon requires 3–64 vertices.");
            }

            float area = 0;
            for (int i = 0; i < points.Length; i++)
            {
                float2 a = points[i];
                float2 b = points[(i + 1) % points.Length];
                if (!math.all(math.isfinite(a)) || math.any(math.abs(a) > 1000) || math.distancesq(a, b) < 0.0001f)
                {
                    throw new ArgumentException("Polygon coordinates or edges are invalid.");
                }

                area += a.x * b.y - b.x * a.y;
                for (int j = i + 2; j < points.Length; j++)
                {
                    if (i == 0 && j == points.Length - 1)
                    {
                        continue;
                    }

                    if (Intersects(a, b, points[j], points[(j + 1) % points.Length]))
                    {
                        throw new ArgumentException("Polygon must not self-intersect.");
                    }
                }
            }

            if (math.abs(area) < 0.02f)
            {
                throw new ArgumentException("Polygon area is too small.");
            }

            return Array.AsReadOnly((float2[])points.Clone());
        }

        private static bool Inside(IReadOnlyList<float2> polygon, float2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                float2 a = polygon[i];
                float2 b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static float DistanceToEdges(IReadOnlyList<float2> polygon, float2 point)
        {
            float distance = float.MaxValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                float2 a = polygon[i];
                float2 edge = polygon[(i + 1) % polygon.Count] - a;
                float2 closest = a + edge * math.saturate(math.dot(point - a, edge) / math.lengthsq(edge));
                distance = math.min(distance, math.distance(point, closest));
            }

            return distance;
        }

        private static bool Crosses(IReadOnlyList<float2> first, IReadOnlyList<float2> second)
        {
            for (int i = 0; i < first.Count; i++)
            {
                for (int j = 0; j < second.Count; j++)
                {
                    if (Intersects(first[i], first[(i + 1) % first.Count], second[j], second[(j + 1) % second.Count]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        private static bool Intersects(float2 a, float2 b, float2 c, float2 d)
        {
            if (math.any(math.max(a, b) + 0.0001f < math.min(c, d)) || math.any(math.max(c, d) + 0.0001f < math.min(a, b)))
            {
                return false;
            }

            return Cross(b - a, c - a) * Cross(b - a, d - a) <= 0.000001f
                && Cross(d - c, a - c) * Cross(d - c, b - c) <= 0.000001f;
        }
    }
}
