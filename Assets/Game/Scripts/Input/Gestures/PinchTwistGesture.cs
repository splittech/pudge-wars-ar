namespace Game.Input.Gestures
{
    public readonly struct PinchTwistGesture
    {
        /// <summary>
        /// Distance ratio between the fingers relative to the previous frame (1 = no change).
        /// </summary>
        public readonly float ScaleFactor;

        /// <summary>
        /// Rotation of the line between the fingers since the previous frame, in degrees.
        /// </summary>
        public readonly float AngleDelta;

        public PinchTwistGesture(float scaleFactor, float angleDelta)
        {
            ScaleFactor = scaleFactor;
            AngleDelta = angleDelta;
        }
    }
}
