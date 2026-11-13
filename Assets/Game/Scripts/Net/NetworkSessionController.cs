using System;
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer.Unity;
using Mirror;

namespace Game.Net
{
    public sealed class NetworkSessionController : IInitializable, IDisposable
    {
        public const int ConnectTimeoutMilliseconds = 5000;
        private readonly PudgeNetworkManager _manager;
        private readonly ConnectionAttemptState _attempt = new();
        private CancellationTokenSource _timeout;
        private bool _disposed;

        public string Message => _attempt.Message;
        public string LastAddress { get; private set; } = string.Empty;
        public string Nickname { get; private set; } = "Player-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        public bool HostDisconnected => _attempt.HostDisconnected;
        public bool IsSearching => _manager.Discovery != null && _manager.Discovery.IsSearching;
        public IReadOnlyList<LanServer> Servers => _manager.Discovery.Results.Servers;
        public bool CanStart => !_disposed && !HostDisconnected && !_attempt.IsPending && _manager.CanBeginSession;
        public bool CanCancel => !_disposed && (IsSearching || _attempt.IsPending || _manager.HasActiveSession);

        public NetworkSessionController(PudgeNetworkManager manager) => _manager = manager;

        public void Initialize()
        {
            _manager.ClientConnected += OnConnected;
            _manager.SessionEnded += OnEnded;
            _manager.ClientConnectionError += OnError;
        }

        public void CreateGame()
        {
            if (!CanStart)
                return;
            if (!PrepareNickname())
                return;
            _manager.Discovery.StopSearch();
            long id = _attempt.Begin(true);
            try
            {
                _manager.StartHostSession();
                if (!_manager.HasActiveSession)
                    _attempt.Fail(id, "Could not create game.");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                _attempt.Fail(id, "Could not create game.");
                _manager.StopSessionAfterFailure();
            }
        }

        public void Connect(string input)
        {
            if (!CanStart)
                return;
            if (!Ipv4Endpoint.TryValidate(input, out string address))
            {
                _attempt.ShowValidationError("Enter an IPv4 address, e.g. 192.168.1.10");
                return;
            }
            if (!PrepareNickname())
                return;
            long id = _attempt.Begin(false);
            _manager.Discovery.StopSearch();
            CancelTimeout();
            _timeout = new CancellationTokenSource();
            CancellationToken token = _timeout.Token;
            try
            {
                _manager.networkAddress = address;
                LastAddress = address;
                _manager.StartClientSession();
                if (!_manager.HasActiveSession)
                {
                    _attempt.Fail(id, "Could not start connection.");
                    CancelTimeout();
                    return;
                }
                WaitForConnectionAsync(id, token).Forget();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                _attempt.Fail(id, "Could not start connection.");
                CancelTimeout();
                _manager.StopSessionAfterFailure();
            }
        }

        public void Cancel()
        {
            if (!CanCancel)
                return;
            _attempt.End(true);
            _manager.Discovery.StopSearch();
            _attempt.ShowValidationError("Cancelled.");
            CancelTimeout();
            if (NetworkServer.active || (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated))
                _manager.StopSession();
            else
                _manager.StopSessionAfterFailure();
        }

        private async UniTaskVoid WaitForConnectionAsync(long id, CancellationToken ct)
        {
            double deadline = NetworkTime.time + ConnectTimeoutMilliseconds / 1000.0;
            try
            {
                await UniTask.WaitUntil(() => NetworkTime.time >= deadline, cancellationToken: ct);
                if (_attempt.Fail(id, "Connection timed out (5 s). Check the IP and Wi-Fi."))
                    _manager.StopSessionAfterFailure();
            }
            catch (OperationCanceledException)
            {
                // Success, cancellation, shutdown or another attempt owns the session now.
            }
        }

        private void OnConnected()
        {
            _attempt.Complete(_attempt.Id);
            CancelTimeout();
        }

        private void OnEnded(bool expected)
        {
            _attempt.End(expected);
            CancelTimeout();
        }

        private void OnError(string reason)
        {
            if (_attempt.IsPending)
                _attempt.Fail(_attempt.Id, reason);
            else
                _attempt.End(false);
            CancelTimeout();
            _manager.StopSessionAfterFailure();
        }

        public void SearchGames()
        {
            if (!CanStart)
                return;
            try
            {
                _manager.Discovery.Search();
                _attempt.ShowValidationError("Searching on Wi-Fi... You can also join by IP.");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"LAN discovery unavailable: {exception.Message}");
                _attempt.ShowValidationError("LAN search unavailable. Join by IP instead.");
            }
        }

        public void AcknowledgeHostDisconnected()
        {
            _attempt.AcknowledgeHostDisconnected();
        }

        public void SetNickname(string value)
        {
            if (CanStart)
                Nickname = value;
        }

        private bool PrepareNickname()
        {
            if (!NicknamePolicy.TryNormalize(Nickname, out string normalized))
            {
                _attempt.ShowValidationError("Nickname must contain 2-16 characters, without control characters.");
                return false;
            }
            Nickname = normalized;
            ((VersionAuthenticator)_manager.authenticator).Nickname = normalized;
            return true;
        }

        private void CancelTimeout()
        {
            CancellationTokenSource timeout = _timeout;
            _timeout = null;
            timeout?.Cancel();
            timeout?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _manager.ClientConnected -= OnConnected;
            _manager.SessionEnded -= OnEnded;
            _manager.ClientConnectionError -= OnError;
            _manager.Discovery?.StopSearch();
            CancelTimeout();
        }
    }
}


