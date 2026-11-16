using System;
using Game.Core;
using Game.Net;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class SessionIdentityTests
    {
        [Test]
        public void SetTeam_FullTeam_RejectsIncludingReservedSlots()
        {
            // Arrange.
            var roster = new SessionRoster();
            roster.TryJoin(Request("Alpha"), out var first);
            roster.TryJoin(Request("Bravo"), out var second);
            roster.TryJoin(Request("Charlie"), out var third);
            roster.MarkSpawned(first); roster.Disconnect(first, true);
            // Act.
            bool accepted = roster.SetTeam(second.Info.SessionPlayerId, Game.Core.Match.Team.A);
            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(second.Info.Team, Is.EqualTo(Game.Core.Match.Team.B));
        }

        [Test]
        public void SetTeam_ReadyPlayer_ClearsReadiness()
        {
            // Arrange.
            var roster = new SessionRoster();
            roster.TryJoin(Request("Alpha"), out var player); roster.MarkSpawned(player);
            roster.SetReady(player.Info.SessionPlayerId, true);
            // Act.
            bool accepted = roster.SetTeam(player.Info.SessionPlayerId, Game.Core.Match.Team.B);
            // Assert.
            Assert.That(accepted, Is.True);
            Assert.That(player.Info.Ready, Is.False);
            Assert.That(player.Info.Team, Is.EqualTo(Game.Core.Match.Team.B));
        }

        [Test]
        public void SetReady_DisconnectedPlayer_Rejects()
        {
            // Arrange.
            var roster = new SessionRoster();
            roster.TryJoin(Request("Alpha"), out var player); roster.MarkSpawned(player);
            roster.SetReady(player.Info.SessionPlayerId, true); roster.Disconnect(player, true);
            // Act.
            bool accepted = roster.SetReady(player.Info.SessionPlayerId, true);
            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(player.Info.Ready, Is.False);
        }

        [Test]
        public void TryJoin_AfterTeamSwitch_ChoosesTeamWithSpace()
        {
            // Arrange.
            var roster = new SessionRoster();
            roster.TryJoin(Request("Alpha"), out var first);
            roster.SetTeam(first.Info.SessionPlayerId, Game.Core.Match.Team.B);
            // Act.
            roster.TryJoin(Request("Bravo"), out var second);
            // Assert.
            Assert.That(second.Info.Team, Is.EqualTo(Game.Core.Match.Team.A));
        }

        private static AuthRequest Request(string name) => new()
        {
            ProtocolVersion = NetworkProtocol.ProtocolVersion, Nickname = name
        };

        [TestCase(" Ab ", "Ab")]
        [TestCase("\u0418\u0433\u0440\u043e\u043a", "\u0418\u0433\u0440\u043e\u043a")]
        [TestCase("1234567890123456", "1234567890123456")]
        public void TryNormalize_ValidNickname_TrimsWithoutChangingCase(string input, string expected)
        {
            // Act.
            bool valid = NicknamePolicy.TryNormalize(input, out string actual);
            // Assert.
            Assert.That(valid, Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" A ")]
        [TestCase("12345678901234567")]
        [TestCase("\tAlice")]
        [TestCase("Al\nIce")]
        [TestCase("Al\0Ice")]
        [TestCase("Al\u007fice")]
        public void TryNormalize_InvalidNickname_Rejects(string input)
        {
            // Act.
            bool valid = NicknamePolicy.TryNormalize(input, out _);
            // Assert.
            Assert.That(valid, Is.False);
        }

        [Test]
        public void TryNormalize_UnpairedSurrogate_Rejects()
        {
            // Arrange.
            string invalid = "Al" + (char)0xD800;
            // Act.
            bool valid = NicknamePolicy.TryNormalize(invalid, out _);
            // Assert.
            Assert.That(valid, Is.False);
        }
        [Test]
        public void TryJoin_ReservedNickname_IsUniqueIgnoringCaseUntilRemoved()
        {
            // Arrange.
            var roster = new SessionRoster();
            roster.TryJoin(Request(" Alice "), out var player);
            roster.MarkSpawned(player);
            roster.Disconnect(player, true);
            // Act.
            AuthCode duplicate = roster.TryJoin(Request("ALICE"), out _);
            // Assert.
            Assert.That(duplicate, Is.EqualTo(AuthCode.NicknameTaken));
            Assert.That(roster.Players[0].Nickname, Is.EqualTo("Alice"));
            Assert.That(roster.Players[0].Connected, Is.False);
            Assert.That(roster.Players[0].Ready, Is.False);
        }

        [Test]
        public void TryJoin_AllSlotsOccupiedOrReserved_RejectsBeforeSpawning()
        {
            // Arrange.
            var roster = new SessionRoster();
            for (int i = 0; i < 4; i++)
            {
                roster.TryJoin(Request("Player" + i), out var player);
                roster.MarkSpawned(player);
                roster.Disconnect(player, true);
            }
            // Act.
            AuthCode result = roster.TryJoin(Request("Fifth"), out _);
            // Assert.
            Assert.That(result, Is.EqualTo(AuthCode.ServerFull));
            Assert.That(roster.Players.Count, Is.EqualTo(4));
        }

        [Test]
        public void Disconnect_BeforeSpawn_ReleasesPendingNicknameAndSlotImmediately()
        {
            // Arrange.
            var roster = new SessionRoster();
            roster.TryJoin(Request("Alice"), out var original);
            // Act.
            roster.Disconnect(original, true);
            AuthCode result = roster.TryJoin(Request("alice"), out var replacement);
            // Assert.
            Assert.That(result, Is.EqualTo(AuthCode.Accepted));
            Assert.That(replacement.Slot, Is.Zero);
            Assert.That(replacement.Info.SessionPlayerId, Is.Not.EqualTo(original.Info.SessionPlayerId));
            Assert.That(roster.Contains(original), Is.False);
        }

        [Test]
        public void Remove_ExpiredOrVoluntaryExit_AllowsSameNameWithNewIdentity()
        {
            // Arrange.
            var roster = new SessionRoster();
            roster.TryJoin(Request("Alice"), out var original);
            roster.MarkSpawned(original);
            // Act.
            roster.Remove(original.Info.SessionPlayerId);
            AuthCode result = roster.TryJoin(Request("Alice"), out var replacement);
            // Assert.
            Assert.That(result, Is.EqualTo(AuthCode.Accepted));
            Assert.That(replacement.Info.SessionPlayerId, Is.Not.EqualTo(original.Info.SessionPlayerId));
        }

        [TestCase(0, "Alice", null, AuthCode.IncompatibleVersion)]
        [TestCase(NetworkProtocol.ProtocolVersion, "A", null, AuthCode.InvalidNickname)]
        [TestCase(NetworkProtocol.ProtocolVersion, "Alice", "0123456789abcdef0123456789abcdef", AuthCode.InvalidReconnectToken)]
        public void TryJoin_RejectedHandshake_DoesNotOccupySlot(int version, string name, string token, AuthCode expected)
        {
            // Arrange.
            var roster = new SessionRoster();
            var request = new AuthRequest { ProtocolVersion = version, Nickname = name, ReconnectToken = token };
            // Act.
            var result = roster.TryJoin(request, out var player);
            // Assert.
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(player, Is.Null);
            Assert.That(roster.Players, Is.Empty);
        }
    }
}

