using System;
using Game.Core.Configs;
using Game.Net;
using Mirror;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Game.Tests.EditMode
{
    public sealed class NetworkPrefabFactoryTests
    {
        private GameObject _managerRoot;
        private GameObject _arenaRoot;
        private ArenaConfig _arenaConfig;
        private MatchConfig _matchConfig;
        private IObjectResolver _resolver;
        private NetworkPrefabList _catalog;
        private NetworkPrefabFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _arenaConfig = ScriptableObject.CreateInstance<ArenaConfig>();
            _matchConfig = ScriptableObject.CreateInstance<MatchConfig>();
            _resolver = Setup.NetworkSpawnContainer(_arenaConfig, _matchConfig);
            _arenaRoot = new GameObject("Local arena");
            _arenaRoot.transform.SetPositionAndRotation(new Vector3(23, 7, -11), Quaternion.Euler(0, 65, 0));
            _arenaRoot.transform.localScale = Vector3.one * 0.05f;
            _catalog = AssetDatabase.LoadAssetAtPath<NetworkPrefabList>(
                "Assets/Game/Configs/Gameplay/Network Prefab List.asset");
            var loader = Setup.NetworkSceneLoader(out _, out _);
            var manager = Setup.NetworkManager(loader, out _managerRoot);
            _factory = new NetworkPrefabFactory(_resolver, _catalog, _arenaRoot.transform, manager);
            _factory.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Dispose();
            UnityEngine.Object.DestroyImmediate(_arenaRoot);
            UnityEngine.Object.DestroyImmediate(_managerRoot);
            _resolver.Dispose();
            UnityEngine.Object.DestroyImmediate(_arenaConfig);
            UnityEngine.Object.DestroyImmediate(_matchConfig);
        }

        [Test]
        public void SpawnClient_HeroUnderTransformedArena_InjectsBeforeNetworkCallbackAndPreservesLocalPose()
        {
            // Arrange.
            var message = new SpawnMessage
            {
                assetId = _catalog.Hero.GetComponent<NetworkIdentity>().assetId,
                position = new Vector3(-8f, 0f, 4f),
                rotation = Quaternion.Euler(0, 30, 0),
                scale = Vector3.one
            };

            // Act.
            GameObject instance = _factory.SpawnClient(message);
            var hero = instance.GetComponent<HeroNet>();
            hero.OnStartClient();

            // Assert.
            Assert.That(hero.HasDependencies, Is.True);
            Assert.That(hero.ClientStartedAfterInjection, Is.True);
            Assert.That(instance.transform.parent, Is.SameAs(_arenaRoot.transform));
            Assert.That(instance.transform.localPosition, Is.EqualTo(message.position));
            Assert.That(Quaternion.Angle(instance.transform.localRotation, message.rotation), Is.LessThan(0.001f));
            Assert.That(instance.transform.localScale, Is.EqualTo(message.scale));
            Assert.That(Vector3.Distance(instance.transform.position,
                _arenaRoot.transform.TransformPoint(message.position)), Is.LessThan(0.001f));
        }

        [Test]
        public void SpawnClient_MatchState_InjectsBeforeNetworkCallback()
        {
            // Arrange.
            var message = new SpawnMessage
            {
                assetId = _catalog.MatchState.GetComponent<NetworkIdentity>().assetId,
                rotation = Quaternion.identity,
                scale = Vector3.one
            };

            // Act.
            var state = _factory.SpawnClient(message).GetComponent<MatchStateNet>();
            state.OnStartClient();

            // Assert.
            Assert.That(state.HasDependencies, Is.True);
            Assert.That(state.ClientStartedAfterInjection, Is.True);
            Assert.That(state.transform.parent, Is.SameAs(_arenaRoot.transform));
        }

        [Test]
        public void SpawnClient_UnknownAssetId_RejectsWithoutCreatingObject()
        {
            // Act and Assert.
            Assert.That(() => _factory.SpawnClient(new SpawnMessage { assetId = 0 }),
                Throws.TypeOf<ArgumentException>());
            Assert.That(_arenaRoot.transform.childCount, Is.Zero);
        }

        [Test]
        public void SpawnServer_NoActiveServer_RejectsWithoutCreatingObject()
        {
            // Act and Assert.
            Assert.That(() => _factory.SpawnServer(_catalog.MatchState.gameObject),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(_arenaRoot.transform.childCount, Is.Zero);
        }

        [Test]
        public void SpawnClient_FactoryDisposed_RejectsWithoutCreatingObject()
        {
            // Arrange.
            _factory.Dispose();
            var message = new SpawnMessage { assetId = _catalog.Hero.GetComponent<NetworkIdentity>().assetId };

            // Act and Assert.
            Assert.That(() => _factory.SpawnClient(message), Throws.TypeOf<ArgumentException>());
            Assert.That(_arenaRoot.transform.childCount, Is.Zero);
        }

        [Test]
        public void Validate_EmptyCatalog_RejectsMissingPrefabs()
        {
            // Arrange.
            var empty = ScriptableObject.CreateInstance<NetworkPrefabList>();
            try
            {
                // Act and Assert.
                Assert.That(() => empty.Validate(), Throws.TypeOf<InvalidOperationException>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void OnStartClient_HeroCreatedWithoutDI_RejectsBeforeClientInitialization()
        {
            // Arrange.
            var instance = UnityEngine.Object.Instantiate(_catalog.Hero, _arenaRoot.transform, false);

            // Act and Assert.
            Assert.That(() => instance.OnStartClient(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(instance.ClientStartedAfterInjection, Is.False);
        }
    }
}
