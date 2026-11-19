using Unity.Mathematics;
using UnityEngine.XR.ARSubsystems;

namespace Game.AR
{
    public readonly struct RoomScanSurface
    {
        public RoomScanSurface(TrackableId id, float heightMeters, float2[] boundary)
        {
            Id = id;
            HeightMeters = heightMeters;
            Boundary = boundary;
        }

        public TrackableId Id { get; }
        public float HeightMeters { get; }
        public float2[] Boundary { get; }
    }
}
