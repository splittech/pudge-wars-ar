namespace Game.Core.Events
{
    public readonly struct HeroDamaged
    {
        public readonly uint HeroId;
        public readonly uint AttackerId;
        public readonly int Damage;
        public readonly int RemainingHp;
        public readonly double Time;

        public HeroDamaged(uint heroId, uint attackerId, int damage, int remainingHp, double time)
        {
            HeroId = heroId;
            AttackerId = attackerId;
            Damage = damage;
            RemainingHp = remainingHp;
            Time = time;
        }
    }
}
