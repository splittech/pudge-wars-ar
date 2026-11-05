using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Services.LoadingScreen;

namespace Game.Tests.Support
{
    public sealed class TestLoadingScreen : GameLoadingScreen
    {
        public bool Visible { get; private set; }
        public override void SetProgressDescription(string description) { }
        protected override void UpdateProgressVisuals(float progress) { }
        protected override void SetVisibleImmediate(bool visible) => Visible = visible;
        protected override UniTask ShowAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Visible = true;
            return UniTask.CompletedTask;
        }
        protected override UniTask HideAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Visible = false;
            return UniTask.CompletedTask;
        }
    }
}
