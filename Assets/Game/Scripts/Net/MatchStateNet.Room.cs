using System;
using Game.Core.Geometry;
using Game.Core.Match;
using Mirror;

namespace Game.Net
{
    public sealed partial class MatchStateNet
    {
        [SyncVar(hook = nameof(OnRoomPayloadChanged))] private byte[] _roomPayload;
        [SyncVar] private uint _roomRevision;

        public bool IsRoom => _summary.Mode == GameMode.RoomFreeForAll;
        public RoomArena Room { get; private set; }
        public uint RoomRevision => _roomRevision;

        [Server]
        internal bool SubmitRoom(byte[] payload, bool host, out string reason)
        {
            reason = "Карту может отправить только хост во время размещения.";
            if (!host || !IsRoom || _simulation.Rules.Phase != MatchPhase.Placement || Room != null)
            {
                return false;
            }

            if (!RoomArenaCodec.TryDecode(payload, out RoomArena room, out reason))
            {
                return false;
            }

            if (!_simulation.TrySetRoom(room))
            {
                reason = "На карте недостаточно свободного места для игроков.";
                return false;
            }

            _roomRevision++;
            _roomPayload = (byte[])payload.Clone();
            Room = room;
            Publish();
            reason = string.Empty;
            return true;
        }

        [Server]
        internal bool ConfirmRoom(uint playerId, uint revision)
        {
            if (!IsRoom || Room == null || revision == 0 || revision != _roomRevision
                || _simulation.Rules.Phase != MatchPhase.Placement)
            {
                return false;
            }

            bool accepted = _placement.Confirm(playerId, NetworkTime.time);
            if (accepted)
            {
                AdvancePlacement(NetworkTime.time);
            }

            return accepted;
        }

        private void ClearRoom()
        {
            _simulation.ClearRoom();
            _roomPayload = Array.Empty<byte>();
            Room = null;
        }

        private void OnRoomPayloadChanged(byte[] previous, byte[] current)
        {
            RoomArenaCodec.TryDecode(current, out RoomArena room, out _);
            Room = room;
        }
    }
}
