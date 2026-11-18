using System;
using R3;
using UnityEngine.XR.ARFoundation;
using TrackingState = UnityEngine.XR.ARSubsystems.TrackingState;

namespace Game.AR
{
    public sealed class TrackingMonitor : IDisposable
    {
        private readonly ReactiveProperty<bool> _tracking = new(false);
        private ARAnchor _anchor;

        public TrackingMonitor()
        {
            ARSession.stateChanged += OnStateChanged;
            Refresh(null);
        }

        public ReadOnlyReactiveProperty<bool> IsTracking => _tracking;

        public void Refresh(ARAnchor anchor)
        {
            _anchor = anchor;
            _tracking.Value = ARSession.state == ARSessionState.SessionTracking
                && (anchor == null || anchor.trackingState == TrackingState.Tracking);
        }

        public void Dispose()
        {
            ARSession.stateChanged -= OnStateChanged;
            _tracking.Dispose();
        }

        private void OnStateChanged(ARSessionStateChangedEventArgs args)
        {
            Refresh(_anchor);
        }
    }
}
