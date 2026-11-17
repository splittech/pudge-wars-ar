using System;
using R3;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Game.AR
{
    public sealed class TrackingMonitor : IDisposable
    {
        private readonly ReactiveProperty<bool> _tracking = new(false);
        public ReadOnlyReactiveProperty<bool> IsTracking => _tracking;
        public TrackingMonitor()
        {
            ARSession.stateChanged += OnStateChanged;
            Refresh(null);
        }
        private void OnStateChanged(ARSessionStateChangedEventArgs args) => Refresh(null);
        public void Refresh(ARAnchor anchor)
            => _tracking.Value = ARSession.state == ARSessionState.SessionTracking
                && (anchor == null || anchor.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking);
        public void Dispose() { ARSession.stateChanged -= OnStateChanged; _tracking.Dispose(); }
    }
}
