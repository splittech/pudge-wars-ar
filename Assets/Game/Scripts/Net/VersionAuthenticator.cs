using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Mirror;
using UnityEngine;

namespace Game.Net
{
    // Keep the version-6 probe message IDs and shape so older builds can reject version 7 clearly.
    public struct VersionAuthRequest : NetworkMessage
    {
        public int ProtocolVersion;
    }

    public struct VersionAuthResponse : NetworkMessage
    {
        public int ProtocolVersion;
    }

    [DisallowMultipleComponent]
    public sealed class VersionAuthenticator : NetworkAuthenticator
    {
        private readonly HashSet<NetworkConnectionToClient> _pending = new();
        private SessionRoster _roster = new();
        private bool _requestSent;
        private string _reconnectToken;
        private string _reconnectAddress;
        private string _reconnectNickname;
        public void ClearReconnect()
        {
            _reconnectToken = null;
            _reconnectAddress = null;
            _reconnectNickname = null;
        }
        public string Nickname { get; set; } = "Player-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        public AuthResponse LastResponse { get; private set; }
        public event Action<string> Rejected;

        public void Configure(SessionRoster roster) => _roster = roster ?? throw new ArgumentNullException(nameof(roster));

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<AuthRequest>(OnRequest, false);
            NetworkServer.RegisterHandler<VersionAuthRequest>(OnVersionProbe, false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<AuthRequest>();
            NetworkServer.UnregisterHandler<VersionAuthRequest>();
            StopAllCoroutines();
            _pending.Clear();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient connection)
        {
            if (_pending.Add(connection))
                StartCoroutine(Expire(connection, 5f));
        }

        private void OnRequest(NetworkConnectionToClient connection, AuthRequest request)
        {
            if (!_pending.Remove(connection) || connection.isAuthenticated)
                return;
            AuthCode code = _roster.TryJoin(request, out SessionPlayer player);
            var response = new AuthResponse
            {
                ProtocolVersion = NetworkProtocol.ProtocolVersion, Code = code,
                SessionPlayerId = player?.Info.SessionPlayerId ?? 0,
                Nickname = player?.Info.Nickname ?? string.Empty,
                SessionEpoch = player?.Epoch ?? 0,
                ReconnectToken = player?.Token
            };
            if (code == AuthCode.Accepted)
            {
                connection.authenticationData = player;
                player.Connection = connection;
                connection.Send(response);
                ServerAccept(connection);
            }
            else
            {
                connection.Send(response);
                StartCoroutine(RejectLater(connection));
            }
        }

        private void OnVersionProbe(NetworkConnectionToClient connection, VersionAuthRequest request)
        {
            if (!_pending.Contains(connection) || connection.isAuthenticated)
                return;
            connection.Send(new VersionAuthResponse { ProtocolVersion = NetworkProtocol.ProtocolVersion });
            if (request.ProtocolVersion != NetworkProtocol.ProtocolVersion)
            {
                _pending.Remove(connection);
                StartCoroutine(RejectLater(connection));
            }
        }

        private IEnumerator Expire(NetworkConnectionToClient connection, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_pending.Remove(connection))
                ServerReject(connection);
        }

        private IEnumerator RejectLater(NetworkConnectionToClient connection)
        {
            yield return new WaitForSecondsRealtime(1f);
            ServerReject(connection);
        }

        public override void OnStartClient()
        {
            LastResponse = default;
            _requestSent = false;
            NetworkClient.RegisterHandler<AuthResponse>(OnResponse, false);
            NetworkClient.RegisterHandler<VersionAuthResponse>(OnVersionResponse, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<AuthResponse>();
            NetworkClient.UnregisterHandler<VersionAuthResponse>();
        }

        public override void OnClientAuthenticate() => NetworkClient.Send(new VersionAuthRequest
        {
            ProtocolVersion = NetworkProtocol.ProtocolVersion
        });

        private void OnVersionResponse(VersionAuthResponse response)
        {
            if (NetworkClient.connection == null || NetworkClient.connection.isAuthenticated || _requestSent)
                return;
            if (response.ProtocolVersion != NetworkProtocol.ProtocolVersion)
            {
                OnResponse(new AuthResponse { ProtocolVersion = response.ProtocolVersion, Code = AuthCode.IncompatibleVersion });
                return;
            }
            _requestSent = true;
            NetworkClient.Send(new AuthRequest
            {
                ProtocolVersion = NetworkProtocol.ProtocolVersion, Nickname = Nickname,
                ReconnectToken = NetworkManager.singleton.networkAddress == _reconnectAddress
                    && string.Equals(Nickname, _reconnectNickname, StringComparison.OrdinalIgnoreCase) ? _reconnectToken : null
            });
        }

        private void OnResponse(AuthResponse response)
        {
            if (NetworkClient.connection == null || NetworkClient.connection.isAuthenticated)
                return;
            var diagnosticResponse = response;
            diagnosticResponse.ReconnectToken = null;
            LastResponse = diagnosticResponse;
            if (response.Code == AuthCode.Accepted && response.ProtocolVersion == NetworkProtocol.ProtocolVersion)
            {
                _reconnectToken = response.ReconnectToken;
                _reconnectAddress = NetworkManager.singleton != null ? NetworkManager.singleton.networkAddress : null;
                _reconnectNickname = response.Nickname;
                ClientAccept();
            }
            else
            {
                if (response.Code == AuthCode.InvalidReconnectToken)
                    ClearReconnect();
                Rejected?.Invoke(DescribeRejection(response));
                if (NetworkClient.connection != null)
                    ClientReject();
            }
        }

        public static string DescribeRejection(AuthResponse response)
        {
            if (response.ProtocolVersion != NetworkProtocol.ProtocolVersion || response.Code == AuthCode.IncompatibleVersion)
                return $"Incompatible protocol: client {NetworkProtocol.ProtocolVersion}, host {response.ProtocolVersion}. Update both games.";
            return response.Code switch
            {
                AuthCode.InvalidNickname => "Nickname must contain 2-16 characters, without control characters.",
                AuthCode.NicknameTaken => "Nickname is already in use or reserved. Choose another name.",
                AuthCode.ServerFull => "Game is full (4 occupied or reserved slots).",
                AuthCode.InvalidReconnectToken => "Reconnect token is invalid or expired.",
                AuthCode.ReconnectInUse => "Previous connection is still active. Wait for its timeout, then retry.",
                _ => "Authentication rejected."
            };
        }
    }
}
