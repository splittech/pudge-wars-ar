using Game.Tests.Support;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Net;
using kcp2k;
using Game.Core.Commands;
using Game.Input.Gestures;
using Game.Services.LoggingService;
using NSubstitute;
using R3;
using Game.Core.Configs;
using Game.Core.Hero;
using Game.Core.Match;
using Game.Input.Routing;
using Unity.Mathematics;
using UnityEngine;
using VContainer;

namespace Game.Tests.EditMode
{
    public static class Setup
    {
        public const float TickDeltaTime = 1f / 30f;

        public static ArenaConfigData Arena(float2? size = null, float riverWidth = 3.5f)
        {
            return new ArenaConfigData(size ?? new float2(24f, 14f), riverWidth,
                Array.Empty<float2>(), Array.Empty<float2>());
        }

        public static HeroConfigData HeroConfig()
        {
            return new HeroConfigData(1000, 3.2f, 0.5f, 4f);
        }

        public static HookConfigData HookConfig(float speed = 20f)
        {
            return new HookConfigData(12f, speed, 0.35f, 175, 3f, 1.5f, 4f, 0.08f, 75, 4);
        }

        public static HeroState Hero(uint id = 1, Team team = Team.A, float x = -4f, float y = 0f,
            int hp = 1000, bool alive = true, bool isPulled = false)
        {
            return new HeroState
            {
                Id = id,
                Team = team,
                Pos = new float2(x, y),
                MoveTarget = new float2(x, y),
                Radius = 0.5f,
                Hp = hp,
                Alive = alive,
                IsPulled = isPulled
            };
        }

        public static MatchSimulation MatchSimulation()
        {
            return MatchSimulation(new[] { Hero(), Hero(id: 2, team: Team.B, x: 4f) });
        }

        public static MatchSimulation MatchSimulation(HeroState[] heroes, float hookSpeed = 20f)
        {
            return new MatchSimulation(Arena(), HeroConfig(), HookConfig(hookSpeed), heroes);
        }

        public static void Advance(MatchSimulation simulation, int ticks)
        {
            for (int i = 0; i < ticks; i++)
                simulation.Step(TickDeltaTime);
        }

        public static InputRouter InputRouter(
            out Subject<Vector2> onTap,
            out Subject<DragGesture> onDrag,
            out IArenaSpace arenaSpace,
            out ILocalHero localHero,
            out IPlayerCommands commands,
            out InputConfig config)
        {
            onTap = new Subject<Vector2>();
            onDrag = new Subject<DragGesture>();

            var gestures = Substitute.For<ITouchGestures>();
            gestures.Tap.Returns(onTap);
            gestures.Drag.Returns(onDrag);
            gestures.PinchTwist.Returns(Observable.Empty<PinchTwistGesture>());

            arenaSpace = Substitute.For<IArenaSpace>();
            arenaSpace.TryScreenToArena(Arg.Any<Vector2>(), out Arg.Any<float2>())
                .Returns(call =>
                {
                    Vector2 point = call.ArgAt<Vector2>(0);
                    call[1] = new float2(point.x, point.y);
                    return true;
                });
            arenaSpace.ArenaToWorld(Arg.Any<float2>()).Returns(call =>
            {
                float2 point = call.ArgAt<float2>(0);
                return new Vector3(point.x, 0f, point.y);
            });

            localHero = Substitute.For<ILocalHero>();
            localHero.TryGetPosition(out Arg.Any<float2>()).Returns(call =>
            {
                call[0] = float2.zero;
                return true;
            });
            commands = Substitute.For<IPlayerCommands>();
            config = ScriptableObject.CreateInstance<InputConfig>();

            InputRouter router = new(gestures, arenaSpace, localHero, commands, config);
            router.Start();
            return router;
        }
        public static TestLoadingScreen LoadingScreen(out GameObject root)
        {
            root = new GameObject("Loading Screen Test");
            var screen = root.AddComponent<TestLoadingScreen>();
            var loggingService = Substitute.For<ILoggingService>();
            loggingService.GetLogger(Arg.Any<LoggingChannel>()).Returns(Substitute.For<IGameLogger>());
            screen.Construct(loggingService);
            return screen;
        }

        public static INetworkSceneLoader NetworkSceneLoader(
            out UniTaskCompletionSource completion, out List<string> requests)
        {
            var pendingLoad = new UniTaskCompletionSource();
            var loadedPaths = new List<string>();
            var loader = Substitute.For<INetworkSceneLoader>();
            loader.IsReadyToStart.Returns(true);
            loader.OfflineScenePath.Returns("Menu");
            loader.OnlineScenePath.Returns("Gameplay");
            loader.LoadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                loadedPaths.Add(call.ArgAt<string>(0));
                return pendingLoad.Task;
            });
            completion = pendingLoad;
            requests = loadedPaths;
            return loader;
        }

        public static PudgeNetworkManager NetworkManager(INetworkSceneLoader loader, out GameObject root)
        {
            root = new GameObject("Network scene test");
            root.SetActive(false);
            var transport = root.AddComponent<KcpTransport>();
            var manager = root.AddComponent<PudgeNetworkManager>();
            manager.transport = transport;
            manager.Construct(loader);
            return manager;
        }

        public static IObjectResolver NetworkSpawnContainer(ArenaConfig arena, MatchConfig match)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(arena);
            builder.RegisterInstance(match);
            return builder.Build();
        }

        public static IObjectResolver EditModeNetworkSpawnResolver(IObjectResolver container)
        {
            var resolver = Substitute.For<IObjectResolver>();
            resolver.When(value => value.Inject(Arg.Any<object>())).Do(call =>
            {
                object instance = call[0];
                container.Inject(instance);
                // Mirror initializes its behaviour cache in Awake, which EditMode does not call.
                if (instance is Mirror.NetworkIdentity identity)
                    typeof(Mirror.NetworkIdentity).GetMethod("Awake",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(identity, null);
            });
            return resolver;
        }
    }
}
