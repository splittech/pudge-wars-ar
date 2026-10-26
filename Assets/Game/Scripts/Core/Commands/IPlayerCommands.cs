using Game.Core.Economy;
using Unity.Mathematics;

namespace Game.Core.Commands
{
    /// <summary>
    /// Player intents published by input. Implemented by Game.Net, which sends them to the server for validation.
    /// </summary>
    public interface IPlayerCommands
    {
        void MoveTo(float2 arenaPoint);

        /// <param name="direction">Normalized throw direction in arena space.</param>
        void ThrowHook(float2 direction);

        void BuyUpgrade(UpgradeId id);
    }
}
