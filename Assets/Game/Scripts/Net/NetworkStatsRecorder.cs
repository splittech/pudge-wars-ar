using System;
using System.Globalization;
using System.IO;
using System.Text;
using Alchemy.Inspector;
using Mirror;
using UnityEngine;

namespace Game.Net
{
    [DisallowMultipleComponent]
    public sealed class NetworkStatsRecorder : MonoBehaviour
    {
        [SerializeField] private bool _autoCapture = true;
        [SerializeField] private string _scenario = "baseline";
        [SerializeField, Min(0)] private float _warmupSeconds = 10;
        [SerializeField, Min(1)] private float _captureSeconds = 60;
        [SerializeField, Range(1, 10)] private int _repeats = 3;
        [SerializeField] private float _configuredRttMs;
        [SerializeField] private float _configuredLossPercent;
        private readonly NetworkTrafficWindow _traffic = new();
        private PudgeNetworkManager _manager;
        private Transport _transport;
        private StreamWriter _writer;
        private StreamWriter _latencyWriter;
        private HeroNet _latencyHero;
        private double _actualWarmup;
        private bool _readyLastFrame, _running, _measuring;
        private double _warmupStart, _measurementStart, _lastSample;
        private int _repeat, _lastFrames, _lastPackets, _lastSnapshots, _lastCorrections, _lastLatencySamples;
        private uint _lastTick;
        private string _runId, _commit, _runScenario;
        private HeroNet _previousHero;
        public bool IsRecording => _running;
        public string LastFile { get; private set; }
        public string OutputDirectory => Path.Combine(Application.persistentDataPath, "NetworkReports");
        public const string Header = "run_id,repeat,scenario,role,platform,protocol,commit,build_guid,app_version,utc,network_time,elapsed_s,interval_s,warmup_s,capture_s,repeat_goal,players,objects,prediction,configured_rtt_ms,configured_loss_percent,rtt_ms,observed_loss_percent,mirror_bytes_in_s,mirror_bytes_out_s,messages_in_s,messages_out_s,transport_payload_in_s,transport_payload_out_s,transport_batches_in_s,transport_batches_out_s,udp_packets_in_s,udp_packets_out_s,server_tick_hz,observed_tick_hz,prediction_tick_hz,input_packets_hz,snapshots_hz,mirror_send_rate,input_send_rate,snapshot_send_rate,error_units,corrections_s,history_size,ack_sequence,server_tick,input_to_view_ms,input_samples,source";

