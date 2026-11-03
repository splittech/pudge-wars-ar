using Game.Core.Hero;
using Unity.Mathematics;

namespace Game.Core.Combat
{
    public static class Combat
    {
        public static int ApplyHit(ref HeroState target, HeroState attacker, int damage)
        {
            if (!target.Alive || target.Team == attacker.Team)
                return 0;
            int applied = math.min(target.Hp, math.max(0, damage));
            target.Hp -= applied;
            target.Alive = target.Hp > 0;
            return applied;
        }
    }
}
