using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Net;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode
{
    public sealed class NetworkSceneTests
    {
        private GameObject _root;
        private PudgeNetworkManager _manager;
        private INetworkSceneLoader _loader;
        private UniTaskCompletionSource _completion;
        private List<string> _requests;

        [SetUp]
        public void SetUp()
        {
            _loader = Setup.NetworkSceneLoader(out _completion, out _requests);
            _manager = Setup.NetworkManager(_loader, out _root);
        }

        [TearDown]
        public void TearDown()
        {
            _completion.TrySetResult();
            UnityEngine.Object.DestroyImmediate(_root);
            NetworkServer.isLoadingScene = false;
            NetworkClient.isLoadingScene = false;
        }

        [Test]
        public void ServerChangeScene_UnknownScene_IsRejectedBeforeLoading()
        {
            // Act and Assert.
            Assert.That(() => _manager.ServerChangeScene("Unknown"), Throws.TypeOf<ArgumentException>());
            Assert.That(_requests, Is.Empty);
            Assert.That(_manager.IsChangingScene, Is.False);
        }

        [Test]
        public void ServerChangeScene_NoServer_IsRejectedBeforeLoading()
        {
            // Act and Assert.
            Assert.That(() => _manager.ServerChangeScene(_loader.OnlineScenePath), Throws.TypeOf<InvalidOperationException>());
            Assert.That(_requests, Is.Empty);
        }

        [UnityTest]
        public IEnumerator StopCallbacks_SamePendingScene_LoadsMenuOnce()
        {
            // Arrange.
            _manager.OnStopServer();

            // Act.
            _manager.OnStopClient();
            Assert.That(_manager.IsChangingScene, Is.True);
            for (int i = 0; i < 30 && _requests.Count == 0; i++)
                yield return null;

            Assert.That(_requests, Is.EqualTo(new[] { _loader.OfflineScenePath }));
            Assert.That(_manager.IsChangingScene, Is.True);
            _manager.OnStopClient();
            _completion.TrySetResult();
            for (int i = 0; i < 30 && _manager.IsChangingScene; i++)
                yield return null;

            // Assert.
            Assert.That(_manager.IsChangingScene, Is.False);
            Assert.That(_requests.Count, Is.EqualTo(1));
            Assert.That(NetworkClient.isLoadingScene, Is.False);
            Assert.That(NetworkServer.isLoadingScene, Is.False);
        }

        [UnityTest]
        public IEnumerator StopCallbacks_MenuLoadFails_ClearsLoadingFlagsWithoutRetry()
        {
            // Arrange.
            NetworkClient.isLoadingScene = true;
            _manager.OnStopClient();
            for (int i = 0; i < 30 && _requests.Count == 0; i++)
                yield return null;

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException: Test load failure"));
            // Act.
            _completion.TrySetException(new InvalidOperationException("Test load failure"));
            for (int i = 0; i < 30 && _manager.IsChangingScene; i++)
                yield return null;

            // Assert.
            Assert.That(_manager.IsChangingScene, Is.False);
            Assert.That(_requests.Count, Is.EqualTo(1));
            Assert.That(NetworkClient.isLoadingScene, Is.False);
            Assert.That(NetworkServer.isLoadingScene, Is.False);
        }

    }
}
