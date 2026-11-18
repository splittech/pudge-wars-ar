using System;
using Game.Core.Configs;
using Game.Core.Hook;
using Game.Net;
using R3;
using UnityEngine;
using VContainer;

namespace Game.Presentation
{
    public sealed class NetworkHookView : MonoBehaviour
    {
        [SerializeField] private HeroNet _hero;
        [SerializeField] private LineRenderer _line;
        [SerializeField] private float _height = 0.5f;
        [SerializeField] private float _width = 0.08f;
        private HookConfigData _config;
        private IDisposable _subscriptions;
        private float _flashUntil;
        private MaterialPropertyBlock _properties;

        [Inject]
        public void Construct(HookConfig config)
        {
            _config = config.ToData();
        }

        private void Awake()
        {
            ArenaLineRenderer.Initialize(_line);
        }

        private void OnEnable()
        {
            if (ReferenceEquals(_hero, null) || _line == null)
            {
                return;
            }

            _subscriptions = Disposable.Combine(
                _hero.HookHits.Subscribe(_ => _flashUntil = Time.unscaledTime + 0.15f),
                _hero.HookClashes.Subscribe(_ => _flashUntil = Time.unscaledTime + 0.15f));
        }

        private void OnDisable()
        {
            _subscriptions?.Dispose();
            _subscriptions = null;
            if (_line != null)
            {
                _line.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (ReferenceEquals(_hero, null))
            {
                return;
            }

            var state = _hero.DisplayHookState;
            bool predicted = _hero.IsHookPredicted;
            bool visible = _hero.IsClientView && _hero.CanAct && state.Phase != HookPhase.Idle;
            _line.enabled = visible;
            if (!visible)
            {
                return;
            }

            Transform arena = transform.parent;
            Vector3 position = _hero.DisplayPosition;
            var owner = new Vector2(position.x, position.z);
            double now = _hero.SynchronizedTime;
            float speed = Mathf.Max(0, _hero.HookViewSpeed);
            Vector2 tip = state.EvaluateTip(
                now,
                owner,
                speed,
                _hero.HookViewRange > 0 ? _hero.HookViewRange : _config.Range);
            ArenaLineRenderer.Draw(
                _line,
                arena,
                owner,
                state.Path,
                state.RemainingPathCount(now, owner, speed),
                tip,
                _height,
                _width);
            Color color;
            if (Time.unscaledTime < _flashUntil)
            {
                color = Color.white;
            }
            else if (predicted)
            {
                color = new Color(0.2f, 0.9f, 1f);
            }
            else
            {
                color = new Color(1, 0.8f, 0.15f);
            }

            _line.startColor = color;
            _line.endColor = color;
            _properties ??= new MaterialPropertyBlock();
            _properties.SetColor("_BaseColor", color);
            _line.SetPropertyBlock(_properties);
        }
    }
}
