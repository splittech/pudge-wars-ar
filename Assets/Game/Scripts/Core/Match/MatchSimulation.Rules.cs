using Game.Core.Combat;
using Game.Core.Configs;
using Game.Core.Economy;
using Game.Core.Hero;
using Game.Core.Hook;

namespace Game.Core.Match
{
    public sealed partial class MatchSimulation
    {
        private readonly DamageHistory _damageHistory = new();
        private EconomyConfigData _economy;
        public MatchRules Rules { get; private set; }
        public bool CanPlay => Rules == null || Rules.Phase == MatchPhase.Playing;

        public void ConfigureRules(MatchConfigData match, EconomyConfigData economy)
        {
            Rules = new MatchRules(match);
            _economy = economy;
        }

        public int CountTeam(Team team)
        {
            int count = 0;
            foreach (var hero in _heroes) if (hero.Team == team) count++;
            return count;
        }

        public bool SetTeam(uint id, Team team)
        {
            int index = Find(id);
            if (index < 0 || (team != Team.A && team != Team.B)) return false;
            _heroes[index].Team = team;
            ResetHero(index, false);
            return true;
        }

        public void ResetRound()
        {
            _intents.Clear(); _damageHistory.Clear();
            _throws.Clear(); _hits.Clear(); _clashes.Clear(); _damage.Clear(); _deaths.Clear();
            for (int i = 0; i < _heroes.Length; i++) ResetHero(i, true);
        }

        private void ResetHero(int index, bool resetStatistics)
        {
            ref var hero = ref _heroes[index];
            if (resetStatistics)
            {
                uint id = hero.Id;
                Team team = hero.Team;
                hero = new HeroState { Id = id, Team = team, Radius = _heroConfig.Radius, Gold = _economy.StartGold };
                InitializeHookStats(ref hero);
            }
            int spawnIndex = 0;
            for (int i = 0; i < index; i++) if (_heroes[i].Team == hero.Team) spawnIndex++;
            var points = hero.Team == Team.A ? _arena.TeamASpawnPoints : _arena.TeamBSpawnPoints;
            hero.Pos = points[spawnIndex % points.Count];
            hero.MoveTarget = hero.Pos;
            hero.Hp = _heroConfig.MaxHp; hero.Alive = true; hero.IsPulled = false;
            hero.HookCooldownEnd = 0; hero.RespawnAt = 0;
            _hooks[index] = new HookState { OwnerId = hero.Id };
        }

        private void InitializeHookStats(ref HeroState hero)
        {
            hero.HookRange = _hookConfig.Range + hero.RangeLevel * _hookConfig.RangePerLevel;
            hero.HookSpeed = _hookConfig.Speed + hero.SpeedLevel * _hookConfig.SpeedPerLevel;
            hero.HookRadius = _hookConfig.Radius + hero.RadiusLevel * _hookConfig.RadiusPerLevel;
            hero.HookDamage = _hookConfig.Damage + hero.DamageLevel * _hookConfig.DamagePerLevel;
        }

        public int UpgradeLevel(uint id, UpgradeId upgrade)
        {
            int index = Find(id);
            if (index < 0) return -1;
            var hero = _heroes[index];
            return upgrade switch
            {
                UpgradeId.Range => hero.RangeLevel, UpgradeId.Speed => hero.SpeedLevel,
                UpgradeId.Radius => hero.RadiusLevel, UpgradeId.Damage => hero.DamageLevel, _ => -1
            };
        }

        public bool TryBuyUpgrade(uint id, UpgradeId upgrade, out string reason)
        {
            reason = string.Empty;
            if (Rules == null || !CanPlay) { reason = "Match is not playing"; return false; }
            int index = Find(id);
            int level = UpgradeLevel(id, upgrade);
            if (level < 0) { reason = "Unknown hero or upgrade"; return false; }
            if (level >= _hookConfig.MaxLevel || level >= _economy.UpgradePrices.Count)
            { reason = "Maximum upgrade level"; return false; }
            int price = _economy.UpgradePrices[level];
            ref var hero = ref _heroes[index];
            if (hero.Gold < price) { reason = "Not enough gold"; return false; }
            hero.Gold -= price;
            switch (upgrade)
            {
                case UpgradeId.Range: hero.RangeLevel++; break;
                case UpgradeId.Speed: hero.SpeedLevel++; break;
                case UpgradeId.Radius: hero.RadiusLevel++; break;
                case UpgradeId.Damage: hero.DamageLevel++; break;
            }
            InitializeHookStats(ref hero);
            return true;
        }

        private void RespawnHeroes()
        {
            for (int i = 0; i < _heroes.Length; i++)
                if (!_heroes[i].Alive && Time >= _heroes[i].RespawnAt)
                    ResetHero(i, false);
        }

        private void ApplyMatchRules()
        {
            foreach (var hit in _damage) _damageHistory.Record(hit.HeroId, hit.AttackerId, hit.Time);
            foreach (var death in _deaths)
            {
                int victim = Find(death.HeroId);
                int killer = Find(death.KillerId);
                _heroes[victim].Deaths++;
                _heroes[victim].RespawnAt = Time + _heroConfig.RespawnTime;
                _heroes[victim].IsPulled = false;
                _heroes[victim].MoveTarget = _heroes[victim].Pos;
                _heroes[killer].Kills++;
                _heroes[killer].Gold += _economy.KillGold;
                Rules.AddKill(_heroes[killer].Team);
                foreach (uint id in _damageHistory.Assists(death.HeroId, death.KillerId, Time, _economy.AssistWindow))
                {
                    int assistant = Find(id);
                    if (assistant < 0 || _heroes[assistant].Team != _heroes[killer].Team) continue;
                    _heroes[assistant].Gold += _economy.AssistGold;
                    _heroes[assistant].Assists++;
                }
                _damageHistory.Remove(death.HeroId);
                // Death cancels both the victim's own hook and any hook carrying the victim.
                for (int i = 0; i < _hooks.Length; i++)
                {
                    if (i == victim)
                    {
                        int target = Find(_hooks[i].TargetId);
                        if (target >= 0) { _heroes[target].IsPulled = false; _heroes[target].MoveTarget = _heroes[target].Pos; }
                        _hooks[i] = new HookState { OwnerId = death.HeroId };
                    }
                    else if (_hooks[i].TargetId == death.HeroId)
                    {
                        HookSimulation.Turn(ref _hooks[i], HookPhase.Retracting, Time);
                        _hooks[i].TargetId = 0;
                    }
                }
            }
            Rules.Advance(Time, CountTeam(Team.A), CountTeam(Team.B));
        }
    }
}
