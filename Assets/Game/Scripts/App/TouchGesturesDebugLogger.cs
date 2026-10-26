using System;
using Game.Input.Gestures;
using Game.Services.LoggingService;
using R3;
using VContainer.Unity;

namespace Game.App
{
    /// <summary>
    /// Logs recognized gestures; verifies the input pipeline until InputRouter consumes them (stage 1).
    /// </summary>
    public sealed class TouchGesturesDebugLogger : IStartable, IDisposable
    {
        private readonly ITouchGestures _gestures;
        private readonly IGameLogger _logger;

        private IDisposable _subscription;

        public TouchGesturesDebugLogger(ITouchGestures gestures, ILoggingService loggingService)
        {
            _gestures = gestures;
            _logger = loggingService.GetLogger(LoggingChannel.Input);
        }

        public void Start()
        {
            // Moved and PinchTwist fire every frame, so only the drag boundaries are logged.
            _subscription = Disposable.Combine(
                _gestures.Tap.Subscribe(position => _logger.Log($"Tap at {position}.")),
                _gestures.Drag
                    .Where(drag => drag.Phase != DragPhase.Moved)
                    .Subscribe(drag => _logger.Log($"Drag {drag.Phase} from {drag.StartPosition} at {drag.Position}.")));
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
