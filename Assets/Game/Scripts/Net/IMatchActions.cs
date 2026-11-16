using Game.Core.Economy;
using Game.Core.Match;

namespace Game.Net
{
    public interface IMatchActions
    {
        void SelectTeam(Team team);
        void SetReady(bool ready);
        void StartMatch();
        void ArenaPlaced();
        void Rematch();
        void ReturnToLobby();
        void BuyUpgrade(UpgradeId id);
    }
}
