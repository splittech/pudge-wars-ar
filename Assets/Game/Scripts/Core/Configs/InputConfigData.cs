namespace Game.Core.Configs
{
    public readonly struct InputConfigData
    {
        public readonly float TapMaxTime;
        public readonly float DragThreshold;
        public readonly float HeroGrabRadius;
        public readonly float AimAssistAngle;
        public readonly float ArenaScaleMin;
        public readonly float ArenaScaleMax;

        public InputConfigData(
            float tapMaxTime,
            float dragThreshold,
            float heroGrabRadius,
            float aimAssistAngle,
            float arenaScaleMin,
            float arenaScaleMax)
        {
            TapMaxTime = tapMaxTime;
            DragThreshold = dragThreshold;
            HeroGrabRadius = heroGrabRadius;
            AimAssistAngle = aimAssistAngle;
            ArenaScaleMin = arenaScaleMin;
            ArenaScaleMax = arenaScaleMax;
        }
    }
}
