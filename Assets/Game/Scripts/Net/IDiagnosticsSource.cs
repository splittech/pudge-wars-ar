using System;

namespace Game.Net
{
    public readonly struct DiagnosticsSnapshot
    {
        public DiagnosticsSnapshot(string text, bool useAR)
        {
            Text = text;
            UseAR = useAR;
        }

        public string Text { get; }
        public bool UseAR { get; }
    }

    public interface IDiagnosticsSource
    {
        DiagnosticsSnapshot Snapshot { get; }
        event Action<DiagnosticsSnapshot> Changed;
        void ToggleArenaMode();
    }
}
