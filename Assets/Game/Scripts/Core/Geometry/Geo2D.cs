using System;
using Unity.Mathematics;

namespace Game.Core.Geometry
{
    public static class Geo2D
    {
        public static float2 ClampToRect(float2 point, Rect2 rect)
        {
            ValidatePoint(point, nameof(point));
            return math.clamp(point, rect.Min, rect.Max);
        }

        public static bool CircleCircle(float2 firstCenter, float firstRadius, float2 secondCenter, float secondRadius)
        {
            ValidatePoint(firstCenter, nameof(firstCenter));
            ValidatePoint(secondCenter, nameof(secondCenter));
            ValidateRadius(firstRadius, nameof(firstRadius));
            ValidateRadius(secondRadius, nameof(secondRadius));
            double2 delta = (double2)firstCenter - (double2)secondCenter;
            double radius = (double)firstRadius + secondRadius;
            return math.dot(delta, delta) <= radius * radius;
        }

        /// <summary>
        /// Sweeps a moving circle against a stationary circle (FR-GP-02).
        /// Returns the first contact fraction in [0, 1], including tangency and initial overlap.
        /// On a miss, t is zero. Double intermediates avoid overflow for finite float inputs.
        /// </summary>
        public static bool SweptCircle(float2 start, float2 end, float movingRadius,
            float2 center, float targetRadius, out float t)
        {
            ValidatePoint(start, nameof(start));
            ValidatePoint(end, nameof(end));
            ValidatePoint(center, nameof(center));
            ValidateRadius(movingRadius, nameof(movingRadius));
            ValidateRadius(targetRadius, nameof(targetRadius));
            t = 0f;
            double2 offset = (double2)start - (double2)center;
            double2 direction = (double2)end - (double2)start;
            double radius = (double)movingRadius + targetRadius;
            double c = math.dot(offset, offset) - radius * radius;
            if (c <= 0d)
                return true;

            double a = math.dot(direction, direction);
            double b = math.dot(offset, direction);
            if (a == 0d || b >= 0d)
                return false;

            // Use the cross product for the discriminant to avoid subtracting
            // large squared longitudinal distances for a distant target.
            double cross = offset.x * direction.y - offset.y * direction.x;
            double discriminant = a * radius * radius - cross * cross;
            if (discriminant < 0d)
                return false;

            // Equivalent to (-b - sqrt(discriminant)) / a, with less cancellation.
            double contact = c / (-b + math.sqrt(discriminant));
            if (contact > 1d)
                return false;

            t = (float)contact;
            return true;
        }

        private static void ValidatePoint(float2 point, string parameter)
        {
            if (!math.all(math.isfinite(point)))
                throw new ArgumentException("Coordinates must be finite.", parameter);
        }

        private static void ValidateRadius(float radius, string parameter)
        {
            if (!math.isfinite(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(parameter, "Radius must be finite and nonnegative.");
        }
    }
}
