using Game.Core.Match;
using Mirror;
using R3;

namespace Game.Net
{
    public sealed partial class HeroNet : IMatchActions
    {
        [SyncVar(hook = nameof(OnStatusChanged))] private HeroStatus _status;
        [SyncVar] private float _flightSpeed;
        [SyncVar] private float _flightRange;
        public float HookViewSpeed => IsHookPredicted ? _status.Speed : _flightSpeed;
        public float HookViewRange => IsHookPredicted ? _status.Range : _flightRange;
        private readonly ReactiveProperty<HeroStatus> _statusProperty = new();
        public ReadOnlyReactiveProperty<HeroStatus> Status => _statusProperty;
        public HeroStatus CurrentStatus => _status;
        public IMatchActions MatchActions => this;
        public static HeroNet Local => NetworkClient.localPlayer == null ? null : NetworkClient.localPlayer.GetComponent<HeroNet>();
        public bool CanAct => MatchStateNet.Active == null || MatchStateNet.Active.Summary.Phase == MatchPhase.Playing;
        public double NetworkNow => NetworkTime.time;
        public static readonly System.Collections.Generic.HashSet<HeroNet> ClientHeroes = new();
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetClientHeroes() => ClientHeroes.Clear();

        public override void OnStopClient() => ClientHeroes.Remove(this);
        public UnityEngine.Vector3 WorldViewPosition => transform.parent.TransformPoint(DisplayPosition);
        public float ArenaScale => UnityEngine.Mathf.Abs(transform.parent.lossyScale.x);
        public bool IsViewAvailable => this != null && isClient;
        public void SelectTeam(Team team) { if (isLocalPlayer) CmdSelectTeam(team); }
        public void SetReady(bool ready) { if (isLocalPlayer) CmdSetReady(ready); }
        public void StartMatch() { if (isLocalPlayer) CmdStartMatch(); }
        public void ArenaPlaced() { if (isLocalPlayer) CmdArenaPlaced(); }
        public void Rematch() { if (isLocalPlayer) CmdRematch(); }
        public void ReturnToLobby() { if (isLocalPlayer) CmdReturnToLobby(); }

        [Command] private void CmdSelectTeam(Team team) => ApplyLobby(0, team);
        [Command] private void CmdSetReady(bool ready) => ApplyLobby(1, value: ready);
        [Command] private void CmdStartMatch() => ApplyLobby(2);
        [Command] private void CmdArenaPlaced() => ApplyLobby(3);
        [Command] private void CmdRematch() => ApplyLobby(4);
        [Command] private void CmdReturnToLobby() => ApplyLobby(5);

        private void ApplyLobby(byte action, Team team = Team.A, bool value = false)
        {
            if (!OwnsHero(connectionToClient)) return;
            bool host = connectionToClient == NetworkServer.localConnection;
            if (MatchStateNet.Active == null || !MatchStateNet.Active.LobbyAction(_sessionPlayerId, action, team, value, host))
                LogRejected("Lobby", "Action is not allowed in the current state");
        }

        internal void ResetRoundEpoch(uint epoch)
        {
            if (epoch == 0) return;
            RestoreSession(epoch);
        }

        private void OnStatusChanged(HeroStatus previous, HeroStatus current) => _statusProperty.Value = current;
    }
}
