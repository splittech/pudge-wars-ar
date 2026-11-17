using System;
using System.Collections.Generic;
using Game.Core.Commands;
using Game.Core.Configs;
using Game.Core.Events;
using Game.Core.Geometry;
using Game.Core.Hero;
using Game.Core.Hook;
using Unity.Mathematics;

namespace Game.Core.Match
{
    public sealed partial class MatchSimulation
    {
        private HeroState[] _heroes;
        private HookState[] _hooks;
        private readonly ArenaConfigData _arena;
        private readonly HeroConfigData _heroConfig;
        private readonly HookConfigData _hookConfig;
        private readonly Queue<Intent> _intents = new(32);
        private readonly List<HookThrown> _throws = new(8);
        private readonly List<HookHit> _hits = new(8);
        private readonly List<HookClash> _clashes = new(8);
        private readonly List<HeroDamaged> _damage = new(8);
        private readonly List<HeroDied> _deaths = new(8);

        private readonly struct Intent
        {
            public readonly uint Id;
            public readonly float2 Value;
            public readonly bool Throw;
            public Intent(uint id, float2 value, bool isThrow)
            {
                Id = id;
                Value = value;
                Throw = isThrow;
            }
        }

        public double Time
        {
            get; private set;
        }
        public uint TickCount
        {
            get; private set;
        }
        public int HeroCount => _heroes.Length;
        public IReadOnlyList<HookThrown> Throws => _throws;
        public IReadOnlyList<HookHit> Hits => _hits;
        public IReadOnlyList<HookClash> Clashes => _clashes;
        public IReadOnlyList<HeroDamaged> Damage => _damage;
        public IReadOnlyList<HeroDied> Deaths => _deaths;
        public HeroState GetHero(int index) => _heroes[index];
        public HookState GetHook(int index) => _hooks[index];

        public MatchSimulation(ArenaConfigData arena, HeroConfigData heroConfig,
            HookConfigData hookConfig, HeroState[] heroes, double initialTime = 0)
        {
            if (heroes == null || !math.isfinite(initialTime))
            {
                throw new ArgumentException("Heroes and a finite initial timestamp are required.");
            }

            if (!math.isfinite(hookConfig.Speed) || hookConfig.Speed <= 0 ||
                !math.isfinite(hookConfig.Range) || hookConfig.Range <= 0 ||
                !math.isfinite(hookConfig.Radius) || hookConfig.Radius < 0 ||
                !math.isfinite(hookConfig.Cooldown) || hookConfig.Cooldown < 0 ||
                !math.isfinite(heroConfig.MoveSpeed) || heroConfig.MoveSpeed < 0)
            {
                throw new ArgumentException("Simulation configuration is invalid.");
            }

            _arena = arena;
            _heroConfig = heroConfig;
            _hookConfig = hookConfig;
            _heroes = (HeroState[])heroes.Clone();
            _hooks = new HookState[heroes.Length];
            Time = initialTime;
            for (int i = 0; i < heroes.Length; i++)
            {
                if (_heroes[i].Id == 0 || !math.all(math.isfinite(_heroes[i].Pos)))
                {
                    throw new ArgumentException("Heroes require unique nonzero IDs and finite positions.");
                }

                for (int j = 0; j < i; j++)
                {
                    if (_heroes[i].Id == _heroes[j].Id)
                    {
                        throw new ArgumentException("Duplicate hero ID.");
                    }
                }

                var bounds = ArenaBounds.ForTeam(arena, _heroes[i].Team, _heroes[i].Radius);
                _heroes[i].Pos = Geo2D.ClampToRect(_heroes[i].Pos, bounds);
                _heroes[i].MoveTarget = _heroes[i].Pos;
                InitializeHookStats(ref _heroes[i]);
                _hooks[i].OwnerId = _heroes[i].Id;
            }
        }

        public void Enqueue(MoveIntent intent) => _intents.Enqueue(new Intent(intent.PlayerId, intent.ArenaPoint, false));
        public void Enqueue(ThrowHookIntent intent) => _intents.Enqueue(new Intent(intent.PlayerId, intent.Direction, true));

        public void AddHero(HeroState hero)
        {
            if (hero.Id == 0 || Find(hero.Id) >= 0 || !math.all(math.isfinite(hero.Pos)) ||
                !math.isfinite(hero.Radius) || hero.Radius < 0)
            {
                throw new ArgumentException("A unique nonzero ID and finite position and radius are required.", nameof(hero));
            }

            hero.Pos = Geo2D.ClampToRect(hero.Pos, ArenaBounds.ForTeam(_arena, hero.Team, hero.Radius));
            hero.MoveTarget = hero.Pos;
            InitializeHookStats(ref hero);
            if (Rules != null)
            {
                hero.Gold = _economy.StartGold;
            }

            int index = _heroes.Length;
            Array.Resize(ref _heroes, index + 1);
            Array.Resize(ref _hooks, index + 1);
            _heroes[index] = hero;
            _hooks[index] = new HookState { OwnerId = hero.Id };
        }

        public bool TryGetHero(uint playerId, out HeroState hero)
        {
            int index = Find(playerId);
            hero = index >= 0 ? _heroes[index] : default;
            return index >= 0;
        }

        public void StopMovement(uint playerId)
        {
            int index = Find(playerId);
            if (index >= 0)
            {
                _heroes[index].MoveTarget = _heroes[index].Pos;
            }
            // Discard queued input from the disconnected controller, preserving other players' order.
            int count = _intents.Count;
            for (int i = 0; i < count; i++)
            {
                Intent intent = _intents.Dequeue();
                if (intent.Id != playerId)
                {
                    _intents.Enqueue(intent);
                }
            }
        }

