using System;
using Game.Net;
using R3;
using UnityEngine;

namespace Game.UI
{
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private HeroNet _hero;
        private UnityEngine.UI.Image _fill;
        private Canvas _canvas;
        private IDisposable _subscription;

        public void Initialize(HeroNet hero, int maxHp)
        {
            _hero = hero;
            // A separate world canvas keeps its rotation independent of the screen-space HUD.
            var root = new GameObject("World Canvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            _canvas = root.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(120, 12);
            var background = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            background.transform.SetParent(root.transform, false);
            var rect = (RectTransform)background.transform; rect.sizeDelta = new Vector2(120, 12);
            var image = background.GetComponent<UnityEngine.UI.Image>(); image.color = Color.black; image.raycastTarget = false;
            var fill = new GameObject("HP", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(1, 1); fillRect.offsetMax = new Vector2(-1, -1);
            _fill = fill.GetComponent<UnityEngine.UI.Image>(); _fill.color = Color.green; _fill.raycastTarget = false;
            _subscription = hero.Health.Subscribe(hp => fillRect.anchorMax = new Vector2(Mathf.Clamp01((float)hp / maxHp), 1));
        }

        private void LateUpdate()
        {
            if (ReferenceEquals(_hero, null) || _canvas == null) return;
            if (!_hero.IsViewAvailable) { _canvas.enabled = false; return; }
            bool visible = _hero.Alive && _hero.CanAct;
            _canvas.enabled = visible;
            if (!visible) return;
            _canvas.transform.position = _hero.WorldViewPosition + Vector3.up * (1.8f * _hero.ArenaScale);
            _canvas.transform.localScale = Vector3.one * (0.012f * _hero.ArenaScale);
            var camera = Camera.main;
            if (camera != null) _canvas.transform.rotation = camera.transform.rotation;
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
