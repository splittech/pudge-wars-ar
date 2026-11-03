using Unity.Mathematics;

namespace Game.Core.Commands
{
    public readonly struct ThrowHookIntent
    {
        public readonly uint PlayerId;
        public readonly float2 Direction;
        public readonly double ClientTime;

        public ThrowHookIntent(uint playerId, float2 direction, double clientTime)
        {
            PlayerId = playerId;
            Direction = direction;
            ClientTime = clientTime;
        }
    }
}
