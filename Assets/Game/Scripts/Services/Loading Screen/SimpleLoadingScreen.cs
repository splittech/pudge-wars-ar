using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Services.LoadingScreen
{
    public sealed class SimpleLoadingScreen : GameLoadingScreen, ILoadingStateSource
    {
        [SerializeField] private float _fadeTime = 0.2f;

        public LoadingSnapshot Snapshot { get; private set; }
        public event Action<LoadingSnapshot> Changed;

        public override void SetProgressDescription(string description)
        {
            Publish(description, Snapshot.Progress, Snapshot.Visible, Snapshot.Transparent);
        }

        protected override UniTask ShowAsync(CancellationToken ct)
        {
            return FadeAsync(true, ct);
        }

        protected override UniTask HideAsync(CancellationToken ct)
        {
            return FadeAsync(false, ct);
        }

        protected override void SetVisibleImmediate(bool visible)
        {
            Publish(Snapshot.Description, Snapshot.Progress, visible, !visible);
        }

        protected override void UpdateProgressVisuals(float progress)
        {
            Publish(Snapshot.Description, progress, Snapshot.Visible, Snapshot.Transparent);
        }

        private async UniTask FadeAsync(bool visible, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Publish(Snapshot.Description, Snapshot.Progress, true, visible);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            Publish(Snapshot.Description, Snapshot.Progress, true, !visible);
            await UniTask.Delay(
                TimeSpan.FromSeconds(Mathf.Max(0, _fadeTime)),
                ignoreTimeScale: true,
                cancellationToken: ct);
            ct.ThrowIfCancellationRequested();
            SetVisibleImmediate(visible);
        }

        private void Publish(string description, float progress, bool visible, bool transparent)
        {
            Snapshot = new LoadingSnapshot(description, progress, visible, transparent);
            Changed?.Invoke(Snapshot);
        }
    }
}
