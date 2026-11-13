using System;
using Game.Core.Configs;
using Game.Core.Match;
using Game.Core.Hero;
using Game.Core.Commands;
using Game.Core.Economy;
using Unity.Mathematics;
using Mirror;
using UnityEngine;
using VContainer;
using R3;

namespace Game.Net
{
    public sealed class HeroNet : NetworkBehaviour, IPlayerCommands
    {
        [SyncVar] private uint _sessionPlayerId;
        [SyncVar] private int _slot = -1;
        [SyncVar] private Team _team;
        [SyncVar(hook = nameof(OnHpChanged))] private int _hp;
        [SyncVar] private bool _alive;
        [SyncVar] private bool _isPulled;
        [SyncVar] private double _hookCooldownEnd;
        [SyncVar] private HookNetState _hookState;
        [SyncVar(hook = nameof(OnSessionEpochChanged))] private uint _sessionEpoch;
        private readonly HookThrowPrediction _prediction = new();
        private readonly ActionRequestSequence _requestSequence = new();
        private readonly ActionRequestSequence _purchaseSequence = new();
        private readonly Subject<ActionResult> _actionResults = new();
        private uint _nextPurchaseRequestId;
        private uint _serverHookRequestId;
        private uint _serverHookEpoch;
        private readonly ReactiveProperty<int> _health = new();
        private readonly Subject<Vector2> _hookHits = new();
        private readonly Subject<Vector2> _hookClashes = new();
        private ArenaConfigData _arena;
        private LocalPlayerCommands _localCommands;
        private ServerPlayerCommands _serverCommands;
        private readonly MovementPredictor _movement = new();
        [SerializeField] private bool _predictionEnabled = true;
        [SerializeField, Range(10, 30)] private int _movementSendRate = 30;
        [SerializeField, Range(10, 30)] private int _movementSnapshotRate = 30;
        private float _moveSpeed;
        private float _heroRadius;
        private MovementBatch _movementBatch;
        private double _nextMovementSend;
        private double _nextMovementSnapshot;
        private double _nextMovementResync;
        private readonly MovementClock _movementClock = new();
        private uint _lastMovementEpoch;
        private bool _lastMovementAlive;
        private bool _lastMovementPulled;
        private HeroState _movementServerState;
        private Vector2 _movementVisualOffset;
        private float _movementOffsetRemaining;
        private int _sentMovementPackets;
        private int _receivedMovementSnapshots;
        private bool _lastPredictionEnabled = true;
        private int _movementFramesProduced;
        private readonly MovementLatencyProbe _inputLatency = new();
        public double LastInputToViewMs { get; private set; } = double.NaN;
        public int InputLatencySamples { get; private set; }
        public event Action<double> MovementPresented;
        private Action<Vector3> _presentedMovementReporter;
        public Action<Vector3> PresentedMovementReporter => _presentedMovementReporter ??= ReportPresentedMovement;
        public int MovementFramesProduced => _movementFramesProduced;
        public uint ObservedServerTick => isServer ? _serverCommands.ServerTick : _movement.LastServerTick;
        public int MovementSendRate => _movementSendRate;
        public int MovementSnapshotRate => _movementSnapshotRate;

        public void ReportPresentedMovement(Vector3 position)
        {
            if (!isLocalPlayer || !_inputLatency.TryPresent(new Vector2(position.x, position.z), _alive && !_isPulled,
                NetworkTime.time, out double milliseconds)) return;
            LastInputToViewMs = milliseconds;
            InputLatencySamples++;
            MovementPresented?.Invoke(LastInputToViewMs);
        }
        public MovementPredictor Movement => _movement;
        public bool PredictionEnabled { get => _predictionEnabled; set => _predictionEnabled = value; }
        public int MovementPackets => _sentMovementPackets;
        public int MovementSnapshots => _receivedMovementSnapshots;
        public bool HasPredictedMovement => isLocalPlayer && !isServer && _movement.Ready;
        public Vector3 DisplayPosition
        {
            get
            {
                if (!HasPredictedMovement) return transform.localPosition;
                Vector2 point = _predictionEnabled ? _movement.Position + _movementVisualOffset : _movement.AuthoritativePosition;
                return new Vector3(point.x, transform.localPosition.y, point.y);
            }
        }

