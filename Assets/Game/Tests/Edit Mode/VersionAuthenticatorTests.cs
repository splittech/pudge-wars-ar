using System.Reflection;
using Game.Core;
using Game.Net;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class VersionAuthenticatorTests
    {
        [Test]
        public void Response_AcceptedReconnect_KeepsCredentialOutOfDiagnostics()
        {
            // Arrange.
            var owner = new GameObject("Reconnect credential test");
            var previous = NetworkClient.connection;
            try
            {
                var auth = owner.AddComponent<VersionAuthenticator>();
                typeof(NetworkClient).GetProperty("connection").SetValue(null, new NetworkConnectionToServer());
                const string sample = "0123456789abcdef0123456789abcdef";
                var handler = typeof(VersionAuthenticator).GetMethod("OnResponse", BindingFlags.Instance | BindingFlags.NonPublic);
                // Act.
                handler.Invoke(auth, new object[] { new AuthResponse
                {
                    ProtocolVersion = NetworkProtocol.ProtocolVersion, Code = AuthCode.Accepted,
                    SessionPlayerId = 1, SessionEpoch = 2, Nickname = "Alice", ReconnectToken = sample
                }});
                // Assert.
                Assert.That(auth.LastResponse.ReconnectToken, Is.Null);
                Assert.That(typeof(VersionAuthenticator).GetField("_reconnectToken",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(auth), Is.EqualTo(sample));
            }
            finally
            {
                typeof(NetworkClient).GetProperty("connection").SetValue(null, previous);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void VersionProbe_OlderServer_ReportsMismatchWithoutSpawning()
        {
            // Arrange.
            var owner = new GameObject("Legacy probe test");
            try
            {
                var auth = owner.AddComponent<VersionAuthenticator>();
                typeof(NetworkClient).GetProperty("connection").SetValue(null, new NetworkConnectionToServer());
                string rejection = null;
                auth.Rejected += reason =>
                {
                    rejection = reason;
                    typeof(NetworkClient).GetProperty("connection").SetValue(null, null);
                };
                var handler = typeof(VersionAuthenticator).GetMethod("OnVersionResponse", BindingFlags.Instance | BindingFlags.NonPublic);
                // Act.
                handler.Invoke(auth, new object[] { new VersionAuthResponse { ProtocolVersion = 6 } });
                // Assert.
                Assert.That(rejection, Does.Contain("client 9, host 6"));
                Assert.That(auth.LastResponse.Code, Is.EqualTo(AuthCode.IncompatibleVersion));
                Assert.That(auth.LastResponse.SessionPlayerId, Is.Zero);
            }
            finally
            {
                typeof(NetworkClient).GetProperty("connection").SetValue(null, null);
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(9, true)]
        [TestCase(5, false)]
        [TestCase(0, false)]
        [TestCase(int.MaxValue, false)]
        public void Response_AcceptsOnlyCurrentProtocol(int version, bool expected)
        {
            var owner = new GameObject("Authenticator test");
            try
            {
                var auth = owner.AddComponent<VersionAuthenticator>();
                typeof(NetworkClient).GetProperty("connection").SetValue(null, new NetworkConnectionToServer());
                int accepted = 0;
                string rejection = null;
                auth.OnClientAuthenticated.AddListener(() =>
                {
                    accepted++;
                    NetworkClient.connection.isAuthenticated = true;
                });
                auth.Rejected += reason =>
                {
                    rejection = reason;
                    typeof(NetworkClient).GetProperty("connection").SetValue(null, null);
                };
                var handler = typeof(VersionAuthenticator).GetMethod("OnResponse", BindingFlags.Instance | BindingFlags.NonPublic);
                handler.Invoke(auth, new object[] { new AuthResponse { ProtocolVersion = version, Code = expected ? AuthCode.Accepted : AuthCode.IncompatibleVersion, SessionPlayerId = expected ? 1u : 0u, Nickname = expected ? "Alice" : string.Empty, SessionEpoch = expected ? 1u : 0u } });
                Assert.That(accepted, Is.EqualTo(expected ? 1 : 0));
                if (expected)
                {
                    handler.Invoke(auth, new object[] { new AuthResponse { ProtocolVersion = version, Code = expected ? AuthCode.Accepted : AuthCode.IncompatibleVersion, SessionPlayerId = expected ? 1u : 0u, Nickname = expected ? "Alice" : string.Empty, SessionEpoch = expected ? 1u : 0u } });
                    Assert.That(accepted, Is.EqualTo(1));
                }
                else
                {
                    Assert.That(rejection, Does.Contain($"client {NetworkProtocol.ProtocolVersion}"));
                    Assert.That(rejection, Does.Contain($"host {version}"));
                }
            }
            finally
            {
                typeof(NetworkClient).GetProperty("connection").SetValue(null, null);
                Object.DestroyImmediate(owner);
            }
        }
    }
}


