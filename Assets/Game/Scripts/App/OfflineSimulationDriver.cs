using Game.Core.Commands;
using Game.Core.Configs;
using Game.Core.Economy;
using Game.Core.Hero;
using Game.Core.Hook;
using Game.Core.Match;
using Game.Core.Geometry;
using Game.Presentation;
using Unity.Mathematics;
using UnityEngine;
using VContainer;

namespace Game.App
{
    public sealed class OfflineSimulationDriver : MonoBehaviour, IPlayerCommands, ILocalHero
    {
        [SerializeField] private PlayableArenaView _view;
        [SerializeField] private bool _drawSimulationGizmos = true;
        [SerializeField, Min(0)]
        [Tooltip("Temporary FPS limit. Zero restores the original frame rate and VSync settings.")]
        private int _frameRateLimit;
        private bool _frameLimitApplied;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private ArenaConfigData _arena;
        private HeroConfigData _hero;
        private HookConfigData _hook;
        private float _accumulator;
        private double _dummyResetAt = double.PositiveInfinity;
        private HeroState _previousHero;
        private HeroState _previousDummy;
        private HookState _previousHook;
        public MatchSimulation Simulation { get; private set; }

        [Inject]
        public void Construct(ArenaConfig arena, HeroConfig hero, HookConfig hook)
        {
            _arena = arena.ToData(); _hero = hero.ToData(); _hook = hook.ToData();
            bool networkSession = Mirror.NetworkServer.active || Mirror.NetworkClient.active;
            _view.Configure(_arena, _hook.Range);
            _view.SetSandboxVisible(!networkSession);
            enabled = Debug.isDebugBuild && !networkSession;
            if (enabled)
            {
                ResetSandbox();
            }
        }

        public void ResetSandbox()
        {
            Simulation = new MatchSimulation(_arena, _hero, _hook,
                new[] { NewHero(1, Team.A, new float2(-4, 0)), NewHero(2, Team.B, new float2(4, 0)) },
                UnityEngine.Time.timeAsDouble);
            _accumulator = 0;
            _dummyResetAt = double.PositiveInfinity;
            CapturePreviousState();
        }

        private void CapturePreviousState()
        {
            _previousHero = Simulation.GetHero(0);
            _previousDummy = Simulation.GetHero(1);
            _previousHook = Simulation.GetHook(0);
        }

        private HeroState NewHero(uint id, Team team, float2 position) => new()
        {
            Id = id, Team = team, Pos = position, MoveTarget = position,
            Radius = _hero.Radius, Hp = _hero.MaxHp, Alive = true
        };

        private void Update()
        {
            ApplyFrameLimit();
            if (Simulation == null)
                return;
            const float dt = 1f / 30f;
            _accumulator += UnityEngine.Time.deltaTime;
            int steps = 0;
            while (_accumulator >= dt && steps++ < 4)
            {
                CapturePreviousState();
                Simulation.Step(dt);
                _accumulator -= dt;
                if (Simulation.Hits.Count > 0)
                    _dummyResetAt = Simulation.Time + 2;
                if (Simulation.Time >= _dummyResetAt && Simulation.GetHook(0).Phase == HookPhase.Idle)
                {
                    Simulation = new MatchSimulation(_arena, _hero, _hook,
                        new[] { Simulation.GetHero(0), NewHero(2, Team.B, new float2(4, 0)) }, Simulation.Time);
                    _dummyResetAt = double.PositiveInfinity;
                    CapturePreviousState();
                }
            }
            if (_accumulator >= dt)
                _accumulator %= dt;
            _view.Render(Simulation, _previousHero, _previousDummy, _previousHook, _accumulator / dt);
        }

        public bool TryGetPosition(out float2 position)
        {
            position = Simulation != null ? Simulation.GetHero(0).Pos : default;
            return Simulation != null && Simulation.GetHero(0).Alive;
        }

        public void MoveTo(float2 point) => Simulation?.Enqueue(new MoveIntent(1, point));
        public void ThrowHook(float2 direction) => Simulation?.Enqueue(new ThrowHookIntent(1, direction, Simulation.Time));
        public void BuyUpgrade(UpgradeId id) { }

        private void ApplyFrameLimit()
        {
            if (!Application.isPlaying || !Debug.isDebugBuild)
                return;
            if (_frameRateLimit > 0)
            {
                if (!_frameLimitApplied)
                {
                    _previousTargetFrameRate = Application.targetFrameRate;
                    _previousVSyncCount = QualitySettings.vSyncCount;
                    _frameLimitApplied = true;
                }
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = _frameRateLimit;
            }
            else
                RestoreFrameLimit();
        }

        private void OnDisable() => RestoreFrameLimit();

        private void RestoreFrameLimit()
        {
            if (!_frameLimitApplied)
                return;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
            _frameLimitApplied = false;
        }

        private void OnDrawGizmos()
        {
            if (!_drawSimulationGizmos || Simulation == null || _view == null)
                return;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = _view.transform.localToWorldMatrix;
            for (int i = 0; i < Simulation.HeroCount; i++)
            {
                HeroState hero = Simulation.GetHero(i);
                Gizmos.color = hero.Team == Team.A ? Color.cyan : new Color(1, 0.5f, 0);
                Rect2 bounds = ArenaBounds.ForTeam(_arena, hero.Team, hero.Radius);
                float2 center = (bounds.Min + bounds.Max) * 0.5f;
                float2 size = bounds.Max - bounds.Min;
                Gizmos.DrawWireCube(new Vector3(center.x, 0.03f, center.y), new Vector3(size.x, 0, size.y));
                DrawCircle(hero.Pos, hero.Radius);
                HookState hook = Simulation.GetHook(i);
                if (hook.Phase == HookPhase.Idle)
                    continue;
                Gizmos.color = Color.yellow;
                DrawCircle(hook.Tip, _hook.Radius);
                Gizmos.DrawLine(new Vector3(hero.Pos.x, 0.5f, hero.Pos.y),
                    new Vector3(hook.Tip.x, 0.5f, hook.Tip.y));
            }
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private static void DrawCircle(float2 center, float radius)
        {
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = i * (math.PI * 2 / segments);
                float b = (i + 1) * (math.PI * 2 / segments);
                Gizmos.DrawLine(
                    new Vector3(center.x + math.cos(a) * radius, 0.5f, center.y + math.sin(a) * radius),
                    new Vector3(center.x + math.cos(b) * radius, 0.5f, center.y + math.sin(b) * radius));
            }
        }
    }
}
