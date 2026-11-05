using Game.Core.Match;
using Game.Net;
using UnityEngine;

namespace Game.Presentation
{
    public sealed class NetworkHeroView : MonoBehaviour
    {
        [SerializeField] private HeroNet _hero;
        [SerializeField] private Renderer _body;
        [SerializeField] private GameObject _localRing;
        private MaterialPropertyBlock _properties;
        private Team? _shownTeam;

        private void LateUpdate()
        {
            if (_shownTeam != _hero.Team)
            {
                _properties ??= new MaterialPropertyBlock();
                _body.GetPropertyBlock(_properties);
                _properties.SetColor("_BaseColor", _hero.Team == Team.A
                    ? new Color(0.15f, 0.55f, 1f) : new Color(1f, 0.4f, 0.12f));
                _body.SetPropertyBlock(_properties);
                _shownTeam = _hero.Team;
            }
            if (_localRing.activeSelf != _hero.IsLocalHero)
                _localRing.SetActive(_hero.IsLocalHero);
        }
    }
}
