using Game.Core.Match;
using Game.Net;
using UnityEngine;
using System;
using R3;

namespace Game.Presentation
{
    public sealed class NetworkHeroView : MonoBehaviour
    {
        [SerializeField] private HeroNet _hero;
        [SerializeField] private Renderer _body;
        [SerializeField] private GameObject _localRing;
        private MaterialPropertyBlock _properties;
        private Team? _shownTeam;
        private IDisposable _healthSubscription;
        private int _lastHp = -1;
        private float _flashUntil;
        private bool _shownFlash;
        private Vector3 _bodyOrigin;
        private Vector3 _ringOrigin;

        private void Awake()
        {
            _bodyOrigin = _body.transform.localPosition;
            _ringOrigin = _localRing.transform.localPosition;
        }

        private void OnEnable()
        {
            _healthSubscription = _hero.Health.Subscribe(hp =>
            {
                if (_lastHp >= 0 && hp < _lastHp)
                    _flashUntil = Time.unscaledTime + 0.15f;
                _lastHp = hp;
            });
        }

        private void OnDisable()
        {
            _healthSubscription?.Dispose();
            _healthSubscription = null;
        }

        private void LateUpdate()
        {
            Vector3 delta = _hero.DisplayPosition - transform.localPosition;
            if (_body.transform != transform) _body.transform.localPosition = _bodyOrigin + delta;
            if (_localRing.transform != transform) _localRing.transform.localPosition = _ringOrigin + delta;
            _hero.PresentedMovementReporter.Invoke(_hero.DisplayPosition);
            bool flash = Time.unscaledTime < _flashUntil;
            if (_shownTeam != _hero.Team || _shownFlash != flash)
            {
                _properties ??= new MaterialPropertyBlock();
                _body.GetPropertyBlock(_properties);
                _properties.SetColor("_BaseColor", flash ? Color.white : (_hero.Team == Team.A
                    ? new Color(0.15f, 0.55f, 1f) : new Color(1f, 0.4f, 0.12f)));
                _body.SetPropertyBlock(_properties);
                _shownTeam = _hero.Team;
                _shownFlash = flash;
            }
            if (_localRing.activeSelf != _hero.IsLocalHero)
                _localRing.SetActive(_hero.IsLocalHero);
        }
    }
}