        private void Awake()
        {
            _manager = GetComponent<PudgeNetworkManager>();
            if (!Debug.isDebugBuild) enabled = false;
        }
        private void OnEnable()
        {
            NetworkDiagnostics.InMessageEvent += OnMessageIn;
            NetworkDiagnostics.OutMessageEvent += OnMessageOut;
        }
        private void OnDisable()
        {
            StopCapture();
            UnbindTransport();
            NetworkDiagnostics.InMessageEvent -= OnMessageIn;
            NetworkDiagnostics.OutMessageEvent -= OnMessageOut;
            if (_latencyHero != null) _latencyHero.MovementPresented -= OnMovementPresented;
            _latencyHero = null;
        }
        private void OnMessageIn(NetworkDiagnostics.MessageInfo value) => _traffic.Message(true, value.bytes, value.count);
        private void OnMessageOut(NetworkDiagnostics.MessageInfo value) => _traffic.Message(false, value.bytes, value.count);
        private void ClientIn(ArraySegment<byte> data, int channel) => _traffic.Payload(true, data.Count);
        private void ClientOut(ArraySegment<byte> data, int channel) => _traffic.Payload(false, data.Count);
        private void ServerIn(int id, ArraySegment<byte> data, int channel) => _traffic.Payload(true, data.Count);
        private void ServerOut(int id, ArraySegment<byte> data, int channel) => _traffic.Payload(false, data.Count);
        private void BindTransport()
        {
            UnbindTransport();
            _transport = PayloadTransport(_manager.transport);
            if (_transport == null) return;
            _transport.OnClientDataReceived += ClientIn; _transport.OnClientDataSent += ClientOut;
            _transport.OnServerDataReceived += ServerIn; _transport.OnServerDataSent += ServerOut;
        }
        public static Transport PayloadTransport(Transport transport)
        {
            for (int i = 0; i < 8 && transport is LatencySimulation simulation; i++)
                transport = simulation.wrap;
            return transport is kcp2k.KcpTransport ? transport : null;
        }
        private void UnbindTransport()
        {
            if (_transport == null) return;
            _transport.OnClientDataReceived -= ClientIn; _transport.OnClientDataSent -= ClientOut;
            _transport.OnServerDataReceived -= ServerIn; _transport.OnServerDataSent -= ServerOut;
            _transport = null;
        }
        [Button]
        public void StartCapture()
        {
            if (_manager == null || (!NetworkServer.active && !NetworkClient.ready)) return;
            StopCapture(); BindTransport();
            _commit = NetworkBuildInfo.Commit(); _runScenario = _scenario;
            _runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _repeat = 1; _running = true; _measuring = false;
            _warmupStart = Time.realtimeSinceStartupAsDouble;
            _traffic.Clear();
        }
        [Button]
        public void StopCapture()
        {
            _running = false; _measuring = false;
            var writer = _writer; _writer = null;
            var latencyWriter = _latencyWriter; _latencyWriter = null;
            foreach (var file in new[] { writer, latencyWriter })
                try { file?.Dispose(); }
                catch (IOException exception) { Debug.LogWarning("Network CSV close failed: " + exception.Message); }
        }
        private HeroNet LocalHero() => NetworkClient.localPlayer == null ? null : NetworkClient.localPlayer.GetComponent<HeroNet>();
        private void Baseline(HeroNet hero)
        {
            _previousHero = hero;
            _lastFrames = hero != null ? hero.MovementFramesProduced : 0;
            _lastPackets = hero != null ? hero.MovementPackets : 0;
            _lastSnapshots = hero != null ? hero.MovementSnapshots : 0;
            _lastCorrections = hero != null ? hero.Movement.Corrections : 0;
            _lastLatencySamples = hero != null ? hero.InputLatencySamples : 0;
            _lastTick = hero != null ? hero.ObservedServerTick : 0;
        }
        private void Update()
        {
            var localHero = LocalHero();
            if (localHero != _latencyHero)
            {
                if (_latencyHero != null) _latencyHero.MovementPresented -= OnMovementPresented;
                _latencyHero = localHero;
                if (_latencyHero != null) _latencyHero.MovementPresented += OnMovementPresented;
            }
            bool ready = NetworkServer.active || NetworkClient.ready;
            if (ready && !_readyLastFrame)
            {
                BindTransport();
                if (_autoCapture) StartCapture();
            }
            if (!ready && _readyLastFrame) { StopCapture(); UnbindTransport(); }
            _readyLastFrame = ready;
            if (!_running) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (!_measuring)
                {
                    if (now - _warmupStart < Math.Max(0, _warmupSeconds)) return;
                    Directory.CreateDirectory(OutputDirectory);
                    LastFile = Path.Combine(OutputDirectory, _runId + "_repeat" + _repeat + ".csv");
                    _writer = new StreamWriter(LastFile, false, new UTF8Encoding(false));
                    _writer.WriteLine(Header); _writer.Flush();
                    _latencyWriter = new StreamWriter(Path.ChangeExtension(LastFile, null) + "_input.csv", false, new UTF8Encoding(false));
                    _latencyWriter.WriteLine("run_id,repeat,network_time,input_to_view_ms");
                    _latencyWriter.Flush();
                    _actualWarmup = now - _warmupStart;
                    _measurementStart = _lastSample = now; _measuring = true;
                    _traffic.Clear(); Baseline(LocalHero());
                    Debug.Log("Network CSV: " + LastFile);
                    return;
                }
                if (now - _lastSample < 1 && now - _measurementStart < Math.Max(1, _captureSeconds)) return;
                WriteSample(now);
                if (now - _measurementStart < Math.Max(1, _captureSeconds)) return;
                _writer.Dispose(); _writer = null;
                _latencyWriter.Dispose(); _latencyWriter = null;
                if (++_repeat > Mathf.Clamp(_repeats, 1, 10)) { StopCapture(); return; }
                _warmupStart = now; _measuring = false; _traffic.Clear();
            }
            catch (IOException exception) { Debug.LogWarning("Network CSV failed: " + exception.Message); StopCapture(); }
            catch (UnauthorizedAccessException exception) { Debug.LogWarning("Network CSV failed: " + exception.Message); StopCapture(); }
        }
        private void OnMovementPresented(double milliseconds)
        {
            if (!_measuring || _latencyWriter == null) return;
            try
            {
                var row = new StringBuilder();
                Add(row, _runId); Add(row, _repeat); Add(row, NetworkTime.time); Add(row, milliseconds);
                _latencyWriter.WriteLine(row.ToString());
            }
            catch (IOException exception) { Debug.LogWarning("Network latency CSV failed: " + exception.Message); StopCapture(); }
        }
        private void WriteSample(double now)
        {
            double seconds = now - _lastSample;
            if (seconds <= 0) return;
            var traffic = _traffic.Take(seconds);
            var hero = LocalHero();
            if (hero != _previousHero) Baseline(hero);
            int latencySamples = hero != null ? Math.Max(0, hero.InputLatencySamples - _lastLatencySamples) : 0;
            var row = new StringBuilder(640);
            Add(row, _runId); Add(row, _repeat); Add(row, _runScenario);
            Add(row, NetworkServer.active ? NetworkClient.active ? "host" : "server" : "client");
            Add(row, Application.platform.ToString());
            Add(row, Game.Core.NetworkProtocol.ProtocolVersion); Add(row, _commit); Add(row, Application.buildGUID);
            Add(row, Application.version); Add(row, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            Add(row, NetworkTime.time); Add(row, now - _measurementStart); Add(row, seconds);
            Add(row, _actualWarmup); Add(row, Math.Max(1, _captureSeconds)); Add(row, Mathf.Clamp(_repeats, 1, 10));
            int players = NetworkServer.active ? _manager.Roster.Players.Count : 0;
            if (!NetworkServer.active)
                foreach (var identity in NetworkClient.spawned.Values)
                    if (identity != null && identity.GetComponent<HeroNet>() != null) players++;
            Add(row, players); Add(row, NetworkServer.active ? NetworkServer.spawned.Count : NetworkClient.spawned.Count);
            Add(row, hero != null ? NetworkServer.active ? "host" : hero.PredictionEnabled ? "on" : "off" : "N/A");
            Add(row, _configuredRttMs); Add(row, _configuredLossPercent);
            Add(row, NetworkClient.isConnected ? NetworkTime.rtt * 1000 : double.NaN); Add(row, "N/A");
            Add(row, traffic.MessageBytesIn); Add(row, traffic.MessageBytesOut); Add(row, traffic.MessagesIn); Add(row, traffic.MessagesOut);
            Add(row, _transport != null ? traffic.PayloadBytesIn : double.NaN);
            Add(row, _transport != null ? traffic.PayloadBytesOut : double.NaN);
            Add(row, _transport != null ? traffic.BatchesIn : double.NaN);
            Add(row, _transport != null ? traffic.BatchesOut : double.NaN);
            Add(row, "N/A"); Add(row, "N/A"); Add(row, 30);
            Add(row, hero != null ? Math.Max(0, (long)hero.ObservedServerTick - _lastTick) / seconds : double.NaN);
            Add(row, hero != null ? Math.Max(0, hero.MovementFramesProduced - _lastFrames) / seconds : double.NaN);
            Add(row, hero != null ? Math.Max(0, hero.MovementPackets - _lastPackets) / seconds : double.NaN);
            Add(row, hero != null ? Math.Max(0, hero.MovementSnapshots - _lastSnapshots) / seconds : double.NaN);
            Add(row, _manager.sendRate); Add(row, hero != null ? hero.MovementSendRate.ToString(CultureInfo.InvariantCulture) : "N/A");
            Add(row, hero != null ? hero.MovementSnapshotRate.ToString(CultureInfo.InvariantCulture) : "N/A"); Add(row, hero != null ? hero.Movement.Error : double.NaN);
            Add(row, hero != null ? Math.Max(0, hero.Movement.Corrections - _lastCorrections) / seconds : double.NaN);
            Add(row, hero != null ? hero.Movement.HistorySize.ToString(CultureInfo.InvariantCulture) : "N/A");
            Add(row, hero != null ? hero.Movement.AckSequence.ToString(CultureInfo.InvariantCulture) : "N/A");
            Add(row, hero != null ? hero.Movement.LastServerTick.ToString(CultureInfo.InvariantCulture) : "N/A");
            Add(row, latencySamples > 0 ? hero.LastInputToViewMs : double.NaN); Add(row, latencySamples);
            Add(row, "Mirror.NetworkDiagnostics; " + (_transport != null ? _transport.GetType().FullName + " payload callbacks" : "transport=N/A"));
            _writer.WriteLine(row.ToString()); _writer.Flush();
            _latencyWriter?.Flush();
            Baseline(hero); _lastSample = now;
        }
        private static void Add(StringBuilder row, object value)
        {
            if (row.Length > 0) row.Append(',');
            string text = value is double number ? double.IsFinite(number) ? number.ToString("0.######", CultureInfo.InvariantCulture) : "N/A"
                : value is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "N/A";
            row.Append('"').Append(text.Replace("\"", "\"\"")).Append('"');
        }
    }
}
