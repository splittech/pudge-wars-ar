using Game.Core.Economy;

namespace Game.Core.Commands
{
    public readonly struct BuyUpgradeIntent
    {
        public readonly uint PlayerId;
        public readonly UpgradeId UpgradeId;

        public BuyUpgradeIntent(uint playerId, UpgradeId upgradeId)
        {
            PlayerId = playerId;
            UpgradeId = upgradeId;
        }
    }
}
