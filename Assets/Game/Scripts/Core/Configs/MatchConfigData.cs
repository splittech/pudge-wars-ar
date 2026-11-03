using System;
using System.Collections.Generic;

namespace Game.Core.Configs
{
    public readonly struct MatchConfigData
    {
        public readonly int TickRate;
        public readonly int KillsToWin;
        public readonly float MatchDuration;
        public readonly float CountdownDuration;
        public readonly float PlacementTimeout;
        public readonly int MaxPlayers;

        public MatchConfigData(int tickRate, int killsToWin, float matchDuration, float countdownDuration, float placementTimeout, int maxPlayers)
        {
            TickRate = tickRate;
            KillsToWin = killsToWin;
            MatchDuration = matchDuration;
            CountdownDuration = countdownDuration;
            PlacementTimeout = placementTimeout;
            MaxPlayers = maxPlayers;
        }
    }
}
