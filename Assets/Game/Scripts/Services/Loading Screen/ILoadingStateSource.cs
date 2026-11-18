using System;

namespace Game.Services.LoadingScreen
{
    public readonly struct LoadingSnapshot
    {
        public LoadingSnapshot(string description, float progress, bool visible, bool transparent)
        {
            Description = description;
            Progress = progress;
            Visible = visible;
            Transparent = transparent;
        }

        public string Description { get; }
        public float Progress { get; }
        public bool Visible { get; }
        public bool Transparent { get; }
    }

    public interface ILoadingStateSource
    {
        LoadingSnapshot Snapshot { get; }
        event Action<LoadingSnapshot> Changed;
    }
}
