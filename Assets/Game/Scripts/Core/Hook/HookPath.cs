using System;
using Unity.Mathematics;

namespace Game.Core.Hook
{
    public struct HookPath : IEquatable<HookPath>
    {
        public const int Capacity = 32;
        // Fixed value storage preserves copied simulation snapshots without per-throw allocations.
        private float2 _point0;
        private float2 _point1;
        private float2 _point2;
        private float2 _point3;
        private float2 _point4;
        private float2 _point5;
        private float2 _point6;
        private float2 _point7;
        private float2 _point8;
        private float2 _point9;
        private float2 _point10;
        private float2 _point11;
        private float2 _point12;
        private float2 _point13;
        private float2 _point14;
        private float2 _point15;
        private float2 _point16;
        private float2 _point17;
        private float2 _point18;
        private float2 _point19;
        private float2 _point20;
        private float2 _point21;
        private float2 _point22;
        private float2 _point23;
        private float2 _point24;
        private float2 _point25;
        private float2 _point26;
        private float2 _point27;
        private float2 _point28;
        private float2 _point29;
        private float2 _point30;
        private float2 _point31;

        public int Count { get; private set; }

        public bool Equals(HookPath other)
        {
            if (Count != other.Count)
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                if (math.any(Get(i) != other.Get(i)))
                {
                    return false;
                }
            }

            return true;
        }

        public float2 Get(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index switch
            {
                0 => _point0,
                1 => _point1,
                2 => _point2,
                3 => _point3,
                4 => _point4,
                5 => _point5,
                6 => _point6,
                7 => _point7,
                8 => _point8,
                9 => _point9,
                10 => _point10,
                11 => _point11,
                12 => _point12,
                13 => _point13,
                14 => _point14,
                15 => _point15,
                16 => _point16,
                17 => _point17,
                18 => _point18,
                19 => _point19,
                20 => _point20,
                21 => _point21,
                22 => _point22,
                23 => _point23,
                24 => _point24,
                25 => _point25,
                26 => _point26,
                27 => _point27,
                28 => _point28,
                29 => _point29,
                30 => _point30,
                31 => _point31,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        public bool TryAdd(float2 point)
        {
            if (!math.all(math.isfinite(point)))
            {
                return false;
            }

            if (Count > 0 && math.distancesq(Get(Count - 1), point) < 1e-10f)
            {
                return true;
            }

            if (Count == Capacity)
            {
                return false;
            }

            switch (Count)
            {
                case 0:
                    _point0 = point;
                    break;
                case 1:
                    _point1 = point;
                    break;
                case 2:
                    _point2 = point;
                    break;
                case 3:
                    _point3 = point;
                    break;
                case 4:
                    _point4 = point;
                    break;
                case 5:
                    _point5 = point;
                    break;
                case 6:
                    _point6 = point;
                    break;
                case 7:
                    _point7 = point;
                    break;
                case 8:
                    _point8 = point;
                    break;
                case 9:
                    _point9 = point;
                    break;
                case 10:
                    _point10 = point;
                    break;
                case 11:
                    _point11 = point;
                    break;
                case 12:
                    _point12 = point;
                    break;
                case 13:
                    _point13 = point;
                    break;
                case 14:
                    _point14 = point;
                    break;
                case 15:
                    _point15 = point;
                    break;
                case 16:
                    _point16 = point;
                    break;
                case 17:
                    _point17 = point;
                    break;
                case 18:
                    _point18 = point;
                    break;
                case 19:
                    _point19 = point;
                    break;
                case 20:
                    _point20 = point;
                    break;
                case 21:
                    _point21 = point;
                    break;
                case 22:
                    _point22 = point;
                    break;
                case 23:
                    _point23 = point;
                    break;
                case 24:
                    _point24 = point;
                    break;
                case 25:
                    _point25 = point;
                    break;
                case 26:
                    _point26 = point;
                    break;
                case 27:
                    _point27 = point;
                    break;
                case 28:
                    _point28 = point;
                    break;
                case 29:
                    _point29 = point;
                    break;
                case 30:
                    _point30 = point;
                    break;
                case 31:
                    _point31 = point;
                    break;
            }

            Count++;
            return true;
        }

        public HookPath Prefix(int count)
        {
            if (count < 0 || count > Count)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            HookPath result = this;
            result.Count = count;
            return result;
        }

        public float2 EvaluateReturn(float2 tip, float2 owner, float distance, out int nextIndex)
        {
            nextIndex = Count - 1;
            while (nextIndex >= 0)
            {
                float2 waypoint = Get(nextIndex);
                float length = math.distance(tip, waypoint);
                if (length > distance)
                {
                    return tip + math.normalizesafe(waypoint - tip) * distance;
                }

                tip = waypoint;
                distance -= length;
                nextIndex--;
            }

            float2 delta = owner - tip;
            if (math.length(delta) <= distance)
            {
                return owner;
            }

            return tip + math.normalizesafe(delta) * distance;
        }
    }
}
