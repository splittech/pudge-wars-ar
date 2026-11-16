using System;
using Game.Core.Configs;
using Game.Core.Match;
using Mirror;
using R3;
using VContainer;

namespace Game.Net
{
    public sealed class MatchStateNet : NetworkBehaviour
    {
        public readonly SyncList<PlayerInfo> Players = new();
        public readonly SyncList<PlayerMatchStats> Statistics = new();
        [SyncVar(hook = nameof(OnStateChanged))] private MatchSummary _summary;
        private readonly ReactiveProperty<MatchSummary> _state = new();
        private readonly ReactiveProperty<int> _contentRevision = new();
        private MatchSimulation _simulation;
        private PudgeNetworkManager _manager;
        private ServerPlayerCommands _commands;
        public static MatchStateNet Active { get; private set; }
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive() => Active = null;
        public MatchSummary Summary => _summary;
        public ReadOnlyReactiveProperty<MatchSummary> State => _state;
        public ReadOnlyReactiveProperty<int> ContentRevision => _contentRevision;
        public MatchEventBus Events { get; } = new();
        public bool IsHost => isServer && isClient;
        public System.Collections.Generic.IEnumerable<PlayerInfo> PlayerRows => Players;
        public System.Collections.Generic.IEnumerable<PlayerMatchStats> StatRows => Statistics;
        public bool CanStart
        {
            get
            {
                int a = 0, b = 0;
                foreach (var player in Players)
                {
                    if (!player.Connected || !player.Ready) return false;
                    if (player.Team == Team.A) a++; else b++;
                }
                return _summary.Phase == MatchPhase.Lobby && a > 0 && b > 0;
            }
        }

        internal void BindRuntime(MatchSimulation simulation, PudgeNetworkManager manager, ServerPlayerCommands commands)
        {
            _simulation = simulation; _manager = manager; _commands = commands;
            Active = this;
            Publish();
        }

        internal void Publish()
        {
            var rules = _simulation?.Rules;
            if (rules == null) return;
            _summary = new MatchSummary { Phase = rules.Phase, CountdownEnd = rules.CountdownEnd,
                MatchEnd = rules.MatchEnd, ScoreA = rules.ScoreA, ScoreB = rules.ScoreB, Winner = rules.Winner };
            _state.Value = _summary;
            for (int i = 0; i < _simulation.HeroCount; i++)
            {
                var hero = _simulation.GetHero(i);
                var stats = new PlayerMatchStats { PlayerId = hero.Id, Team = hero.Team,
                    Kills = hero.Kills, Deaths = hero.Deaths, Assists = hero.Assists, HookHits = hero.HookHits };
                foreach (var info in Players) if (info.SessionPlayerId == hero.Id) stats.Nickname = info.Nickname;
                int index = -1;
                for (int j = 0; j < Statistics.Count; j++) if (Statistics[j].PlayerId == hero.Id) { index = j; break; }
                if (index < 0) Statistics.Add(stats);
                else if (!Statistics[index].Equals(stats)) Statistics[index] = stats;
            }
        }

        private void Update()
        {
            if (isServer) Publish();
        }

        [Server]
        internal bool LobbyAction(uint id, byte action, Team team, bool value, bool host)
        {
            var rules = _simulation?.Rules;
            if (rules == null) return false;
            bool accepted = false;
            if (action == 0 && rules.Phase == MatchPhase.Lobby)
                accepted = _manager.Roster.SetTeam(id, team) && _simulation.SetTeam(id, team);
            else if (action == 1 && rules.Phase == MatchPhase.Lobby)
                accepted = _manager.Roster.SetReady(id, value);
            else if (action == 2)
            {
                accepted = rules.Begin(NetworkTime.time, host, CanStart,
                    _simulation.CountTeam(Team.A), _simulation.CountTeam(Team.B));
                if (accepted) rules.ArenaPlaced(NetworkTime.time);
            }
            else if (action == 3 && rules.Phase == MatchPhase.Placement)
                accepted = rules.ArenaPlaced(NetworkTime.time);
            else if (action == 4)
                accepted = rules.Rematch(NetworkTime.time, host,
                    _simulation.CountTeam(Team.A), _simulation.CountTeam(Team.B));
            else if (action == 5)
                accepted = rules.ReturnToLobby(host);
            if (!accepted) return false;
            if (action == 2 || action == 4 || action == 5)
            {
                _simulation.ResetRound();
                Statistics.Clear();
                _manager.Roster.ClearReady();
                foreach (var identity in NetworkServer.spawned.Values)
                    if (identity.TryGetComponent<HeroNet>(out var hero))
                    {
                        _commands.Remove(hero.SessionPlayerId);
                        hero.ResetRoundEpoch(_manager.Roster.AdvanceEpoch(hero.SessionPlayerId));
                    }
            }
            Publish();
            return true;
        }

        [ClientRpc]
        internal void RpcKill(KillNotice notice) => Events.Publish(notice);

        private void OnStateChanged(MatchSummary previous, MatchSummary current) => _state.Value = current;

        private void OnPlayersChanged(SyncList<PlayerInfo>.Operation operation, int index, PlayerInfo value)
            => _contentRevision.Value++;

        private void OnStatisticsChanged(SyncList<PlayerMatchStats>.Operation operation, int index, PlayerMatchStats value)
            => _contentRevision.Value++;

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            Players.OnChange -= OnPlayersChanged;
            Statistics.OnChange -= OnStatisticsChanged;
            _state.Dispose(); _contentRevision.Dispose(); Events.Dispose();
        }
        public bool HasDependencies { get; private set; }
        public bool ClientStartedAfterInjection { get; private set; }

        [Inject]
        public void Construct(MatchConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            HasDependencies = true;
        }

        public override void OnStartServer() => RequireDependencies();

        public override void OnStartClient()
        {
            RequireDependencies();
            ClientStartedAfterInjection = true;
            Active = this;
            Players.OnChange += OnPlayersChanged;
            Statistics.OnChange += OnStatisticsChanged;
            _state.Value = _summary;
        }

        private void RequireDependencies()
        {
            if (!HasDependencies)
                throw new InvalidOperationException("MatchStateNet must be created through NetworkPrefabFactory before network callbacks.");
        }
    }
}
