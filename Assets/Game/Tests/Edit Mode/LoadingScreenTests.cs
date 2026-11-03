using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Services.LoadingScreen;
using Game.Services.LoggingService;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class LoadingScreenTests
    {
        [Test]
        public void CancelledShow_CanBeShownAgain()
        {
            var root = new GameObject("Loading screen test");
            var screen = root.AddComponent<TestLoadingScreen>();
            screen.Construct(new SilentLogging());
            try
            {
                Assert.Throws<OperationCanceledException>(() =>
                    screen.ShowScreenAsync(new CancellationToken(true)).GetAwaiter().GetResult());
                Assert.That(root.activeSelf, Is.False);
                screen.ShowScreenAsync().GetAwaiter().GetResult();
                Assert.That(root.activeSelf, Is.True);
                Assert.That(screen.Visible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CancelledHide_RestoresVisibleState()
        {
            var root = new GameObject("Loading screen test");
            var screen = root.AddComponent<TestLoadingScreen>();
            screen.Construct(new SilentLogging());
            try
            {
                screen.ShowScreenImmediate();
                Assert.Throws<OperationCanceledException>(() =>
                    screen.HideScreenAsync(new CancellationToken(true)).GetAwaiter().GetResult());
                Assert.That(screen.Visible, Is.True);
                Assert.That(root.activeSelf, Is.True);
                screen.HideScreenImmediate();
                Assert.That(root.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class SilentLogging : ILoggingService, IGameLogger
        {
            public IGameLogger GetLogger(LoggingChannel channel) => this;
            public void Log(string message) { }
            public void LogWarning(string message) { }
            public void LogError(string message) { }
        }
    }

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
