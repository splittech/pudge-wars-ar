using R3;
using UnityEngine;
using Game.UI.Foundation;
using Game.Services.LoadingScreen;

namespace Game.UI
{
    [CreateAssetMenu(menuName = "Game/UI/Loading ViewModel")]
    public sealed class LoadingViewModel : UiViewModel
    {
        private ILoadingStateSource _source;

        private ReactiveProperty<string> _description;
        public ReactiveProperty<string> Description => _description ??= Property<string>("");

        private ReactiveProperty<float> _progress;
        public ReactiveProperty<float> Progress => _progress ??= Property<float>(0f);

        private ReactiveProperty<string> _progressText;
        public ReactiveProperty<string> ProgressText => _progressText ??= Property<string>("0%");

        private ReactiveProperty<bool> _visible;
        public ReactiveProperty<bool> Visible => _visible ??= Property<bool>(false);

        private ReactiveProperty<bool> _transparent;
        public ReactiveProperty<bool> Transparent => _transparent ??= Property<bool>(true);

        public void Initialize(ILoadingStateSource source)
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

        private void Apply(LoadingSnapshot state)
        {
            Description.Value = state.Description;
            Progress.Value = state.Progress;
            ProgressText.Value = $"{state.Progress * 100:0}%";
            Visible.Value = state.Visible;
            Transparent.Value = state.Transparent;
        }
    }
}
