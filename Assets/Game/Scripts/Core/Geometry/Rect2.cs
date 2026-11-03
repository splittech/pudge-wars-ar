using System;
using Unity.Mathematics;

namespace Game.Core.Geometry
{
    public readonly struct Rect2
    {
        public readonly float2 Min;
        public readonly float2 Max;

        public Rect2(float2 min, float2 max)
        {
            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)) || math.any(min > max))
                throw new ArgumentException("Rectangle bounds must be finite and ordered.");

            Min = min;
            Max = max;
        }
    }
}
