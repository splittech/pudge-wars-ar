using System;
using Mirror;
using Game.Net;
using UnityEngine;

namespace Game.App
{
    public sealed class DevelopmentOverlay : MonoBehaviour, IDiagnosticsSource
    {
        private string _text;
        [SerializeField, Min(0.1f)] private float _refreshInterval = 0.5f;

        private float _elapsed;
        private int _frames;
        private int _lastCorrections;
        private int _lastPackets;
        private int _lastSnapshots;

        private void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            _frames = 0;
            Publish($"v{Application.version}");
        }

        public DiagnosticsSnapshot Snapshot { get; private set; }

        public event Action<DiagnosticsSnapshot> Changed;

        public void ToggleArenaMode()
        {
            Game.Input.Routing.ArenaMode.UseAR = !Game.Input.Routing.ArenaMode.UseAR;
            Publish(_text ?? $"v{Application.version}");
        }

        private void Publish(string text)
        {
            Snapshot = new DiagnosticsSnapshot(text, Game.Input.Routing.ArenaMode.UseAR);
            Changed?.Invoke(Snapshot);
        }

        private void Update()
        {
            if (Snapshot.UseAR != Game.Input.Routing.ArenaMode.UseAR)
            {
                Publish(_text ?? $"v{Application.version}");
            }
            _elapsed += Time.unscaledDeltaTime;
            _frames++;
            if (_elapsed < _refreshInterval)
            {
                return;
            }

            string role = "offline";

            if (NetworkServer.active)
            {
                role = NetworkClient.active ? "host" : "server";
            }
            else if (NetworkClient.isConnected)
            {
                role = "client";
            }
            string rtt = NetworkClient.isConnected ? $"{NetworkTime.rtt * 1000:0} ms" : "-";
            _text = $"FPS: {_frames / _elapsed:0} | v{Application.version} | {role} | RTT: {rtt}";
            if (MatchStateNet.Active != null)
            {
                _text += $" | {MatchStateNet.Active.Summary.Phase}";
            }

            var hero = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<HeroNet>() : null;
            if (hero != null)
            {
                var movement = hero.Movement;
                string color = movement.Error <= 0.1f ? "#55ee88" : "#ff5555";
                float corrections = Mathf.Max(0, movement.Corrections - _lastCorrections) / _elapsed;
                float sent = Mathf.Max(0, hero.MovementPackets - _lastPackets) / _elapsed;
                float received = Mathf.Max(0, hero.MovementSnapshots - _lastSnapshots) / _elapsed;
                string prediction = hero.PredictionEnabled ? "ON" : "OFF";

                if (NetworkServer.active)
                {
                    prediction = "HOST";
                }

                _text += $"\nPred {prediction} <color={color}>err {movement.Error:0.000}</color>"
                    + $" c/s {corrections:0} h {movement.HistorySize} ack {movement.AckSequence}/t{movement.LastServerTick}"
                    + $" send/snap {sent:0}/{received:0}";
                _lastCorrections = movement.Corrections;
                _lastPackets = hero.MovementPackets;
                _lastSnapshots = hero.MovementSnapshots;
            }
            Publish(_text);
            _elapsed = 0;
            _frames = 0;
        }

        private void OnDestroy()
        {
            Changed = null;
        }
    }
}
