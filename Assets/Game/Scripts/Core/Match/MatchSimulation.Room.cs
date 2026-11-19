using Game.Core.Geometry;
using Unity.Mathematics;

namespace Game.Core.Match
{
    public sealed partial class MatchSimulation
    {
        private readonly System.Collections.Generic.Dictionary<uint, float2> _roomSpawns = new();
        public RoomArena Room { get; private set; }
        public bool IsRoom => Rules?.Mode == GameMode.RoomFreeForAll;

        public bool TrySetRoom(RoomArena room)
        {
            if (!IsRoom || Rules.Phase != MatchPhase.Placement || Room != null || room == null
                || !room.TryFindSpawns(_heroes.Length, _heroConfig.Radius, out float2[] spawns))
            {
                return false;
            }

            Room = room;
            _roomSpawns.Clear();
            for (int i = 0; i < _heroes.Length; i++)
            {
                _roomSpawns.Add(_heroes[i].Id, spawns[i]);
            }

            ResetRound();
            return true;
        }

        public void ClearRoom()
        {
            if (Rules?.Phase == MatchPhase.Lobby)
            {
                Room = null;
                _roomSpawns.Clear();
            }
        }

        private Rect2 MovementBounds(float2 position, float radius)
        {
            if (IsRoom && Room != null)
            {
                return new Rect2(Room.Bounds.Min + radius, Room.Bounds.Max - radius);
            }

            return ArenaBounds.ForPosition(_arena, position, radius);
        }

        private float2 MoveInRoom(float2 start, float2 destination, float radius)
        {
            return IsRoom && Room != null ? Room.Move(start, destination, radius) : destination;
        }

        private void AdvanceRules()
        {
            Rules.Advance(Time, CountTeam(Team.A), CountTeam(Team.B));
            Rules.AdvanceRoom(Time, _heroes.Length, _heroes.Length == 1 ? _heroes[0].Id : 0);
        }
    }
}