        [Inject]
        public void ConstructMovement(HeroConfig config)
        {
            var data = config.ToData(); _moveSpeed = data.MoveSpeed; _heroRadius = data.Radius;
        }

        private void ResetMovement()
        {
            _movement.Reset(_sessionEpoch, Game.Core.Geometry.ArenaBounds.ForTeam(_arena, _team, _heroRadius));
            _movementBatch = default; _movementVisualOffset = Vector2.zero; _movementOffsetRemaining = 0;
            _nextMovementSend = 0;
            _movementClock.Reset();
            _inputLatency.Reset();
        }

        private void Update()
        {
            if (!isLocalPlayer || !NetworkClient.ready || !_movement.Ready) return;
            if (_lastPredictionEnabled != _predictionEnabled)
            {
                _movementVisualOffset = Vector2.zero; _movementOffsetRemaining = 0;
                _lastPredictionEnabled = _predictionEnabled;
            }
            if (!_alive || _isPulled) _movement.Suspend(_alive, _isPulled);
            if (_movementOffsetRemaining > 0)
            {
                float elapsed = Mathf.Min(Time.unscaledDeltaTime, _movementOffsetRemaining);
                _movementVisualOffset *= 1 - elapsed / _movementOffsetRemaining;
                _movementOffsetRemaining -= elapsed;
            }
            // An independent cadence is essential: snapshots arriving at 30 Hz must not suppress input ticks.
            int steps = _movementClock.Advance(NetworkTime.time);
            for (int i = 0; i < steps; i++)
            {
                if (!_movement.Step(out var frame, !isServer && _predictionEnabled)) break;
                _movementFramesProduced++;
                if (_movementBatch.Count == 4) SendMovement();
                _movementBatch.Add(frame);
            }
            if (_movementClock.DroppedTime)
            {
                CmdRequestMovementSnapshot();
            }
            if (_movementBatch.Count > 0 && NetworkTime.time >= _nextMovementSend) SendMovement();
        }

        private void SendMovement()
        {
            CmdMovement(_movementBatch);
            _sentMovementPackets++;
            _movementBatch = default;
            double interval = 1.0 / Mathf.Clamp(_movementSendRate, 10, 30);
            _nextMovementSend = _nextMovementSend == 0 ? NetworkTime.time + interval
                : Math.Max(_nextMovementSend + interval, NetworkTime.time - interval);
        }

        [Command(requiresAuthority = false, channel = Channels.Unreliable)]
        private void CmdMovement(MovementBatch batch, NetworkConnectionToClient sender = null)
        {
            if (!OwnsHero(sender) || batch.Count == 0 || batch.Count > 4) return;
            for (int i = 0; i < batch.Count; i++)
                _serverCommands.TryMovement(_sessionPlayerId, batch.Get(i), _sessionEpoch, NetworkTime.time, true, out _);
        }

        [Command]
        private void CmdRequestMovementSnapshot()
        {
            if (NetworkTime.time < _nextMovementResync) return;
            _nextMovementResync = NetworkTime.time + 0.5;
            _serverCommands.ResyncMovementClock(_sessionPlayerId);
            _nextMovementSnapshot = 0;
            PublishMovement(_serverCommands.ServerTick,
                _serverCommands.ServerTick == 0 ? NetworkTime.time : _serverCommands.ServerTime, _movementServerState);
        }

