using System.Collections.Generic;
using System;
using Game.Core.Commands;
using Game.Core.Configs;
using Game.Core.Geometry;
using Game.Core.Hero;
using Game.Core.Hook;
using Game.Core.Match;
using Unity.Mathematics;

namespace Game.Net
{
    public sealed class ServerPlayerCommands
    {
        private sealed class Pending
        {
            public double WindowStart;
            public int Moves;
            public int Throws;
            public bool HasMove;
            public bool HasThrow;
            public float2 Move;
            public float2 Direction;
            public uint RequestId;
            public uint SessionEpoch;
            public bool Submitted;
        }

        private readonly MatchSimulation _simulation;
        private readonly ArenaConfigData _arena;
        private readonly Dictionary<uint, Pending> _pending = new(4);
        public event Action<uint, uint, uint, bool, string> ThrowCompleted;

        public ServerPlayerCommands(MatchSimulation simulation, ArenaConfig arena)
            : this(simulation, arena.ToData()) { }

        public ServerPlayerCommands(MatchSimulation simulation, ArenaConfigData arena)
        {
            _simulation = simulation;
            _arena = arena;
        }

        public bool TryMove(uint id, float2 point, double now, bool ownsHero, out string reason)
        {
            if (!Validate(id, point, now, ownsHero, out HeroState hero, out reason))
                return false;
            Pending pending = GetPending(id, now);
            if (++pending.Moves > 30)
                return Reject("Move rate exceeded", out reason);
            pending.Move = Geo2D.ClampToRect(point, ArenaBounds.ForTeam(_arena, hero.Team, hero.Radius));
            pending.HasMove = true;
            return true;
        }

        public bool TryThrow(uint id, float2 direction, double now, bool ownsHero, out string reason)
            => TryThrow(id, direction, now, ownsHero, 0, 0, out reason);

        public bool TryThrow(uint id, float2 direction, double now, bool ownsHero,
            uint requestId, uint epoch, out string reason)
        {
            if (!Validate(id, direction, now, ownsHero, out HeroState hero, out reason))
                return false;
            float lengthSquared = math.lengthsq(direction);
            if (!math.isfinite(lengthSquared) || lengthSquared < 1e-12f)
                return Reject("Invalid direction", out reason);
            Pending pending = GetPending(id, now);
            if (++pending.Throws > 10)
                return Reject("Throw rate exceeded", out reason);
            if (pending.HasThrow || pending.Submitted)
                return Reject("Throw already pending", out reason);
            for (int i = 0; i < _simulation.HeroCount; i++)
                if (_simulation.GetHero(i).Id == id && _simulation.GetHook(i).Phase != HookPhase.Idle)
                    return Reject("Hook is busy", out reason);
            if (now < hero.HookCooldownEnd)
                return Reject("Hook cooldown", out reason);
            pending.Direction = math.normalize(direction);
            pending.HasThrow = true;
            pending.RequestId = requestId;
            pending.SessionEpoch = epoch;
            return true;
        }

        public void Flush(double now)
        {
            foreach (var entry in _pending)
            {
                Pending pending = entry.Value;
                if (pending.HasMove)
                    _simulation.Enqueue(new MoveIntent(entry.Key, pending.Move));
                if (pending.HasThrow)
                {
                    _simulation.Enqueue(new ThrowHookIntent(entry.Key, pending.Direction, now));
                    pending.Submitted = true;
                }
                pending.HasMove = false;
                pending.HasThrow = false;
            }
        }

        public void CompleteStep()
        {
            foreach (var entry in _pending)
            {
                Pending pending = entry.Value;
                if (!pending.Submitted)
                    continue;
                pending.Submitted = false;
                bool accepted = false;
                foreach (var thrown in _simulation.Throws)
                    if (thrown.OwnerId == entry.Key)
                        accepted = true;
                ThrowCompleted?.Invoke(entry.Key, pending.RequestId, pending.SessionEpoch, accepted,
                    accepted ? string.Empty : "Hero state changed before simulation step");
            }
        }

        public void Remove(uint id) => _pending.Remove(id);

        private bool Validate(uint id, float2 value, double now, bool ownsHero,
            out HeroState hero, out string reason)
        {
            hero = default;
            reason = string.Empty;
            if (!ownsHero)
                return Reject("Sender does not own hero", out reason);
            if (!math.isfinite(now) || !math.all(math.isfinite(value)))
                return Reject("Non-finite input", out reason);
            if (!_simulation.TryGetHero(id, out hero))
                return Reject("Hero is not registered", out reason);
            if (!hero.Alive || hero.IsPulled)
                return Reject("Hero cannot act", out reason);
            return true;
        }

        private Pending GetPending(uint id, double now)
        {
            if (!_pending.TryGetValue(id, out Pending pending))
            {
                pending = new Pending { WindowStart = now };
                _pending.Add(id, pending);
            }
            if (now - pending.WindowStart >= 1)
            {
                pending.WindowStart = now;
                pending.Moves = 0;
                pending.Throws = 0;
            }
            return pending;
        }

        private static bool Reject(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
