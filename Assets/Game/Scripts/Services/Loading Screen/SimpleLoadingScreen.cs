using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.Foundation;
using UnityEngine;

namespace Game.Services.LoadingScreen
{
    public sealed class SimpleLoadingScreen : GameLoadingScreen
    {
        [SerializeField] private float _fadeTime = 0.2f;
        [SerializeField] private UiState _runtimeModel;
        private ReactiveDocument _view;

        private void EnsureView()
        {
            if (_view != null) return;

            _view = ToolkitRuntime.Create(gameObject, "Loading", "LoadingState", 1000);
            _runtimeModel = _view.Model;
        }

        public override void SetProgressDescription(string description)
        {
            EnsureView(); _runtimeModel.Text("description", description);
        }

        protected override UniTask ShowAsync(CancellationToken ct) => FadeAsync(true, ct);
        protected override UniTask HideAsync(CancellationToken ct) => FadeAsync(false, ct);

        protected override void SetVisibleImmediate(bool visible)
        {
            EnsureView(); _runtimeModel.Visible("loading", visible);
            _runtimeModel.StyleClass("loading", visible ? string.Empty : "transparent");
        }

        protected override void UpdateProgressVisuals(float progress)
        {
            EnsureView(); _runtimeModel.Number("progress", progress);
            _runtimeModel.Text("progress", $"{progress * 100:0}%");
        }

        private async UniTask FadeAsync(bool visible, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            EnsureView();
            _runtimeModel.Visible("loading", true);
            _runtimeModel.StyleClass("loading", visible ? "transparent" : string.Empty);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            _runtimeModel.StyleClass("loading", visible ? string.Empty : "transparent");
            await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0, _fadeTime)), ignoreTimeScale: true, cancellationToken: ct);
            ct.ThrowIfCancellationRequested();
            SetVisibleImmediate(visible);
        }
    }
}
