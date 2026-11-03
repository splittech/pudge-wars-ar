namespace Game.Core.Events
{
    public readonly struct HeroDied
    {
        public readonly uint HeroId;
        public readonly uint KillerId;
        public readonly double Time;

        public HeroDied(uint heroId, uint killerId, double time)
        {
            HeroId = heroId;
            KillerId = killerId;
            Time = time;
        }
    }
}
