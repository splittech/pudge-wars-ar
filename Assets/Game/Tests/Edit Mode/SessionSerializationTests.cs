using System;
using System.IO;
using Game.Core;
using Game.Core.Hook;
using Game.Core.Match;
using Game.Net;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class SessionSerializationTests
    {
        private static byte[] Payload(int kind)
        {
            using var writer = NetworkWriterPool.Get();
            switch (kind)
            {
                case 0: writer.WriteAuthRequest(new AuthRequest { ProtocolVersion = NetworkProtocol.ProtocolVersion, Nickname = "Alice", ReconnectToken = "0123456789abcdef0123456789abcdef" }); break;
                case 1: writer.WriteAuthResponse(new AuthResponse { ProtocolVersion = NetworkProtocol.ProtocolVersion, Code = AuthCode.Accepted, SessionPlayerId = uint.MaxValue, Nickname = "Alice", SessionEpoch = 1 }); break;
                case 2: writer.WritePlayerInfo(new PlayerInfo { SessionPlayerId = uint.MaxValue, Nickname = "Alice", Team = Team.B, Connected = true, Ready = true }); break;
                case 3: writer.WriteHookNetState(new HookNetState { Phase = HookPhase.Pulling, Origin = new Vector2(-4, 2), Dir = Vector2.right, StartTime = 100.25, TurnTime = 101, TurnLength = 8, RequestId = uint.MaxValue, SessionEpoch = 1 }); break;
                case 4: writer.WriteMovementFrame(new MovementFrame { Sequence = uint.MaxValue, ClientTick = uint.MaxValue, HasMoveTarget = true, Target = new Vector2(2, -3), SessionEpoch = 1 }); break;
                case 5: writer.WriteMovementSnapshot(new MovementSnapshot { ServerTick = uint.MaxValue, ServerTime = 100.125, AckSequence = uint.MaxValue, SessionEpoch = 1, Pos = new Vector2(2, -3), MoveTarget = new Vector2(4, -5), MoveSpeed = 6, Alive = true }); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            return writer.ToArray();
        }

        private static void Read(int kind, byte[] bytes)
        {
            using var reader = NetworkReaderPool.Get(bytes);
            switch (kind)
            {
                case 0: reader.ReadAuthRequest(); break;
                case 1: reader.ReadAuthResponse(); break;
                case 2: reader.ReadPlayerInfo(); break;
                case 3: reader.ReadHookNetState(); break;
                case 4: reader.ReadMovementFrame(); break;
                case 5: reader.ReadMovementSnapshot(); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            Assert.That(reader.Remaining, Is.Zero);
        }

        [TestCase(0, 46)]
        [TestCase(1, 21)]
        [TestCase(2, 14)]
        [TestCase(3, 49)]
        [TestCase(4, 21)]
        [TestCase(5, 42)]
        public void Serialize_ValidPayload_HasDocumentedSizeAndConsumesEntirePacket(int kind, int expectedBytes)
        {
            // Act.
            byte[] bytes = Payload(kind);
            Read(kind, bytes);
            // Assert.
            Assert.That(bytes.Length, Is.EqualTo(expectedBytes));
        }

        [Test]
        public void ReadAuthRequest_OptionalTokenAndWhitespaceNickname_RoundTrips()
        {
            // Arrange.
            using var writer = NetworkWriterPool.Get();
            writer.WriteAuthRequest(new AuthRequest { ProtocolVersion = int.MaxValue, Nickname = " Alice " });
            using var reader = NetworkReaderPool.Get(writer.ToArray());
            // Act.
            var value = reader.ReadAuthRequest();
            // Assert.
            Assert.That(value.ProtocolVersion, Is.EqualTo(int.MaxValue));
            Assert.That(value.Nickname, Is.EqualTo(" Alice "));
            Assert.That(value.ReconnectToken, Is.Null);
        }

        [TestCase(AuthCode.IncompatibleVersion)]
        [TestCase(AuthCode.InvalidNickname)]
        [TestCase(AuthCode.NicknameTaken)]
        [TestCase(AuthCode.ServerFull)]
        [TestCase(AuthCode.InvalidReconnectToken)]
        [TestCase(AuthCode.ReconnectInUse)]
        public void ReadAuthResponse_FailureCode_RoundTripsWithoutIdentity(AuthCode code)
        {
            // Arrange.
            using var writer = NetworkWriterPool.Get();
            writer.WriteAuthResponse(new AuthResponse { ProtocolVersion = 7, Code = code });
            using var reader = NetworkReaderPool.Get(writer.ToArray());
            // Act.
            var value = reader.ReadAuthResponse();
            // Assert.
            Assert.That(value.Code, Is.EqualTo(code));
            Assert.That(value.SessionPlayerId, Is.Zero);
            Assert.That(value.Nickname, Is.Empty);
            Assert.That(value.ReconnectToken, Is.Null);
        }

        [TestCase("Ab")]
        [TestCase("\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f\u042f")]
        [TestCase("1234567890123456")]
        public void ReadPlayerInfo_NicknameAtLimits_RoundTrips(string name)
        {
            // Arrange.
            using var writer = NetworkWriterPool.Get();
            writer.WritePlayerInfo(new PlayerInfo { SessionPlayerId = uint.MaxValue, Nickname = name, Team = Team.B, Connected = true });
            using var reader = NetworkReaderPool.Get(writer.ToArray());
            // Act.
            var value = reader.ReadPlayerInfo();
            // Assert.
            Assert.That(value.SessionPlayerId, Is.EqualTo(uint.MaxValue));
            Assert.That(value.Nickname, Is.EqualTo(name));
            Assert.That(value.Team, Is.EqualTo(Team.B));
            Assert.That(value.Connected, Is.True);
            Assert.That(value.Ready, Is.False);
        }

        [Test]
        public void ReadMovementFrame_OptionalTargetAbsent_UsesThirteenBytes()
        {
            // Arrange.
            using var writer = NetworkWriterPool.Get();
            writer.WriteMovementFrame(new MovementFrame { Sequence = 9, ClientTick = 8, SessionEpoch = 7 });
            using var reader = NetworkReaderPool.Get(writer.ToArray());
            // Act.
            var value = reader.ReadMovementFrame();
            // Assert.
            Assert.That(writer.Position, Is.EqualTo(13));
            Assert.That(value.Sequence, Is.EqualTo(9));
            Assert.That(value.ClientTick, Is.EqualTo(8));
            Assert.That(value.SessionEpoch, Is.EqualTo(7));
            Assert.That(value.HasMoveTarget, Is.False);
            Assert.That(value.Target, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ReadMovementSnapshot_FullState_RoundTrips()
        {
            // Arrange.
            using var reader = NetworkReaderPool.Get(Payload(5));
            // Act.
            var value = reader.ReadMovementSnapshot();
            // Assert.
            Assert.That(value.ServerTick, Is.EqualTo(uint.MaxValue));
            Assert.That(value.AckSequence, Is.EqualTo(uint.MaxValue));
            Assert.That(value.SessionEpoch, Is.EqualTo(1));
            Assert.That(value.ServerTime, Is.EqualTo(100.125));
            Assert.That(value.Pos, Is.EqualTo(new Vector2(2, -3)));
            Assert.That(value.MoveTarget, Is.EqualTo(new Vector2(4, -5)));
            Assert.That(value.MoveSpeed, Is.EqualTo(6));
            Assert.That(value.Alive, Is.True);
            Assert.That(value.IsPulled, Is.False);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void Read_TruncatedAtEveryByte_RejectsIncompletePayload(int kind)
        {
            // Arrange.
            byte[] complete = Payload(kind);
            // Act and Assert.
            for (int length = 0; length < complete.Length; length++)
            {
                var truncated = new byte[length];
                Array.Copy(complete, truncated, length);
                Assert.That(() => Read(kind, truncated), Throws.InstanceOf<Exception>(), $"kind={kind}, length={length}");
            }
        }

        [TestCase(1, 4)] // AuthCode.
        [TestCase(2, 11)] // Team.
        [TestCase(2, 12)] // Ready bool.
        [TestCase(3, 0)] // HookPhase.
        [TestCase(4, 8)] // Optional-target bool.
        [TestCase(5, 40)] // Alive bool.
        public void Read_InvalidEnumOrBoolean_Rejects(int kind, int offset)
        {
            // Arrange.
            byte[] bytes = Payload(kind);
            bytes[offset] = 255;
            // Act and Assert.
            Assert.That(() => Read(kind, bytes), Throws.TypeOf<InvalidDataException>());
        }

        [TestCase(3, 1)] [TestCase(3, 9)] [TestCase(3, 33)]
        [TestCase(4, 9)] [TestCase(5, 20)] [TestCase(5, 28)] [TestCase(5, 36)]
        public void Read_NonFiniteFloat_Rejects(int kind, int offset)
        {
            // Arrange.
            byte[] bytes = Payload(kind);
            BitConverter.GetBytes(float.NaN).CopyTo(bytes, offset);
            // Act and Assert.
            Assert.That(() => Read(kind, bytes), Throws.TypeOf<InvalidDataException>());
        }

        [TestCase(3, 17)] [TestCase(3, 25)] [TestCase(5, 4)]
        public void Read_NonFiniteTime_Rejects(int kind, int offset)
        {
            // Arrange.
            byte[] bytes = Payload(kind);
            BitConverter.GetBytes(double.PositiveInfinity).CopyTo(bytes, offset);
            // Act and Assert.
            Assert.That(() => Read(kind, bytes), Throws.TypeOf<InvalidDataException>());
        }

        [TestCase(0, 4)] [TestCase(1, 9)] [TestCase(2, 4)]
        public void Read_ExcessiveStringLength_RejectsBeforeDecoding(int kind, int offset)
        {
            // Arrange.
            byte[] bytes = Payload(kind);
            bytes[offset] = 255;
            bytes[offset + 1] = 255;
            // Act and Assert.
            Assert.That(() => Read(kind, bytes), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void ReadAuthRequest_InvalidUtf8_RejectsWithoutLoggingPayload()
        {
            // Arrange.
            byte[] bytes = Payload(0);
            bytes[6] = 255;
            // Act and Assert.
            Assert.That(() => Read(0, bytes), Throws.TypeOf<InvalidDataException>());
        }

        [TestCase("")]
        [TestCase("A")]
        [TestCase("12345678901234567")]
        [TestCase(" Alice ")]
        public void WritePlayerInfo_InvalidCanonicalNickname_Rejects(string name)
        {
            // Arrange.
            using var writer = NetworkWriterPool.Get();
            // Act and Assert.
            Assert.That(() => writer.WritePlayerInfo(new PlayerInfo { SessionPlayerId = 1, Nickname = name }), Throws.TypeOf<InvalidDataException>());
        }
    }
}
