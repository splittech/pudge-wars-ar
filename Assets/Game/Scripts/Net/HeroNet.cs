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
        private uint _serverHookRequestId;
        private uint _serverHookEpoch;
        private readonly ReactiveProperty<int> _health = new();
        private readonly Subject<Vector2> _hookHits = new();
        private readonly Subject<Vector2> _hookClashes = new();
        private ArenaConfigData _arena;
        private LocalPlayerCommands _localCommands;
        private ServerPlayerCommands _serverCommands;

        public uint SessionPlayerId => _sessionPlayerId;
        public int Slot => _slot;
        public Team Team => _team;
        public int Hp => _hp;
        public ReadOnlyReactiveProperty<int> Health => _health;
        public HookNetState HookState => _hookState;
        public uint SessionEpoch => _sessionEpoch;
        public Observable<Vector2> HookHits => _hookHits;
        public Observable<Vector2> HookClashes => _hookClashes;
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
            _localCommands.Bind(this);
        }

        public override void OnStopLocalPlayer()
        {
            _prediction.Reset(0);
            _localCommands.Unbind(this);
        }

        public void MoveTo(float2 point)
        {
            if (isLocalPlayer && NetworkClient.ready)
                CmdMoveTo(new Vector2(point.x, point.y));
        }

        public void ThrowHook(float2 direction)
        {
            if (!isLocalPlayer || !NetworkClient.ready || !_alive || _isPulled
                || _hookState.Phase != Game.Core.Hook.HookPhase.Idle
                || NetworkTime.time < _hookCooldownEnd)
                return;
            Vector3 origin = transform.localPosition;
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
            // Purchases are introduced with match economy in stage 3.
        }

        [Command(requiresAuthority = false)]
        private void CmdMoveTo(Vector2 point, NetworkConnectionToClient sender = null)
        {
            if (!_serverCommands.TryMove(_sessionPlayerId, new float2(point.x, point.y),
                NetworkTime.time, OwnsHero(sender), out string reason))
                LogRejected("Move", reason);
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
                TargetActionResult(sender, requestId, epoch, false, reason);
                return;
            }
            if (!_serverCommands.TryThrow(_sessionPlayerId, new float2(direction.x, direction.y),
                NetworkTime.time, true, requestId, epoch, out reason))
            {
                LogRejected("Throw", reason);
                TargetActionResult(sender, requestId, epoch, false, reason);
            }
        }

        [TargetRpc]
        private void TargetActionResult(NetworkConnectionToClient target, uint requestId,
            uint epoch, bool accepted, string reason)
        {
            _prediction.ApplyResult(requestId, epoch, accepted);
            if (!accepted && Debug.isDebugBuild)
                Debug.Log($"Throw request {requestId} rejected: {reason}");
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
                TargetActionResult(connectionToClient, requestId, epoch, accepted, reason);
            if (!accepted)
                LogRejected("Throw", reason);
        }

        private void OnSessionEpochChanged(uint previous, uint current) => _prediction.Reset(current);

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
            _slot = slot;
            _team = team;
            transform.localPosition = new Vector3(points[index].x, 0f, points[index].y);
        }

        public override void OnStartServer()
        {
            RequireDependencies();
            _serverCommands.ThrowCompleted += OnThrowCompleted;
        }

        public override void OnStopServer() => _serverCommands.ThrowCompleted -= OnThrowCompleted;

        internal void ApplyServerState(HeroState state)
        {
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
        }

        private void RequireDependencies()
        {
            if (!HasDependencies)
                throw new InvalidOperationException("HeroNet must be created through NetworkPrefabFactory before network callbacks.");
        }
    }
}