        internal void PublishMovement(uint tick, double time, HeroState state)
        {
            if (connectionToClient == null || !NetworkServer.active) return;
            bool forced = _lastMovementEpoch != _sessionEpoch || _lastMovementAlive != state.Alive || _lastMovementPulled != state.IsPulled;
            if (!forced && time < _nextMovementSnapshot) return;
            _lastMovementEpoch = _sessionEpoch; _lastMovementAlive = state.Alive; _lastMovementPulled = state.IsPulled;
            double interval = 1.0 / Mathf.Clamp(_movementSnapshotRate, 10, 30);
            _nextMovementSnapshot = forced || _nextMovementSnapshot == 0 ? time + interval
                : Math.Max(_nextMovementSnapshot + interval, time - interval);
            TargetMovementSnapshot(connectionToClient, new MovementSnapshot
            {
                ServerTick = tick, ServerTime = time, AckSequence = _serverCommands.MovementAck(_sessionPlayerId),
                SessionEpoch = _sessionEpoch, Pos = new Vector2(state.Pos.x, state.Pos.y),
                MoveTarget = new Vector2(state.MoveTarget.x, state.MoveTarget.y), MoveSpeed = _moveSpeed,
                Alive = state.Alive, IsPulled = state.IsPulled
            });
        }

        [TargetRpc(channel = Channels.Unreliable)]
        private void TargetMovementSnapshot(NetworkConnectionToClient target, MovementSnapshot snapshot)
        {
            if (!isLocalPlayer || snapshot.SessionEpoch != _sessionEpoch) return;
            Vector2 previous = _movement.Position + _movementVisualOffset;
            bool wasMoving = _movement.CanMove;
            int resyncs = _movement.Resyncs;
            if (!_movement.Receive(snapshot, !isServer && _predictionEnabled)) return;
            if (_movement.Resyncs != resyncs)
            {
                _movementBatch = default;
                CmdRequestMovementSnapshot();
            }
            _receivedMovementSnapshots++;
            Vector2 correction = previous - _movement.Position;
            bool smooth = _predictionEnabled && wasMoving == _movement.CanMove
                && snapshot.Alive && !snapshot.IsPulled && correction.magnitude <= 1 && _movement.Error <= 1;
            _movementVisualOffset = smooth ? correction : Vector2.zero;
            _movementOffsetRemaining = smooth ? 0.1f : 0;
        }

        public uint SessionPlayerId => _sessionPlayerId;
        public int Slot => _slot;
        public Team Team => _team;
        public int Hp => _hp;
        public ReadOnlyReactiveProperty<int> Health => _health;
        public HookNetState HookState => _hookState;
        public uint SessionEpoch => _sessionEpoch;
        public Observable<Vector2> HookHits => _hookHits;
        public Observable<Vector2> HookClashes => _hookClashes;
        public Observable<ActionResult> ActionResults => _actionResults;
        public bool Alive => _alive;
        public bool IsPulled => _isPulled;
        public double HookCooldownEnd => _hookCooldownEnd;
        public bool IsLocalHero => isLocalPlayer;
        public bool IsClientView => isClient;
        public double SynchronizedTime => NetworkTime.time;
        public bool HasDependencies { get; private set; }
        public bool ClientStartedAfterInjection { get; private set; }

        [Inject]
        public void Construct(ArenaConfig arena)
        {
            _arena = arena.ToData();
            HasDependencies = true;
        }

        [Inject]
        public void ConstructCommands(LocalPlayerCommands localCommands, ServerPlayerCommands serverCommands)
        {
            _localCommands = localCommands;
            _serverCommands = serverCommands;
        }

        public override void OnStartLocalPlayer()
        {
            _prediction.Reset(_sessionEpoch);
            ResetMovement();
            _localCommands.Bind(this);
            CmdRequestMovementSnapshot();
        }

        public override void OnStopLocalPlayer()
        {
            _prediction.Reset(0);
            ResetMovement();
            _localCommands.Unbind(this);
        }

        public void MoveTo(float2 point)
        {
            if (isLocalPlayer && NetworkClient.ready)
            {
                Vector2 previousTarget = _movement.Target;
                if (_movement.Move(new Vector2(point.x, point.y)))
                {
                    Vector3 visible = DisplayPosition;
                    _inputLatency.Begin(new Vector2(visible.x, visible.z), previousTarget, _movement.Target,
                        _alive && !_isPulled, _movementOffsetRemaining <= 0, NetworkTime.time);
                }
            }
        }

