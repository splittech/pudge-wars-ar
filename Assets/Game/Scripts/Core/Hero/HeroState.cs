using Game.Core.Match;
using Unity.Mathematics;

namespace Game.Core.Hero
{
    public struct HeroState
    {
        public uint Id;
        public Team Team;
        public float2 Pos;
        public float2 MoveTarget;
        public float Radius;
        public int Hp;
        public bool Alive;
        public bool IsPulled;
        public double HookCooldownEnd;
        public double RespawnAt;
        public bool EnemyTerritoryExposed;
        public double EnemyTerritoryDamageAt;
        public int Gold;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int HookHits;
        public int HookLevel;
        public float HookRange;
        public float HookSpeed;
        public float HookRadius;
        public int HookDamage;
    }
}
