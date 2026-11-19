using Game.Core.Geometry;
using Game.Core.Match;
using Mirror;

namespace Game.Net
{
    public sealed partial class HeroNet
    {
        public string RoomError { get; private set; }

        public void SubmitRoom(RoomArena room)
        {
            if (isLocalPlayer)
            {
                RoomError = string.Empty;
                CmdSubmitRoom(RoomArenaCodec.Encode(room));
            }
        }

        public void ConfirmRoom(uint revision)
        {
            if (isLocalPlayer)
            {
                CmdConfirmRoom(revision);
            }
        }

        [Command]
        private void CmdSetGameMode(GameMode mode)
        {
            if (OwnsHero(connectionToClient))
            {
                TargetRoomResult("Режим выбирается до создания лобби. Создайте новую игру для смены режима.");
            }
        }

        [Command]
        private void CmdSubmitRoom(byte[] payload)
        {
            if (!OwnsHero(connectionToClient) || MatchStateNet.Active == null)
            {
                return;
            }

            MatchStateNet.Active.SubmitRoom(payload, connectionToClient == NetworkServer.localConnection, out string reason);
            TargetRoomResult(reason);
        }

        [Command]
        private void CmdConfirmRoom(uint revision)
        {
            if (OwnsHero(connectionToClient) && MatchStateNet.Active != null)
            {
                MatchStateNet.Active.ConfirmRoom(_sessionPlayerId, revision);
            }
        }

        [TargetRpc]
        private void TargetRoomResult(string reason)
        {
            RoomError = reason;
        }
    }
}
