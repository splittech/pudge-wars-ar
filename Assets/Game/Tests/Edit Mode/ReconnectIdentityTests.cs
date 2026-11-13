using System.Reflection;
using Game.Core;
using Game.Net;
using NUnit.Framework;
using Mirror;

namespace Game.Tests.EditMode
{
    public sealed class ReconnectIdentityTests
    {
        private static string Token(SessionPlayer player) => (string)typeof(SessionPlayer)
            .GetProperty("Token", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
        private static AuthRequest Request(string token = null) => new()
        {
            ProtocolVersion = NetworkProtocol.ProtocolVersion, Nickname = "Alice", ReconnectToken = token
        };

        [Test]
        public void Join_ValidReservation_RotatesTokenAndEpochWithoutNewIdentity()
        {
            var roster = new SessionRoster();
            roster.TryJoin(Request(), out var original);
            string token = Token(original);
            roster.MarkSpawned(original);
            roster.Disconnect(original, true);
            var result = roster.TryJoin(Request(token), out var restored);
            Assert.That(result, Is.EqualTo(AuthCode.Accepted));
            Assert.That(restored, Is.SameAs(original));
            Assert.That(restored.Epoch, Is.EqualTo(2));
            Assert.That(Token(restored).Length, Is.EqualTo(32));
            Assert.That(Token(restored), Is.Not.EqualTo(token));
            Assert.That(roster.Players.Count, Is.EqualTo(1));
            roster.CompleteRestore(restored);
            Assert.That(roster.TryJoin(Request(token), out _), Is.EqualTo(AuthCode.InvalidReconnectToken));
        }

        [Test]
        public void Join_HandoffInterruptedBeforeOwnership_AllowsRetryThenInvalidatesPreviousToken()
        {
            // Arrange.
            var roster = new SessionRoster();
            roster.TryJoin(Request(), out var player);
            string original = Token(player);
            roster.MarkSpawned(player);
            roster.Disconnect(player, true);
            roster.TryJoin(Request(original), out var pending);
            // Act: the response or connection is lost before AddPlayerForConnection.
            roster.Disconnect(pending, true);
            var retry = roster.TryJoin(Request(original), out var restored);
            roster.CompleteRestore(restored);
            roster.Disconnect(restored, true);
            // Assert.
            Assert.That(retry, Is.EqualTo(AuthCode.Accepted));
            Assert.That(restored, Is.SameAs(player));
            Assert.That(roster.TryJoin(Request(original), out _), Is.EqualTo(AuthCode.InvalidReconnectToken));
            Assert.That(roster.TryJoin(Request(Token(restored)), out _), Is.EqualTo(AuthCode.Accepted));
        }

        [Test]
        public void Join_ConnectedOwnerOrExpiredDeadline_RejectsToken()
        {
            var roster = new SessionRoster();
            roster.TryJoin(Request(), out var player);
            string token = Token(player);
            roster.MarkSpawned(player);
            Assert.That(roster.TryJoin(Request(token), out _), Is.EqualTo(AuthCode.ReconnectInUse));
            roster.Disconnect(player, true);
            typeof(SessionPlayer).GetProperty("Deadline").SetValue(player, NetworkTime.time - 1);
            Assert.That(roster.TryJoin(Request(token), out _), Is.EqualTo(AuthCode.InvalidReconnectToken));
        }

        [Test]
        public void Join_VoluntaryExitOrClearedSession_InvalidatesToken()
        {
            var roster = new SessionRoster();
            roster.TryJoin(Request(), out var player);
            string token = Token(player);
            roster.MarkSpawned(player);
            roster.Disconnect(player, false);
            Assert.That(roster.TryJoin(Request(token), out _), Is.EqualTo(AuthCode.InvalidReconnectToken));
            roster.Clear();
            Assert.That(roster.TryJoin(Request(token), out _), Is.EqualTo(AuthCode.InvalidReconnectToken));
        }
    }
}

