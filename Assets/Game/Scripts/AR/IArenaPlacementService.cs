using System;

namespace Game.AR
{
    public readonly struct ArenaPlacementStatus : IEquatable<ArenaPlacementStatus>
    {
        public ArenaPlacementStatus(
            string message,
            bool panelVisible,
            bool confirmVisible,
            bool canConfirm,
            bool fallbackVisible,
            bool trackingLost)
        {
            Message = message;
            PanelVisible = panelVisible;
            ConfirmVisible = confirmVisible;
            CanConfirm = canConfirm;
            FallbackVisible = fallbackVisible;
            TrackingLost = trackingLost;
        }

        public string Message { get; }
        public bool PanelVisible { get; }
        public bool ConfirmVisible { get; }
        public bool CanConfirm { get; }
        public bool FallbackVisible { get; }
        public bool TrackingLost { get; }

        public bool Equals(ArenaPlacementStatus other)
        {
            return Message == other.Message
                && PanelVisible == other.PanelVisible
                && ConfirmVisible == other.ConfirmVisible
                && CanConfirm == other.CanConfirm
                && FallbackVisible == other.FallbackVisible
                && TrackingLost == other.TrackingLost;
        }
    }

    public interface IArenaPlacementService
    {
        ArenaPlacementStatus Status { get; }
        event Action<ArenaPlacementStatus> StatusChanged;
        void Confirm();
        void UseFlat();
    }
}
