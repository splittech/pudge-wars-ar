using Game.Tests.Support;
using System;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class LoadingScreenTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void ShowScreenAsync_TokenIsCancelled_ThrowsAndHidesScreen()
        {
            // Arrange.
            TestLoadingScreen screen = Setup.LoadingScreen(out _root);
            CancellationToken token = new(canceled: true);

            // Act and Assert.
            Assert.That(() => screen.ShowScreenAsync(token).GetAwaiter().GetResult(),
                Throws.TypeOf<OperationCanceledException>());
            Assert.That(_root.activeSelf, Is.False);
        }

        [Test]
        public void ShowScreenAsync_PreviousShowWasCancelled_ScreenIsVisible()
        {
            // Arrange.
            TestLoadingScreen screen = Setup.LoadingScreen(out _root);
            try
            {
                screen.ShowScreenAsync(new CancellationToken(canceled: true)).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // The previous transition was cancelled before this action.
            }

            // Act.
            screen.ShowScreenAsync().GetAwaiter().GetResult();

            // Assert.
            Assert.That(_root.activeSelf, Is.True);
            Assert.That(screen.Visible, Is.True);
        }

        [Test]
        public void HideScreenAsync_TokenIsCancelled_ScreenRemainsVisible()
        {
            // Arrange.
            TestLoadingScreen screen = Setup.LoadingScreen(out _root);
            screen.ShowScreenImmediate();
            CancellationToken token = new(canceled: true);

            // Act and Assert.
            Assert.That(() => screen.HideScreenAsync(token).GetAwaiter().GetResult(),
                Throws.TypeOf<OperationCanceledException>());
            Assert.That(screen.Visible, Is.True);
            Assert.That(_root.activeSelf, Is.True);
        }

        [Test]
        public void HideScreenImmediate_PreviousHideWasCancelled_ScreenIsInactive()
        {
            // Arrange.
            TestLoadingScreen screen = Setup.LoadingScreen(out _root);
            screen.ShowScreenImmediate();
            try
            {
                screen.HideScreenAsync(new CancellationToken(canceled: true)).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // The previous transition was cancelled before this action.
            }

            // Act.
            screen.HideScreenImmediate();

            // Assert.
            Assert.That(_root.activeSelf, Is.False);
        }
    }
}