        public bool RemoveHero(uint playerId)
        {
            int index = Find(playerId);
            if (index < 0)
            {
                return false;
            }

            StopMovement(playerId);
            int pulled = Find(_hooks[index].TargetId);
            if (_hooks[index].Phase == HookPhase.Pulling && pulled >= 0)
            {
                _heroes[pulled].IsPulled = false;
                _heroes[pulled].MoveTarget = _heroes[pulled].Pos;
            }
            for (int i = 0; i < _hooks.Length; i++)
            {
                if (_hooks[i].Phase == HookPhase.Pulling && _hooks[i].TargetId == playerId)
                {
                    HookSimulation.Turn(ref _hooks[i], HookPhase.Retracting, Time);
                    _hooks[i].TargetId = 0;
                }
            }
            Array.Copy(_heroes, index + 1, _heroes, index, _heroes.Length - index - 1);
            Array.Copy(_hooks, index + 1, _hooks, index, _hooks.Length - index - 1);
            Array.Resize(ref _heroes, _heroes.Length - 1);
            Array.Resize(ref _hooks, _hooks.Length - 1);
            return true;
        }

        public void Step(float dt) => Step(dt, Time);

        public void Step(float dt, double stepStartTime)
        {
            if (!math.isfinite(dt) || dt <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dt));
            }

            if (!math.isfinite(stepStartTime) || stepStartTime < Time)
            {
                throw new ArgumentOutOfRangeException(nameof(stepStartTime));
            }

            Time = stepStartTime;
            TickCount++;
            _throws.Clear();
            _hits.Clear();
            _clashes.Clear();
            _damage.Clear();
            _deaths.Clear();
            if (Rules != null)
            {
                Rules.Advance(Time, CountTeam(Team.A), CountTeam(Team.B));
                if (Rules.Phase != MatchPhase.Playing)
                {
                    ResolveHeroOverlaps();
                    _intents.Clear();
                    Time += dt;
                    return;
                }
                RespawnHeroes();
            }
            while (_intents.Count > 0)
            {
                Intent intent = _intents.Dequeue();
                int index = Find(intent.Id);
                if (index < 0 || !math.all(math.isfinite(intent.Value)) ||
                    !_heroes[index].Alive || _heroes[index].IsPulled)
                {
                    continue;
                }

                if (intent.Throw)
                {
                    if (HookSimulation.TryThrow(ref _hooks[index], _heroes[index], intent.Value, Time))
                    {
                        _throws.Add(new HookThrown(intent.Id, _heroes[index].Pos, _hooks[index].Dir, Time));
                    }
                }
                else
                {
                    _heroes[index].MoveTarget = Geo2D.ClampToRect(intent.Value,
                        ArenaBounds.ForPosition(_arena, _heroes[index].Pos, _heroes[index].Radius));
                }
            }
            ResolveHeroOverlaps();
            for (int i = 0; i < _heroes.Length; i++)
            {
                HeroState previous = _heroes[i];
                HeroMovement.Step(ref _heroes[i], _heroConfig.MoveSpeed,
                    ArenaBounds.ForPosition(_arena, _heroes[i].Pos, _heroes[i].Radius), dt);
                _heroes[i].Pos = HeroCollision.Move(previous, _heroes[i].Pos, _heroes);
            }
            Time += dt;
            for (int i = 0; i < _hooks.Length; i++)
            {
                if (_hooks[i].Phase != HookPhase.Extending)
                {
                    StepHook(i, dt);
                }
            }

            StepFlyingHooks(dt);
            ApplyTerritoryDamage();
            if (Rules != null)
            {
                ApplyMatchRules();
            }

            ResolveHeroOverlaps();
        }

        private void StepHook(int index, float dt)
        {
            ref HookState hook = ref _hooks[index];
            if (hook.Phase == HookPhase.Idle)
            {
                return;
            }

            int pulled = hook.Phase == HookPhase.Pulling ? Find(hook.TargetId) : -1;
            var config = new HookConfigData(hook.Range, hook.Speed, hook.Radius, hook.Damage, _hookConfig.Cooldown, 0, 0, 0, 0, 4);
            bool finished = HookSimulation.Return(ref hook, _heroes[index], config, dt);
            if (pulled >= 0)
            {
                float2 desired = hook.Tip;
                float2 position = HeroCollision.Move(_heroes[pulled], desired, _heroes);
                bool blocked = math.distancesq(position, desired) > 1e-10f;
                _heroes[pulled].Pos = position;
                hook.Tip = position;
                if (blocked || finished)
                {
                    _heroes[pulled].IsPulled = false;
                    _heroes[pulled].MoveTarget = position;
                    hook.Origin = position;
                    hook.Length = 0;
                    hook.StartTime = Time;
                    hook.TargetId = 0;
                    HookSimulation.Turn(ref hook, HookPhase.Retracting, Time);
                    return;
                }
            }
            if (!finished)
            {
                return;
            }

            if (pulled >= 0)
            {
                _heroes[pulled].IsPulled = false;
                _heroes[pulled].MoveTarget = _heroes[pulled].Pos;
            }
            hook.Phase = HookPhase.Idle;
            hook.TargetId = 0;
            hook.Length = 0;
            _heroes[index].HookCooldownEnd = Time + _hookConfig.Cooldown;
        }

        private int Find(uint id)
        {
            for (int i = 0; i < _heroes.Length; i++)
            {
                if (_heroes[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
