using Unity.Mathematics;

namespace Game.Input.Routing
{
    /// <summary>
    /// Hook aiming state for the aim line view. All values are in arena space.
    /// </summary>
    public readonly struct AimState
    {
        public static readonly AimState Inactive = default;

        public readonly bool IsActive;

        /// <summary>
        /// The finger is within the hero grab radius: releasing now cancels the throw (FR-IN-03).
        /// </summary>
        public readonly bool IsInCancelZone;

        /// <summary>
        /// Hero position the aim starts from.
        /// </summary>
        public readonly float2 Origin;

        /// <summary>
        /// Normalized throw direction; zero in the cancel zone.
        /// </summary>
        public readonly float2 Direction;

        public AimState(float2 origin, float2 direction, bool isInCancelZone)
        {
            IsActive = true;
            IsInCancelZone = isInCancelZone;
            Origin = origin;
            Direction = direction;
        }
    }
}
