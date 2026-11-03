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
    public sealed class MatchSimulation
    {
        private readonly HeroState[] _heroes;
        private readonly HookState[] _hooks;
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
            public Intent(uint id, float2 value, bool isThrow) { Id = id; Value = value; Throw = isThrow; }
        }

        public double Time { get; private set; }
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
            if (heroes == null || heroes.Length == 0 || !math.isfinite(initialTime))
                throw new ArgumentException("Heroes and a finite initial timestamp are required.");
            if (!math.isfinite(hookConfig.Speed) || hookConfig.Speed <= 0 ||
                !math.isfinite(hookConfig.Range) || hookConfig.Range <= 0 ||
                !math.isfinite(hookConfig.Radius) || hookConfig.Radius < 0 ||
                !math.isfinite(hookConfig.Cooldown) || hookConfig.Cooldown < 0 ||
                !math.isfinite(heroConfig.MoveSpeed) || heroConfig.MoveSpeed < 0)
                throw new ArgumentException("Simulation configuration is invalid.");
            _arena = arena;
            _heroConfig = heroConfig;
            _hookConfig = hookConfig;
            _heroes = (HeroState[])heroes.Clone();
            _hooks = new HookState[heroes.Length];
            Time = initialTime;
            for (int i = 0; i < heroes.Length; i++)
            {
                if (_heroes[i].Id == 0 || !math.all(math.isfinite(_heroes[i].Pos)))
                    throw new ArgumentException("Heroes require unique nonzero IDs and finite positions.");
                for (int j = 0; j < i; j++)
                    if (_heroes[i].Id == _heroes[j].Id)
                        throw new ArgumentException("Duplicate hero ID.");
                var bounds = ArenaBounds.ForTeam(arena, _heroes[i].Team, _heroes[i].Radius);
                _heroes[i].Pos = Geo2D.ClampToRect(_heroes[i].Pos, bounds);
                _heroes[i].MoveTarget = _heroes[i].Pos;
                _hooks[i].OwnerId = _heroes[i].Id;
            }
        }

        public void Enqueue(MoveIntent intent) => _intents.Enqueue(new Intent(intent.PlayerId, intent.ArenaPoint, false));
        public void Enqueue(ThrowHookIntent intent) => _intents.Enqueue(new Intent(intent.PlayerId, intent.Direction, true));

        public void Step(float dt)
        {
            if (!math.isfinite(dt) || dt <= 0)
                throw new ArgumentOutOfRangeException(nameof(dt));
            _throws.Clear(); _hits.Clear(); _clashes.Clear(); _damage.Clear(); _deaths.Clear();
            while (_intents.Count > 0)
            {
                Intent intent = _intents.Dequeue();
                int index = Find(intent.Id);
                if (index < 0 || !math.all(math.isfinite(intent.Value)) ||
                    !_heroes[index].Alive || _heroes[index].IsPulled)
                    continue;
                if (intent.Throw)
                {
                    if (HookSimulation.TryThrow(ref _hooks[index], _heroes[index], intent.Value, Time))
                        _throws.Add(new HookThrown(intent.Id, _heroes[index].Pos, _hooks[index].Dir, Time));
                }
                else
                    _heroes[index].MoveTarget = Geo2D.ClampToRect(intent.Value,
                        ArenaBounds.ForTeam(_arena, _heroes[index].Team, _heroes[index].Radius));
            }
            for (int i = 0; i < _heroes.Length; i++)
                HeroMovement.Step(ref _heroes[i], _heroConfig.MoveSpeed,
                    ArenaBounds.ForTeam(_arena, _heroes[i].Team, _heroes[i].Radius), dt);
            Time += dt;
            for (int i = 0; i < _hooks.Length; i++)
                StepHook(i, dt);
            for (int i = 0; i < _hooks.Length; i++)
                for (int j = i + 1; j < _hooks.Length; j++)
                    if (_hooks[i].Phase == HookPhase.Extending && _hooks[j].Phase == HookPhase.Extending &&
                        Geo2D.CircleCircle(_hooks[i].Tip, _hookConfig.Radius, _hooks[j].Tip, _hookConfig.Radius))
                    {
                        HookSimulation.Turn(ref _hooks[i], HookPhase.Retracting, Time);
                        HookSimulation.Turn(ref _hooks[j], HookPhase.Retracting, Time);
                        _clashes.Add(new HookClash(_hooks[i].OwnerId, _hooks[j].OwnerId,
                            (_hooks[i].Tip + _hooks[j].Tip) * 0.5f, Time));
                    }
            foreach (HookHit hit in _hits)
            {
                int target = Find(hit.TargetId);
                int owner = Find(hit.OwnerId);
                int damage = Combat.Combat.ApplyHit(ref _heroes[target], _heroes[owner], _hookConfig.Damage);
                if (damage <= 0)
                    continue;
                _damage.Add(new HeroDamaged(hit.TargetId, hit.OwnerId, damage, _heroes[target].Hp, Time));
                if (!_heroes[target].Alive)
                    _deaths.Add(new HeroDied(hit.TargetId, hit.OwnerId, Time));
            }
        }

        private void StepHook(int index, float dt)
        {
            ref HookState hook = ref _hooks[index];
            if (hook.Phase == HookPhase.Idle)
                return;
            if (hook.Phase == HookPhase.Extending)
            {
                float nextLength = math.min(_hookConfig.Range, hook.Length + _hookConfig.Speed * dt);
                float2 nextTip = hook.Origin + hook.Dir * nextLength;
                int target = -1;
                float firstT = float.PositiveInfinity;
                for (int j = 0; j < _heroes.Length; j++)
                {
                    if (j == index || !_heroes[j].Alive || _heroes[j].IsPulled)
                        continue;
                    if (Geo2D.SweptCircle(hook.Tip, nextTip, _hookConfig.Radius,
                        _heroes[j].Pos, _heroes[j].Radius, out float t) && t < firstT)
                    { firstT = t; target = j; }
                }
                if (target >= 0)
                {
                    hook.Tip = math.lerp(hook.Tip, nextTip, firstT);
                    hook.Length = math.distance(hook.Origin, hook.Tip);
                    hook.TargetId = _heroes[target].Id;
                    HookSimulation.Turn(ref hook, HookPhase.Pulling, Time);
                    _heroes[target].IsPulled = true;
                    _heroes[target].Pos = hook.Tip;
                    _hits.Add(new HookHit(hook.OwnerId, hook.TargetId, hook.Tip, Time));
                    return;
                }
                hook.Tip = nextTip;
                hook.Length = nextLength;
                if (nextLength >= _hookConfig.Range)
                    HookSimulation.Turn(ref hook, HookPhase.Retracting, Time);
                return;
            }

            int pulled = hook.Phase == HookPhase.Pulling ? Find(hook.TargetId) : -1;
            bool finished = HookSimulation.Return(ref hook, _heroes[index], _hookConfig, dt);
            if (pulled >= 0)
                _heroes[pulled].Pos = hook.Tip;
            if (!finished)
                return;
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
                if (_heroes[i].Id == id)
                    return i;
            return -1;
        }
    }
}
