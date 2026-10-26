using Unity.Mathematics;
using UnityEngine;

namespace Game.Input.Routing
{
    /// <summary>
    /// Conversion between screen, arena and world space. Works the same in AR and flat mode:
    /// screen points are raycast onto the mathematical plane of ArenaRoot.
    /// </summary>
    public interface IArenaSpace
    {
        /// <summary>
        /// False if the camera ray through the point is parallel to the arena plane or points away from it.
        /// </summary>
        bool TryScreenToArena(Vector2 screenPoint, out float2 arenaPoint);

        Vector3 ArenaToWorld(float2 arenaPoint);
    }
}
