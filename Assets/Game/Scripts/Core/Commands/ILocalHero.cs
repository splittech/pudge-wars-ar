using Unity.Mathematics;

namespace Game.Core.Commands
{
    /// <summary>
    /// The hero controlled by this client.
    /// </summary>
    public interface ILocalHero
    {
        /// <summary>
        /// Current hero position in arena space; false if there is no hero or it is dead.
        /// </summary>
        bool TryGetPosition(out float2 position);
    }
}
