using System;
using System.Collections.Generic;

namespace Game.Core.Configs
{
    public readonly struct HookConfigData
    {
        public readonly float Range;
        public readonly float Speed;
        public readonly float Radius;
        public readonly int Damage;
        public readonly float Cooldown;
        public readonly float RangePerLevel;
        public readonly float SpeedPerLevel;
        public readonly float RadiusPerLevel;
        public readonly int DamagePerLevel;
        public readonly int MaxLevel;

        public HookConfigData(float range, float speed, float radius, int damage, float cooldown, float rangePerLevel, float speedPerLevel, float radiusPerLevel, int damagePerLevel, int maxLevel)
        {
            Range = range;
            Speed = speed;
            Radius = radius;
            Damage = damage;
            Cooldown = cooldown;
            RangePerLevel = rangePerLevel;
            SpeedPerLevel = speedPerLevel;
            RadiusPerLevel = radiusPerLevel;
            DamagePerLevel = damagePerLevel;
            MaxLevel = maxLevel;
        }
    }
}
