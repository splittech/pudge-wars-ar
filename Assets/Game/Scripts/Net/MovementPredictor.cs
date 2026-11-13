using System;
using Game.Core.Geometry;
using Game.Core.Hero;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Net
{
    public sealed class MovementPredictor
    {
        public const int Capacity = 120;
        public const float Delta = 1f / 30f;
        private struct Entry { public uint Tick; public MovementFrame Frame; public HeroState State; }
        private readonly Entry[] _history = new Entry[Capacity];
        private HeroState _state;
        private Rect2 _bounds;
        private float _speed;
        private uint _epoch;
        private uint _sequence;
        private uint _targetSequence;
        private bool _pendingTarget;
        private float2 _target;
        private bool _initialized;
        public uint Tick { get; private set; }
        public uint LastServerTick { get; private set; }
        public uint AckSequence { get; private set; }
        public int HistorySize { get; private set; }
        public int Corrections { get; private set; }
        public int Resyncs { get; private set; }
        public float Error { get; private set; }
        public bool Ready => _initialized;
        public bool CanMove => _state.Alive && !_state.IsPulled;
        public Vector2 Position => new(_state.Pos.x, _state.Pos.y);
        public Vector2 Target => new(_state.MoveTarget.x, _state.MoveTarget.y);
        public Vector2 AuthoritativePosition { get; private set; }
        public double ServerTime { get; private set; }

        public void Reset(uint epoch, Rect2 bounds)
        {
            _epoch = epoch; _bounds = bounds; _sequence = 0; Tick = 0;
            LastServerTick = 0; AckSequence = 0; HistorySize = 0;
            _pendingTarget = false; _initialized = false; Error = 0;
            Corrections = 0; Resyncs = 0; Array.Clear(_history, 0, Capacity);
        }

        public bool Move(Vector2 point)
        {
            if (!_initialized || !_state.Alive || _state.IsPulled || !SessionSerialization.Finite(point)) return false;
            _target = Geo2D.ClampToRect(new float2(point.x, point.y), _bounds);
            _pendingTarget = true; _targetSequence = 0;
            _state.MoveTarget = _target;
            return true;
        }

        public void Suspend(bool alive, bool pulled)
        {
            _state.Alive = alive; _state.IsPulled = pulled;
            _state.MoveTarget = _state.Pos; _pendingTarget = false;
        }

        public bool Step(out MovementFrame frame, bool predict = true)
        {
            frame = default;
            if (!_initialized || _sequence == uint.MaxValue || Tick == uint.MaxValue) return false;
            frame = new MovementFrame { Sequence = ++_sequence, ClientTick = ++Tick, SessionEpoch = _epoch,
                HasMoveTarget = _pendingTarget, Target = new Vector2(_target.x, _target.y) };
            if (_pendingTarget && _targetSequence == 0) _targetSequence = frame.Sequence;
            if (predict) HeroMovement.Step(ref _state, _speed, _bounds, Delta);
            _history[Tick % Capacity] = new Entry { Tick = Tick, Frame = frame, State = _state };
            HistorySize = Math.Min(Capacity, HistorySize + 1);
            return true;
        }

        public bool Receive(MovementSnapshot snapshot, bool predict = true)
        {
            if (snapshot.SessionEpoch != _epoch || (_initialized && snapshot.ServerTick <= LastServerTick)
                || !SessionSerialization.Finite(snapshot.Pos) || !SessionSerialization.Finite(snapshot.MoveTarget)
                || !SessionSerialization.TimeValid(snapshot.ServerTime) || !float.IsFinite(snapshot.MoveSpeed)
                || snapshot.MoveSpeed < 0 || snapshot.AckSequence > _sequence) return false;
            bool forced = !snapshot.Alive || snapshot.IsPulled || (_initialized
                && (_state.Alive != snapshot.Alive || _state.IsPulled != snapshot.IsPulled));
            bool found = _initialized && snapshot.ServerTick <= Tick && Tick - snapshot.ServerTick < Capacity
                && _history[snapshot.ServerTick % Capacity].Tick == snapshot.ServerTick;
            Error = found ? math.distance(_history[snapshot.ServerTick % Capacity].State.Pos,
                new float2(snapshot.Pos.x, snapshot.Pos.y)) : 0;
            if (Error > 0.00001f) Corrections++;
            uint replayEnd = Tick;
            _state.Pos = new float2(snapshot.Pos.x, snapshot.Pos.y);
            _state.MoveTarget = new float2(snapshot.MoveTarget.x, snapshot.MoveTarget.y);
            _state.Alive = snapshot.Alive; _state.IsPulled = snapshot.IsPulled; _speed = snapshot.MoveSpeed;
            AuthoritativePosition = snapshot.Pos; ServerTime = snapshot.ServerTime;
            LastServerTick = snapshot.ServerTick; AckSequence = snapshot.AckSequence;
            if (_pendingTarget && _targetSequence != 0 && AckSequence >= _targetSequence) _pendingTarget = false;
            if (!found || forced)
            {
                if (_initialized && !found) Resyncs++;
                Tick = snapshot.ServerTick; HistorySize = 0; Array.Clear(_history, 0, Capacity);
                if (forced) _pendingTarget = false;
                _targetSequence = 0;
            }
            else
            {
                for (uint tick = snapshot.ServerTick + 1; tick <= replayEnd; tick++)
                {
                    ref Entry entry = ref _history[tick % Capacity];
                    if (entry.Frame.HasMoveTarget && entry.Frame.Sequence > AckSequence)
                        _state.MoveTarget = new float2(entry.Frame.Target.x, entry.Frame.Target.y);
                    if (predict) HeroMovement.Step(ref _state, _speed, _bounds, Delta);
                    entry.State = _state;
                }
                HistorySize = (int)(replayEnd - snapshot.ServerTick);
            }
            if (_pendingTarget) _state.MoveTarget = _target;
            _initialized = true;
            return true;
        }
    }
}
