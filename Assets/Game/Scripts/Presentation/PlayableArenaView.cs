using Game.Core.Configs;
using Game.Core.Hook;
using Game.Core.Hero;
using Game.Core.Match;
using Game.Input.Routing;
using TMPro;
using UnityEngine;
using VContainer;
using Unity.Mathematics;

namespace Game.Presentation
{
    public sealed class PlayableArenaView : MonoBehaviour
    {
        [SerializeField] private Transform _hero;
        [SerializeField] private Transform _dummy;
        [SerializeField] private Transform _floor;
        [SerializeField] private Transform _river;
        [SerializeField] private LineRenderer _hook;
        [SerializeField] private LineRenderer _aim;
        [SerializeField] private TMP_Text _status;
        [SerializeField] private Camera _camera;
        private InputRouter _router;
        private float _range;
        private Vector2 _size;
        private float _aspect;
        private Camera _previousCamera;
        private int _lastHp = -1;
        private int _lastCooldown = -1;
        private HookPhase _lastPhase;

        private void OnEnable()
        {
            _previousCamera = Camera.main;
            if (_previousCamera != null && _previousCamera != _camera)
                _previousCamera.enabled = false;
        }

        private void OnDisable()
        {
            if (_previousCamera != null && _previousCamera != _camera)
                _previousCamera.enabled = true;
        }

        [Inject]
        public void Construct(InputRouter router) => _router = router;

        public void Configure(ArenaConfigData arena, float range)
        {
            _range = range;
            _size = new Vector2(arena.Size.x, arena.Size.y);
            _floor.localScale = new Vector3(arena.Size.x, 0.15f, arena.Size.y);
            _river.localScale = new Vector3(arena.RiverWidth, 0.05f, arena.Size.y);
            FitCamera();
        }

        private void FitCamera()
        {
            _aspect = _camera.aspect;
            _camera.orthographicSize = Mathf.Max(_size.y / 2f / Mathf.Sin(60 * Mathf.Deg2Rad),
                _size.x / (2f * _aspect)) * 1.2f;
        }

        public void Render(MatchSimulation simulation, HeroState previousHero, HeroState previousDummy,
            HookState previousHook, float alpha)
        {
            if (_camera.aspect != _aspect)
                FitCamera();
            var hero = simulation.GetHero(0);
            var dummy = simulation.GetHero(1);
            float2 heroPosition = math.lerp(previousHero.Pos, hero.Pos, alpha);
            float2 dummyPosition = math.lerp(previousDummy.Pos, dummy.Pos, alpha);
            _hero.localPosition = Point(heroPosition.x, heroPosition.y, 0.75f);
            _dummy.localPosition = Point(dummyPosition.x, dummyPosition.y, 0.75f);
            _dummy.localScale = dummy.Alive ? new Vector3(1, 0.75f, 1) : new Vector3(1, 0.3f, 1);
            HookState hook = simulation.GetHook(0);
            _hook.enabled = hook.Phase != HookPhase.Idle;
            float2 tip = hook.Phase == previousHook.Phase ? math.lerp(previousHook.Tip, hook.Tip, alpha) : hook.Tip;
            _hook.SetPosition(0, Point(heroPosition.x, heroPosition.y, 0.5f));
            _hook.SetPosition(1, Point(tip.x, tip.y, 0.5f));
            AimState aim = _router.Aim.CurrentValue;
            _aim.enabled = aim.IsActive;
            _aim.startColor = _aim.endColor = aim.IsInCancelZone ? Color.red : new Color(0.5f, 1, 0.7f);
            _aim.SetPosition(0, Point(hero.Pos.x, hero.Pos.y, 0.55f));
            _aim.SetPosition(1, Point(hero.Pos.x + aim.Direction.x * _range,
                hero.Pos.y + aim.Direction.y * _range, 0.55f));
            double cooldown = System.Math.Max(0, hero.HookCooldownEnd - simulation.Time);
            int cooldownTenths = (int)System.Math.Ceiling(cooldown * 10);
            if (_lastHp != dummy.Hp || _lastPhase != hook.Phase || _lastCooldown != cooldownTenths)
            {
                _lastHp = dummy.Hp; _lastPhase = hook.Phase; _lastCooldown = cooldownTenths;
                _status.text = $"TRAINING  |  Dummy HP: {dummy.Hp}\nHook: {hook.Phase}  |  Cooldown: {cooldownTenths / 10f:0.0}s\nTap to move. Drag from the blue hero to aim; release to throw.";
            }
        }

        private static Vector3 Point(float x, float y, float height) => new(x, height, y);
    }
}
