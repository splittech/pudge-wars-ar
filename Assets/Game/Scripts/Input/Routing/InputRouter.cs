using System;
using Game.Core.Commands;
using Game.Core.Configs;
using Game.Input.Gestures;
using R3;
using Unity.Mathematics;
using UnityEngine;
using VContainer.Unity;

namespace Game.Input.Routing
{
    /// <summary>
    /// Turns gestures into player intents: tap moves the hero (FR-IN-01), a drag started near the own hero
    /// aims the hook and throws it on release (FR-IN-02), returning the finger to the hero cancels the throw (FR-IN-03).
    /// </summary>
    public sealed class InputRouter : IStartable, IDisposable
    {
        private readonly ITouchGestures _gestures;
        private readonly IArenaSpace _arenaSpace;
        private readonly ILocalHero _localHero;
        private readonly IPlayerCommands _commands;
        private readonly float _heroGrabRadius;

        private readonly ReactiveProperty<AimState> _aim = new(AimState.Inactive);

        private IDisposable _subscriptions;

        public ReadOnlyReactiveProperty<AimState> Aim => _aim;

        public InputRouter(
            ITouchGestures gestures,
            IArenaSpace arenaSpace,
            ILocalHero localHero,
            IPlayerCommands commands,
            InputConfig config)
        {
            _gestures = gestures;
            _arenaSpace = arenaSpace;
            _localHero = localHero;
            _commands = commands;
            _heroGrabRadius = config.ToData().HeroGrabRadius;
        }

        public void Start()
        {
            _subscriptions = Disposable.Combine(
                _gestures.Tap.Subscribe(OnTap),
                _gestures.Drag.Subscribe(OnDrag));
        }

        public void Dispose()
        {
            _subscriptions?.Dispose();
            _aim.Dispose();
        }

        private void OnTap(Vector2 screenPoint)
        {
            if (_arenaSpace.TryScreenToArena(screenPoint, out float2 arenaPoint))
                _commands.MoveTo(arenaPoint);
        }

        private void OnDrag(DragGesture drag)
        {
            switch (drag.Phase)
            {
                case DragPhase.Began:
                    if (CanStartAim(drag.StartPosition))
                        UpdateAim(drag.Position);
                    break;

                case DragPhase.Moved:
                    if (_aim.Value.IsActive)
                        UpdateAim(drag.Position);
                    break;

                case DragPhase.Ended:
                    if (_aim.Value.IsActive)
                        ReleaseAim(drag.Position);
                    break;

                case DragPhase.Cancelled:
                    _aim.Value = AimState.Inactive;
                    break;
            }
        }

        private bool CanStartAim(Vector2 screenPoint)
        {
            return _localHero.TryGetPosition(out float2 heroPosition)
                && _arenaSpace.TryScreenToArena(screenPoint, out float2 arenaPoint)
                && math.distancesq(arenaPoint, heroPosition) <= _heroGrabRadius * _heroGrabRadius;
        }

        private void ReleaseAim(Vector2 screenPoint)
        {
            UpdateAim(screenPoint);

            AimState aim = _aim.Value;
            _aim.Value = AimState.Inactive;

            if (aim.IsActive && !aim.IsInCancelZone)
                _commands.ThrowHook(aim.Direction);
        }

        private void UpdateAim(Vector2 screenPoint)
        {
            // The hero keeps moving while aiming, so the origin is re-read on every gesture event.
            if (!_localHero.TryGetPosition(out float2 heroPosition))
            {
                _aim.Value = AimState.Inactive;
                return;
            }

            // Keep the last valid direction if the finger leaves the arena plane.
            if (!_arenaSpace.TryScreenToArena(screenPoint, out float2 arenaPoint))
                return;

            float2 offset = arenaPoint - heroPosition;
            bool isInCancelZone = math.lengthsq(offset) <= _heroGrabRadius * _heroGrabRadius;
            float2 direction = isInCancelZone ? float2.zero : math.normalize(offset);

            _aim.Value = new AimState(heroPosition, direction, isInCancelZone);
        }
    }
}
