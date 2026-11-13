using TMPro;
using Mirror;
using Game.Net;
using UnityEngine;

namespace Game.App
{
    public sealed class DevelopmentOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [SerializeField, Min(0.1f)] private float _refreshInterval = 0.5f;

        private float _elapsed;
        private int _frames;
        private int _lastCorrections;
        private int _lastPackets;
        private int _lastSnapshots;

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

            string role = NetworkServer.active ? (NetworkClient.active ? "host" : "server")
                : NetworkClient.isConnected ? "client" : "offline";
            string rtt = NetworkClient.isConnected ? $"{NetworkTime.rtt * 1000:0} ms" : "-";
            _label.text = $"FPS: {_frames / _elapsed:0} | v{Application.version} | {role} | RTT: {rtt}";
            var hero = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<HeroNet>() : null;
            if (hero != null)
            {
                var movement = hero.Movement;
                string color = movement.Error <= 0.1f ? "#55ee88" : "#ff5555";
                float corrections = Mathf.Max(0, movement.Corrections - _lastCorrections) / _elapsed;
                float sent = Mathf.Max(0, hero.MovementPackets - _lastPackets) / _elapsed;
                float received = Mathf.Max(0, hero.MovementSnapshots - _lastSnapshots) / _elapsed;
                _label.text += $"\nPred {(NetworkServer.active ? "HOST" : hero.PredictionEnabled ? "ON" : "OFF")} <color={color}>err {movement.Error:0.000}</color>"
                    + $" c/s {corrections:0} h {movement.HistorySize} ack {movement.AckSequence}/t{movement.LastServerTick}"
                    + $" send/snap {sent:0}/{received:0}";
                _lastCorrections = movement.Corrections; _lastPackets = hero.MovementPackets;
                _lastSnapshots = hero.MovementSnapshots;
            }
            _elapsed = 0;
            _frames = 0;
        }
    }
}
