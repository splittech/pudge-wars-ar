using System;
using System.Collections.Generic;

namespace Game.Core.Configs
{
    public readonly struct HeroConfigData
    {
        public readonly int MaxHp;
        public readonly float MoveSpeed;
        public readonly float Radius;
        public readonly float RespawnTime;

        public HeroConfigData(int maxHp, float moveSpeed, float radius, float respawnTime)
        {
            MaxHp = maxHp;
            MoveSpeed = moveSpeed;
            Radius = radius;
            RespawnTime = respawnTime;
        }
    }
}
