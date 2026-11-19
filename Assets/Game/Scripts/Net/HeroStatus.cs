using Game.Core.Hero;

namespace Game.Net
{
    public struct HeroStatus
    {
        public int Gold;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int HookHits;
        public int HookLevel;
        public float Range;
        public float Speed;
        public float Radius;
        public int Damage;
        public double RespawnAt;

        public static HeroStatus From(HeroState hero)
        {
            return new HeroStatus
            {
                Gold = hero.Gold,
                Kills = hero.Kills,
                Deaths = hero.Deaths,
                Assists = hero.Assists,
                HookHits = hero.HookHits,
                HookLevel = hero.HookLevel,
                Range = hero.HookRange,
                Speed = hero.HookSpeed,
                Radius = hero.HookRadius,
                Damage = hero.HookDamage,
                RespawnAt = hero.RespawnAt
            };
        }
    }
}
