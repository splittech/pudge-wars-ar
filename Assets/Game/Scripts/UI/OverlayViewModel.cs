using R3;
using UnityEngine;
using Game.UI.Foundation;
using Game.Net;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Overlay ViewModel")]
    public sealed class OverlayViewModel : UiViewModel
    {
        private IDiagnosticsSource _source;

        private ReactiveProperty<string> _diagnostics;
        public ReactiveProperty<string> Diagnostics => _diagnostics ??= Property<string>("");

        private ReactiveProperty<string> _arenaModeText;
        public ReactiveProperty<string> ArenaModeText => _arenaModeText ??= Property<string>("Flat → AR");

        public void Initialize(IDiagnosticsSource source)
        {
            Disconnect();
            _source = source;
            _source.Changed += Apply;
            Apply(_source.Snapshot);
        }

        public override void Disconnect()
        {
            if (_source != null)
            {
                _source.Changed -= Apply;
                _source = null;
            }
        }

        public void ToggleArenaMode()
        {
            _source?.ToggleArenaMode();
        }

        private void Apply(DiagnosticsSnapshot state)
        {
            Diagnostics.Value = state.Text;
            ArenaModeText.Value = state.UseAR ? "AR → Flat" : "Flat → AR";
        }
    }
}
