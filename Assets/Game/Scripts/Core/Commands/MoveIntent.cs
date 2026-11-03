using Unity.Mathematics;

namespace Game.Core.Commands
{
    public readonly struct MoveIntent
    {
        public readonly uint PlayerId;
        public readonly float2 ArenaPoint;

        public MoveIntent(uint playerId, float2 arenaPoint)
        {
            PlayerId = playerId;
            ArenaPoint = arenaPoint;
        }
    }
}
