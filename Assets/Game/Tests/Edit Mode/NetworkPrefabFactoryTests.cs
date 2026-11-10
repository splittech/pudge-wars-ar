using System;
using Game.Core.Configs;
using Game.Net;
using Game.Core.Match;
using NSubstitute;
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
        private HeroConfig _heroConfig;
        private HookConfig _hookConfig;
        private IObjectResolver _resolver;
        private NetworkPrefabList _catalog;
        private NetworkPrefabFactory _factory;
        private MatchSimulation _simulation;
        private ServerPlayerCommands _commands;
        private MatchStateSink _sink;
        private ReconnectRegistry _reconnect;
        private bool _serverStarted;
        private Transport _previousTransport;
        private bool _previousListen;

        [SetUp]
        public void SetUp()
        {
            _arenaConfig = ScriptableObject.CreateInstance<ArenaConfig>();
            _matchConfig = ScriptableObject.CreateInstance<MatchConfig>();
            _heroConfig = ScriptableObject.CreateInstance<HeroConfig>();
            _hookConfig = ScriptableObject.CreateInstance<HookConfig>();
            _simulation = Setup.MatchSimulation(System.Array.Empty<Game.Core.Hero.HeroState>());
            _commands = new ServerPlayerCommands(_simulation, _arenaConfig);
            _resolver = Setup.NetworkSpawnContainer(_arenaConfig, _matchConfig, _commands, _hookConfig);
            _arenaRoot = new GameObject("Local arena");
            _arenaRoot.transform.SetPositionAndRotation(new Vector3(23, 7, -11), Quaternion.Euler(0, 65, 0));
            _arenaRoot.transform.localScale = Vector3.one * 0.05f;
            _catalog = AssetDatabase.LoadAssetAtPath<NetworkPrefabList>(
                "Assets/Game/Configs/Gameplay/Network Prefab List.asset");
            var loader = Setup.NetworkSceneLoader(out _, out _);
            var manager = Setup.NetworkManager(loader, out _managerRoot);
            _sink = new MatchStateSink();
            _reconnect = new ReconnectRegistry();
            _factory = new NetworkPrefabFactory(Setup.EditModeNetworkSpawnResolver(_resolver),
                _catalog, _arenaRoot.transform, manager,
                _simulation,
                _sink, _reconnect, _arenaConfig, _heroConfig, _commands);
            _factory.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_serverStarted)
            {
                NetworkServer.Shutdown();
                NetworkServer.listen = _previousListen;
                Transport.active = _previousTransport;
                _serverStarted = false;
            }
            _factory.Dispose();
            UnityEngine.Object.DestroyImmediate(_arenaRoot);
            UnityEngine.Object.DestroyImmediate(_managerRoot);
            _resolver.Dispose();
            UnityEngine.Object.DestroyImmediate(_arenaConfig);
            UnityEngine.Object.DestroyImmediate(_matchConfig);
            UnityEngine.Object.DestroyImmediate(_heroConfig);
            UnityEngine.Object.DestroyImmediate(_hookConfig);
        }

        private NetworkConnectionToClient StartTestServer()
        {
            _previousTransport = Transport.active;
            _previousListen = NetworkServer.listen;
            _serverStarted = true;
            Transport.active = _managerRoot.GetComponent<Transport>();
            // EditMode does not invoke Awake on the inactive test root. Initialize KCP without opening sockets.
            typeof(kcp2k.KcpTransport).GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(Transport.active, null);
            NetworkServer.listen = false;
            NetworkServer.Listen(4);
            return new NetworkConnectionToClient(1) { isAuthenticated = true, isReady = true };
        }

        [Test]
        public void Apply_ConfirmedThrow_CopiesRequestCorrelationToHookSnapshot()
        {
            // Arrange.
            NetworkConnectionToClient connection = StartTestServer();
            HeroNet hero = _factory.SpawnPlayer(connection);
            _commands.TryThrow(hero.SessionPlayerId, new Unity.Mathematics.float2(1, 0),
                100, true, 17, hero.SessionEpoch, out _);
            var runner = new ServerTickRunner(_simulation, _sink, _factory, _commands);

            // Act.
            runner.Advance(ServerTickRunner.StepDelta, 100, true);

            // Assert.
            Assert.That(hero.HookState.RequestId, Is.EqualTo(17));
            Assert.That(hero.HookState.SessionEpoch, Is.EqualTo(hero.SessionEpoch));
            Assert.That(hero.HookState.Phase, Is.EqualTo(Game.Core.Hook.HookPhase.Extending));
        }

        [Test]
        public void Apply_HookHitsHero_ExportsNetworkTargetAndReactiveDamage()
        {
            // Arrange.
            NetworkConnectionToClient connection = StartTestServer();
            _factory.EnsureMatchState();
            HeroNet owner = _factory.SpawnPlayer(connection);
            HeroNet target = _factory.SpawnPlayer(new NetworkConnectionToClient(2)
                { isAuthenticated = true, isReady = true });
            _commands.TryMove(owner.SessionPlayerId, new Unity.Mathematics.float2(-3, -4), 100, true, out _);
            _commands.TryMove(target.SessionPlayerId, new Unity.Mathematics.float2(3, -4), 100, true, out _);
            var runner = new ServerTickRunner(_simulation, _sink, _factory, _commands);
            for (int i = 0; i < 60; i++)
                runner.Advance(ServerTickRunner.StepDelta, 100 + (i + 1) * (double)ServerTickRunner.StepDelta, true);

            // Act.
            _commands.TryThrow(owner.SessionPlayerId, new Unity.Mathematics.float2(1, 0), 103, true, out _);
            for (int i = 0; i < 30 && owner.HookState.Phase != Game.Core.Hook.HookPhase.Pulling; i++)
                runner.Advance(ServerTickRunner.StepDelta, 103 + (i + 1) * (double)ServerTickRunner.StepDelta, true);

            // Assert.
            Assert.That(owner.HookState.Phase, Is.EqualTo(Game.Core.Hook.HookPhase.Pulling));
            Assert.That(owner.HookState.TargetNetId, Is.EqualTo(target.netId));
            Assert.That(target.netId, Is.Not.EqualTo(target.SessionPlayerId));
            Assert.That(target.Health.CurrentValue, Is.EqualTo(825));
            Assert.That(target.Health.CurrentValue, Is.EqualTo(target.Hp));
        }

        [Test]
        public void HeroPrefab_NetworkTransform_UsesServerLocalPositionOnly()
        {
            // Arrange.
            NetworkBehaviour component = Array.Find(_catalog.Hero.GetComponents<NetworkBehaviour>(),
                value => value.GetType().Name == "NetworkTransformUnreliable");

            // Act.
            Assert.That(component, Is.Not.Null);
            var settings = new SerializedObject(component);

            // Assert.
            Assert.That(settings.FindProperty("coordinateSpace").enumValueIndex, Is.Zero);
            Assert.That(component.syncDirection, Is.EqualTo(SyncDirection.ServerToClient));
            Assert.That(settings.FindProperty("syncPosition").boolValue, Is.True);
            Assert.That(settings.FindProperty("syncRotation").boolValue, Is.False);
            Assert.That(settings.FindProperty("syncScale").boolValue, Is.False);
            Assert.That(settings.FindProperty("target").objectReferenceValue,
                Is.SameAs(_catalog.Hero.transform));
        }

        [Test]
        public void Apply_HookKeepsExtending_PreservesCompactSnapshotAndReactiveHp()
        {
            // Arrange.
            NetworkConnectionToClient connection = StartTestServer();
            HeroNet hero = _factory.SpawnPlayer(connection);
            _commands.TryThrow(hero.SessionPlayerId, new Unity.Mathematics.float2(1, 0), 100, true, out _);
            var runner = new ServerTickRunner(_simulation, _sink, _factory, _commands);
            runner.Advance(ServerTickRunner.StepDelta, 100, true);
            HookNetState first = hero.HookState;

            // Act.
            runner.Advance(ServerTickRunner.StepDelta, 100 + ServerTickRunner.StepDelta, true);

            // Assert.
            Assert.That(first.Phase, Is.EqualTo(Game.Core.Hook.HookPhase.Extending));
            Assert.That(hero.HookState, Is.EqualTo(first));
            Assert.That(hero.Health.CurrentValue, Is.EqualTo(hero.Hp));
            Assert.That(_simulation.GetHook(0).Length, Is.GreaterThan(0));
        }

        [Test]
        public void SpawnPlayer_ServerTick_RegistersAndProjectsArenaCoordinates()
        {
            // Arrange.
            NetworkConnectionToClient connection = StartTestServer();
            HeroNet hero = _factory.SpawnPlayer(connection);
            hero.transform.localPosition += Vector3.up * 0.75f;
            var target = _arenaConfig.ToData().TeamASpawnPoints[0];
            target.x += 1;
            _commands.TryMove(hero.SessionPlayerId, target, 100, true, out _);
            var runner = new ServerTickRunner(_simulation, _sink, _factory, _commands);

            // Act.
            runner.Advance(ServerTickRunner.StepDelta, 100, true);

            // Assert.
            Assert.That(_simulation.HeroCount, Is.EqualTo(1));
            Assert.That(hero.Hp, Is.EqualTo(_heroConfig.ToData().MaxHp));
            Assert.That(hero.Alive, Is.True);
            var position = _simulation.GetHero(0).Pos;
            Assert.That(hero.transform.localPosition, Is.EqualTo(new Vector3(position.x, 0.75f, position.y)));
            Assert.That(hero.transform.parent, Is.SameAs(_arenaRoot.transform));
        }

        [Test]
        public void OnServerDisconnect_DeadlineExpires_PreservesThenRemovesHeroAndReleasesSlot()
        {
            // Arrange.
            NetworkConnectionToClient connection = StartTestServer();
            HeroNet hero = _factory.SpawnPlayer(connection);
            uint playerId = hero.SessionPlayerId;
            var manager = _managerRoot.GetComponent<PudgeNetworkManager>();
            var runner = new ServerTickRunner(_simulation, _sink, _factory, _commands);

            // Act.
            manager.OnServerDisconnect(connection);

            // Assert.
            Assert.That(connection.identity, Is.Null);
            Assert.That(hero == null, Is.False);
            Assert.That(_simulation.HeroCount, Is.EqualTo(1));
            Assert.That(_reconnect.Count, Is.EqualTo(1));

            // Act.
            var secondConnection = new NetworkConnectionToClient(2) { isAuthenticated = true, isReady = true };
            HeroNet second = _factory.SpawnPlayer(secondConnection);
            runner.Advance(ServerTickRunner.StepDelta, NetworkTime.time + 31, true);
            var thirdConnection = new NetworkConnectionToClient(3) { isAuthenticated = true, isReady = true };
            HeroNet replacement = _factory.SpawnPlayer(thirdConnection);

            // Assert.
            Assert.That(second.Slot, Is.EqualTo(1));
            Assert.That(replacement.Slot, Is.Zero);
            Assert.That(replacement.SessionPlayerId, Is.Not.EqualTo(playerId));
            Assert.That(_reconnect.Count, Is.Zero);
            Assert.That(_simulation.TryGetHero(playerId, out _), Is.False);
            Assert.That(_simulation.HeroCount, Is.EqualTo(2));
        }

        [Test]
        public void Advance_ServerInactive_DoesNotStepOrPublish()
        {
            // Arrange.
            var sink = Substitute.For<IMatchStateSink>();
            var runner = new ServerTickRunner(_simulation, sink, _factory, _commands);

            // Act.
            int steps = runner.Advance(1, 100, false);

            // Assert.
            Assert.That(steps, Is.Zero);
            Assert.That(_simulation.Time, Is.Zero);
            sink.DidNotReceive().Apply(Arg.Any<MatchSimulation>());
        }

        [Test]
        public void Advance_LongFrame_PublishesFourStepsAndDropsBacklog()
        {
            // Arrange.
            var sink = Substitute.For<IMatchStateSink>();
            var runner = new ServerTickRunner(_simulation, sink, _factory, _commands);

            // Act.
            int steps = runner.Advance(1, 100, true);
            int nextSteps = runner.Advance(0, 100, true);

            // Assert.
            Assert.That(steps, Is.EqualTo(4));
            Assert.That(nextSteps, Is.Zero);
            Assert.That(_simulation.Time, Is.EqualTo(100).Within(0.000001));
            sink.Received(4).Apply(_simulation);
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
