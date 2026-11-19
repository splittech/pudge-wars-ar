using Game.Core.Events;

namespace Game.Core.Match
{
    public sealed partial class MatchSimulation
    {
        private void ApplyTerritoryDamage()
        {
            if (IsRoom)
            {
                return;
            }

            for (int i = 0; i < _heroes.Length; i++)
            {
                ref var hero = ref _heroes[i];
                bool hostile = hero.Team == Team.A ? hero.Pos.x > _arena.RiverWidth * 0.5f
                    : hero.Pos.x < -_arena.RiverWidth * 0.5f;
                if (!hero.Alive || !hostile || _heroConfig.EnemyTerritoryDamage == 0)
                {
                    hero.EnemyTerritoryExposed = false;
                    hero.EnemyTerritoryDamageAt = 0;
                    continue;
                }
                if (!hero.EnemyTerritoryExposed)
                {
                    hero.EnemyTerritoryExposed = true;
                    hero.EnemyTerritoryDamageAt = Time + _heroConfig.EnemyTerritoryGracePeriod;
                }
                if (Time < hero.EnemyTerritoryDamageAt)
                {
                    continue;
                }

                double count = System.Math.Floor((Time - hero.EnemyTerritoryDamageAt) / _heroConfig.EnemyTerritoryDamageInterval) + 1;
                int damage = (int)System.Math.Min(hero.Hp, count * _heroConfig.EnemyTerritoryDamage);
                hero.EnemyTerritoryDamageAt += count * _heroConfig.EnemyTerritoryDamageInterval;
                hero.Hp -= damage;
                _damage.Add(new HeroDamaged(hero.Id, 0, damage, hero.Hp, Time));
                if (hero.Hp > 0)
                {
                    continue;
                }

                hero.Alive = false;
                hero.EnemyTerritoryExposed = false;
                hero.EnemyTerritoryDamageAt = 0;
                _deaths.Add(new HeroDied(hero.Id, 0, Time));
            }
        }
    }
}
