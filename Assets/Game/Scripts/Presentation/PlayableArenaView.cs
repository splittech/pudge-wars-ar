using Game.Core.Configs;
using Game.Core.Geometry;
using Game.Core.Hook;
using Game.Core.Hero;
using Game.Core.Match;
using Game.Input.Routing;

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
        [SerializeField] private float _hookWidth = 0.12f;
        [SerializeField] private float _aimWidth = 0.045f;

        [SerializeField] private Camera _camera;

        private InputRouter _router;
        private float _range;
        private Vector2 _size;
        private float _aspect;
        private RoomArena _cameraRoom;
        private bool _cameraAR;
        private Vector2 _defaultSize;
        private Vector3 _cameraHome;
        private void Awake()
        {
            ArenaLineRenderer.Initialize(_hook);
            ArenaLineRenderer.Initialize(_aim);
        }

        private void LateUpdate()
        {
            bool room = Game.Net.MatchStateNet.Active?.IsRoom == true;
            _floor.gameObject.SetActive(!room);
            _river.gameObject.SetActive(!room);
            UpdateRoomCamera();

            var hero = Game.Net.HeroNet.Local;
            if (ReferenceEquals(hero, null))
            {
                return;
            }

            bool visible = hero.CanAct && hero.Alive;
            _aim.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var aim = _router.Aim.CurrentValue;
            _aim.enabled = aim.IsActive;
            float range = hero.CurrentStatus.Range > 0 ? hero.CurrentStatus.Range : _range;
            Vector3 position = hero.DisplayPosition;
            Color color = aim.IsInCancelZone ? Color.red : new Color(0.5f, 1, 0.7f);
            _aim.startColor = color;
            _aim.endColor = color;
            DrawAim(new float2(position.x, position.z), aim, range);
        }

        [Inject]
        public void Construct(InputRouter router)
        {
            _router = router;
        }

        public void Configure(ArenaConfigData arena, float range)
        {
            _range = range;
            _size = new Vector2(arena.Size.x, arena.Size.y);
            _defaultSize = _size;
            _cameraHome = _camera.transform.position;
            _floor.localScale = new Vector3(arena.Size.x, 0.15f, arena.Size.y);
            _river.localScale = new Vector3(arena.RiverWidth, 0.05f, arena.Size.y);
            FitCamera();
        }

        public void SetSandboxVisible(bool visible)
        {
            _hero.gameObject.SetActive(visible);
            _dummy.gameObject.SetActive(visible);
            _hook.gameObject.SetActive(visible);
            _aim.gameObject.SetActive(visible);
        }

        private void FitCamera()
        {
            _aspect = _camera.aspect;
            _camera.orthographicSize = Mathf.Max(_size.y / 2f / Mathf.Sin(60 * Mathf.Deg2Rad),
                _size.x / (2f * _aspect)) * 1.2f;
        }

        private void UpdateRoomCamera()
        {
            RoomArena room = Game.Net.MatchStateNet.Active?.Room;
            bool useAR = ArenaMode.UseAR;
            if (ReferenceEquals(room, _cameraRoom) && useAR == _cameraAR && Mathf.Approximately(_aspect, _camera.aspect))
            {
                return;
            }

            _cameraRoom = room;
            _cameraAR = useAR;
            _size = _defaultSize;
            _camera.transform.position = _cameraHome;
            if (room != null && !useAR)
            {
                float2 size = room.Bounds.Max - room.Bounds.Min;
                float2 center = (room.Bounds.Max + room.Bounds.Min) * 0.5f;
                _size = new Vector2(size.x, size.y);
                _camera.transform.position += transform.TransformVector(new Vector3(center.x, 0, center.y));
            }

            FitCamera();
        }

        public void Render(
            MatchSimulation simulation,
            HeroState previousHero,
            HeroState previousDummy,
            HookState previousHook,
            float alpha)
        {
            if (_camera.aspect != _aspect)
            {
                FitCamera();
            }

            var hero = simulation.GetHero(0);
            var dummy = simulation.GetHero(1);
            float2 heroPosition = math.lerp(previousHero.Pos, hero.Pos, alpha);
            float2 dummyPosition = math.lerp(previousDummy.Pos, dummy.Pos, alpha);
            _hero.localPosition = Point(heroPosition.x, heroPosition.y, 0.75f);
            _dummy.localPosition = Point(dummyPosition.x, dummyPosition.y, 0.75f);
            _dummy.localScale = dummy.Alive ? new Vector3(1, 0.75f, 1) : new Vector3(1, 0.3f, 1);
            HookState hook = simulation.GetHook(0);
            _hook.enabled = hook.Phase != HookPhase.Idle;
            float2 tip = hook.Tip;
            bool sameSegment = hook.Phase == previousHook.Phase && math.all(hook.Origin == previousHook.Origin)
                && hook.ReturnIndex == previousHook.ReturnIndex;
            if (sameSegment)
            {
                tip = math.lerp(previousHook.Tip, hook.Tip, alpha);
            }

            int count = hook.Path.Count;
            if (hook.Phase != HookPhase.Extending)
            {
                count = math.clamp(hook.ReturnIndex + 1, 0, count);
            }

            ArenaLineRenderer.Draw(
                _hook,
                transform,
                new Vector2(heroPosition.x, heroPosition.y),
                hook.Path,
                count,
                new Vector2(tip.x, tip.y),
                0.5f,
                _hookWidth);
            AimState aim = _router.Aim.CurrentValue;
            _aim.enabled = aim.IsActive;
            Color color = aim.IsInCancelZone ? Color.red : new Color(0.5f, 1, 0.7f);
            _aim.startColor = color;
            _aim.endColor = color;
            DrawAim(hero.Pos, aim, _range);
        }

        private void DrawAim(float2 position, AimState aim, float range)
        {
            ArenaLineRenderer.Draw(
                _aim,
                transform,
                new Vector2(position.x, position.y),
                default,
                0,
                new Vector2(position.x + aim.Direction.x * range, position.y + aim.Direction.y * range),
                0.55f,
                _aimWidth);
        }

        private static Vector3 Point(float x, float y, float height) => new(x, height, y);
    }
}
