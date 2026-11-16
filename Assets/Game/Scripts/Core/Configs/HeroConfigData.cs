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
        public readonly float EnemyTerritoryGracePeriod;
        public readonly float EnemyTerritoryDamageInterval;
        public readonly int EnemyTerritoryDamage;

        public HeroConfigData(int maxHp, float moveSpeed, float radius, float respawnTime,
            float enemyTerritoryGracePeriod = 5, float enemyTerritoryDamageInterval = 1, int enemyTerritoryDamage = 50)
        {
            if (!float.IsFinite(enemyTerritoryGracePeriod) || enemyTerritoryGracePeriod < 0
                || !float.IsFinite(enemyTerritoryDamageInterval) || enemyTerritoryDamageInterval <= 0 || enemyTerritoryDamage < 0)
                throw new ArgumentOutOfRangeException(nameof(enemyTerritoryGracePeriod));
            MaxHp = maxHp;
            MoveSpeed = moveSpeed;
            Radius = radius;
            RespawnTime = respawnTime;
            EnemyTerritoryGracePeriod = enemyTerritoryGracePeriod;
            EnemyTerritoryDamageInterval = enemyTerritoryDamageInterval;
            EnemyTerritoryDamage = enemyTerritoryDamage;
        }
    }
}