        public void ThrowHook(float2 direction)
        {
            if (!isLocalPlayer || !NetworkClient.ready || !_alive || _isPulled
                || _hookState.Phase != Game.Core.Hook.HookPhase.Idle
                || NetworkTime.time < _hookCooldownEnd)
                return;
            Vector3 origin = DisplayPosition;
            Vector2 value = new(direction.x, direction.y);
            if (_prediction.TryBegin(new Vector2(origin.x, origin.z), value, NetworkTime.time, out uint requestId))
                CmdThrowHook(value, requestId, _sessionEpoch);
        }

        public bool IsHookPredicted => isLocalPlayer && _prediction.IsPending;

        public HookNetState DisplayHookState
        {
            get
            {
                if (!isLocalPlayer || !_alive || _isPulled)
                    _prediction.Cancel();
                _prediction.TryGetState(NetworkTime.time, _hookState, out HookNetState state);
                return state;
            }
        }

        public void BuyUpgrade(UpgradeId id)
        {
            if (isLocalPlayer && NetworkClient.ready && _nextPurchaseRequestId != uint.MaxValue)
                CmdBuyUpgrade(id, ++_nextPurchaseRequestId, _sessionEpoch);
        }

        [Command(requiresAuthority = false)]
        private void CmdBuyUpgrade(UpgradeId id, uint requestId, uint epoch,
            NetworkConnectionToClient sender = null)
        {
            if (!OwnsHero(sender))
            {
                LogRejected("BuyUpgrade", "Sender does not own hero");
                return;
            }
            string reason;
            if (_purchaseSequence.TryConsume(requestId, epoch, out reason))
                reason = id < UpgradeId.Range || id > UpgradeId.Damage
                    ? "Unknown upgrade" : "Purchases are not available until match economy is enabled";
            TargetActionResult(sender, ActionKind.BuyUpgrade, requestId, epoch, false, reason);
        }

        [Command(requiresAuthority = false)]
        private void CmdThrowHook(Vector2 direction, uint requestId, uint epoch,
            NetworkConnectionToClient sender = null)
        {
            if (!OwnsHero(sender))
            {
                LogRejected("Throw", "Sender does not own hero");
                return;
            }
            if (!_requestSequence.TryConsume(requestId, epoch, out string reason))
            {
                LogRejected("Throw", reason);
                TargetActionResult(sender, ActionKind.Throw, requestId, epoch, false, reason);
                return;
            }
            if (!_serverCommands.TryThrow(_sessionPlayerId, new float2(direction.x, direction.y),
                NetworkTime.time, true, requestId, epoch, out reason))
            {
                LogRejected("Throw", reason);
                TargetActionResult(sender, ActionKind.Throw, requestId, epoch, false, reason);
            }
        }

        [TargetRpc]
        private void TargetActionResult(NetworkConnectionToClient target, ActionKind kind, uint requestId,
            uint epoch, bool accepted, string reason)
        {
            if (!isLocalPlayer || epoch != _sessionEpoch)
                return;
            if (kind == ActionKind.Throw)
                _prediction.ApplyResult(requestId, epoch, accepted);
            _actionResults.OnNext(new ActionResult(kind, requestId, epoch, accepted, reason));
            if (!accepted && Debug.isDebugBuild)
                Debug.Log($"{kind} request {requestId} rejected: {reason}");
        }

        private void OnThrowCompleted(uint playerId, uint requestId, uint epoch, bool accepted, string reason)
        {
            if (playerId != _sessionPlayerId)
                return;
            if (accepted)
            {
                _serverHookRequestId = requestId;
                _serverHookEpoch = epoch == 0 ? _sessionEpoch : epoch;
            }
            if (requestId != 0 && connectionToClient != null)
                TargetActionResult(connectionToClient, ActionKind.Throw, requestId, epoch, accepted, reason);
            if (!accepted)
                LogRejected("Throw", reason);
        }

        private void OnSessionEpochChanged(uint previous, uint current)
        {
            _prediction.Reset(current);
            if (isLocalPlayer) ResetMovement();
            _nextPurchaseRequestId = 0;
        }

