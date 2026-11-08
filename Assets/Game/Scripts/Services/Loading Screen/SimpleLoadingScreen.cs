using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Services.LoadingScreen
{
    public sealed class SimpleLoadingScreen : GameLoadingScreen
    {
        [Header("Simple Loading Screen")]
        [SerializeField] private Slider _slider;
        [SerializeField] private TMP_Text _progressDescription;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeTime = 1f;

        private Tween _fadeTween;

        public override void SetProgressDescription(string description)
        {
            _progressDescription.text = description;
        }

        protected override UniTask ShowAsync(CancellationToken ct)
        {
            return DoFadeAsync(1f, ct);
        }

        protected override UniTask HideAsync(CancellationToken ct)
        {
            return DoFadeAsync(0f, ct);
        }

        protected override void SetVisibleImmediate(bool visible)
        {
            KillFadeAnimation();
            _canvasGroup.alpha = visible ? 1f : 0f;
        }

        protected override void UpdateProgressVisuals(float progress)
        {
            _slider.value = progress;
        }

        private async UniTask DoFadeAsync(float endValue, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            KillFadeAnimation();

            // Independent update keeps the fade running while Time.timeScale is 0.
            Tween tween = _canvasGroup.DOFade(endValue, _fadeTime).SetUpdate(true);
            _fadeTween = tween;
            try
            {
                // Poll cancellation on the player loop. A destruction callback must not
                // kill a stale tween after DOTween has cleared its active tween arrays.
                while (tween.IsActive() && tween.IsPlaying())
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);

                ct.ThrowIfCancellationRequested();
            }
            finally
            {
                // A superseded transition must never kill the replacement animation.
                if (ReferenceEquals(_fadeTween, tween))
                    KillFadeAnimation();
            }
        }

        private void KillFadeAnimation()
        {
            _fadeTween = null;
            // Query DOTween's current active list instead of killing a retained handle.
            if (_canvasGroup != null)
                _canvasGroup.DOKill();
        }
    }
}
