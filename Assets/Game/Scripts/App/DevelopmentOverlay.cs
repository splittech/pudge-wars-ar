using TMPro;
using UnityEngine;

namespace Game.App
{
    public sealed class DevelopmentOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField, Min(0.1f)] private float _refreshInterval = 0.5f;

        private float _elapsed;
        private int _frames;

        private void Awake()
        {
            if (!Debug.isDebugBuild)
                gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            _frames = 0;
            _label.text = $"v{Application.version}";
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            _frames++;
            if (_elapsed < _refreshInterval)
                return;

            _label.text = $"FPS: {_frames / _elapsed:0} | v{Application.version}";
            _elapsed = 0;
            _frames = 0;
        }
    }
}