        private bool OwnsHero(NetworkConnectionToClient sender) => sender != null
            && sender.isAuthenticated && sender.isReady && sender == connectionToClient
            && sender.identity == netIdentity;

        private void LogRejected(string action, string reason)
        {
            if (Debug.isDebugBuild)
                Debug.LogWarning($"{action} rejected for player {_sessionPlayerId}: {reason}");
        }

        internal void InitializePlayer(uint sessionPlayerId, int slot)
        {
            RequireDependencies();
            if (sessionPlayerId == 0 || slot < 0 || slot >= 4)
                throw new ArgumentOutOfRangeException(nameof(slot), "A nonzero session ID and a slot in 0..3 are required.");

            Team team = slot % 2 == 0 ? Team.A : Team.B;
            var points = team == Team.A ? _arena.TeamASpawnPoints : _arena.TeamBSpawnPoints;
            int index = slot / 2;
            if (index >= points.Count)
                throw new InvalidOperationException("ArenaConfig does not contain enough spawn points.");

            _sessionPlayerId = sessionPlayerId;
            _sessionEpoch = 1;
            _serverHookEpoch = _sessionEpoch;
            _requestSequence.Reset(_sessionEpoch);
            _purchaseSequence.Reset(_sessionEpoch);
            _slot = slot;
            _team = team;
            transform.localPosition = new Vector3(points[index].x, 0f, points[index].y);
        }

        internal void RestoreSession(uint epoch)
        {
            _sessionEpoch = epoch;
            _serverHookEpoch = epoch;
            _serverHookRequestId = 0;
            _requestSequence.Reset(epoch);
            _purchaseSequence.Reset(epoch);
            _prediction.Reset(epoch);
            _nextPurchaseRequestId = 0;
        }

        public override void OnStartServer()
        {
            RequireDependencies();
            _serverCommands.ThrowCompleted += OnThrowCompleted;
        }

        public override void OnStopServer() => _serverCommands.ThrowCompleted -= OnThrowCompleted;

        internal void ApplyServerState(HeroState state)
        {
            _movementServerState = state;
            if (state.Id != _sessionPlayerId)
                throw new ArgumentException("Simulation player ID does not match the network hero.", nameof(state));
            Vector3 position = transform.localPosition;
            transform.localPosition = new Vector3(state.Pos.x, position.y, state.Pos.y);
            _hp = state.Hp;
            _health.Value = state.Hp;
            _alive = state.Alive;
            _isPulled = state.IsPulled;
            _hookCooldownEnd = state.HookCooldownEnd;
        }

        public override void OnStartClient()
        {
            RequireDependencies();
            ClientStartedAfterInjection = true;
            _health.Value = _hp;
            gameObject.name = $"Hero {_sessionPlayerId} ({_team})";
        }

        internal void ApplyServerHook(HookNetState state)
        {
            state.RequestId = _serverHookRequestId;
            state.SessionEpoch = _serverHookEpoch;
            if (_hookState.Phase != state.Phase || _hookState.StartTime != state.StartTime
                || _hookState.TargetNetId != state.TargetNetId || _hookState.RequestId != state.RequestId
                || _hookState.SessionEpoch != state.SessionEpoch)
                _hookState = state;
        }

        private void OnHpChanged(int previous, int current) => _health.Value = current;

        [ClientRpc]
        internal void RpcHookHit(Vector2 arenaPosition) => _hookHits.OnNext(arenaPosition);

        [ClientRpc]
        internal void RpcHookClash(Vector2 arenaPosition) => _hookClashes.OnNext(arenaPosition);

        private void OnDestroy()
        {
            _health.Dispose();
            _hookHits.Dispose();
            _hookClashes.Dispose();
            _actionResults.Dispose();
        }

        private void RequireDependencies()
        {
            if (!HasDependencies)
                throw new InvalidOperationException("HeroNet must be created through NetworkPrefabFactory before network callbacks.");
        }
    }
}
