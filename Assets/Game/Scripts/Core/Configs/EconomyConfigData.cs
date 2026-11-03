using System;
using System.Collections.Generic;

namespace Game.Core.Configs
{
    public readonly struct EconomyConfigData
    {
        public readonly int KillGold;
        public readonly int AssistGold;
        public readonly float AssistWindow;
        public readonly int StartGold;
        public readonly IReadOnlyList<int> UpgradePrices;

        public EconomyConfigData(int killGold, int assistGold, float assistWindow, int startGold, int[] upgradePrices)
        {
            KillGold = killGold;
            AssistGold = assistGold;
            AssistWindow = assistWindow;
            StartGold = startGold;
            UpgradePrices = Array.AsReadOnly((int[])upgradePrices.Clone());
        }
    }
}
