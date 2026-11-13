using System;
using System.IO;
using System.Text;
using Game.Core.Match;
using Mirror;
using UnityEngine;

namespace Game.Net
{
    public static class SessionSerialization
    {
        public static void WriteMovementBatch(this NetworkWriter writer, MovementBatch value)
        {
            Require(value.Count > 0 && value.Count <= 4);
            writer.WriteByte(value.Count);
            for (int i = 0; i < value.Count; i++) writer.WriteMovementFrame(value.Get(i));
        }

        public static MovementBatch ReadMovementBatch(this NetworkReader reader)
        {
            byte count = reader.ReadByte();
            Require(count > 0 && count <= 4);
            var batch = new MovementBatch();
            for (int i = 0; i < count; i++) batch.Add(reader.ReadMovementFrame());
            return batch;
        }
        private static readonly UTF8Encoding Utf8 = new(false, true);

        public static void WriteAuthRequest(this NetworkWriter writer, AuthRequest value)
        {
            writer.WriteInt(value.ProtocolVersion);
            WriteText(writer, value.Nickname ?? string.Empty, 192);
            WriteToken(writer, value.ReconnectToken);
        }

        public static AuthRequest ReadAuthRequest(this NetworkReader reader)
        {
            Require(reader.Remaining >= 4);
            return new AuthRequest
            {
                ProtocolVersion = reader.ReadInt(), Nickname = ReadText(reader, 192), ReconnectToken = ReadToken(reader)
            };
        }

        public static void WriteAuthResponse(this NetworkWriter writer, AuthResponse value)
        {
            Validate(value);
            writer.WriteInt(value.ProtocolVersion);
            writer.WriteByte((byte)value.Code);
            writer.WriteUInt(value.SessionPlayerId);
            WriteText(writer, value.Nickname ?? string.Empty, 64);
            writer.WriteUInt(value.SessionEpoch);
            WriteToken(writer, value.ReconnectToken);
        }

        public static AuthResponse ReadAuthResponse(this NetworkReader reader)
        {
            Require(reader.Remaining >= 9);
            var value = new AuthResponse
            {
                ProtocolVersion = reader.ReadInt(), Code = (AuthCode)reader.ReadByte(),
                SessionPlayerId = reader.ReadUInt(), Nickname = ReadText(reader, 64),
                SessionEpoch = ReadAuthEpoch(reader), ReconnectToken = ReadToken(reader)
            };
            Validate(value);
            return value;
        }

        public static void WritePlayerInfo(this NetworkWriter writer, PlayerInfo value)
        {
            Validate(value);
            writer.WriteUInt(value.SessionPlayerId);
            WriteText(writer, value.Nickname, 64);
            writer.WriteByte((byte)value.Team);
            writer.WriteBool(value.Ready);
            writer.WriteBool(value.Connected);
        }

        public static PlayerInfo ReadPlayerInfo(this NetworkReader reader)
        {
            var value = new PlayerInfo
            {
                SessionPlayerId = reader.ReadUInt(), Nickname = ReadText(reader, 64),
                Team = (Team)reader.ReadByte(), Ready = ReadFlag(reader), Connected = ReadFlag(reader)
            };
            Validate(value);
            return value;
        }

        public static void WriteMovementFrame(this NetworkWriter writer, MovementFrame value)
        {
            Validate(value);
            writer.WriteUInt(value.Sequence);
            writer.WriteUInt(value.ClientTick);
            writer.WriteBool(value.HasMoveTarget);
            if (value.HasMoveTarget) writer.WriteVector2(value.Target);
            writer.WriteUInt(value.SessionEpoch);
        }

        public static MovementFrame ReadMovementFrame(this NetworkReader reader)
        {
            var value = new MovementFrame
            {
                Sequence = reader.ReadUInt(), ClientTick = reader.ReadUInt(), HasMoveTarget = ReadFlag(reader)
            };
            if (value.HasMoveTarget) value.Target = reader.ReadVector2();
            value.SessionEpoch = reader.ReadUInt();
            Validate(value);
            return value;
        }

        public static void WriteMovementSnapshot(this NetworkWriter writer, MovementSnapshot value)
        {
            Validate(value);
            writer.WriteUInt(value.ServerTick);
            writer.WriteDouble(value.ServerTime);
            writer.WriteUInt(value.AckSequence);
            writer.WriteUInt(value.SessionEpoch);
            writer.WriteVector2(value.Pos);
            writer.WriteVector2(value.MoveTarget);
            writer.WriteFloat(value.MoveSpeed);
            writer.WriteBool(value.Alive);
            writer.WriteBool(value.IsPulled);
        }

        public static MovementSnapshot ReadMovementSnapshot(this NetworkReader reader)
        {
            var value = new MovementSnapshot
            {
                ServerTick = reader.ReadUInt(), ServerTime = reader.ReadDouble(),
                AckSequence = reader.ReadUInt(), SessionEpoch = reader.ReadUInt(),
                Pos = reader.ReadVector2(), MoveTarget = reader.ReadVector2(), MoveSpeed = reader.ReadFloat(),
                Alive = ReadFlag(reader), IsPulled = ReadFlag(reader)
            };
            Validate(value);
            return value;
        }

        internal static void Require(bool condition)
        {
            if (!condition) throw new InvalidDataException("Invalid network payload.");
        }

        internal static bool Finite(Vector2 value) => float.IsFinite(value.x) && float.IsFinite(value.y);
        internal static bool TimeValid(double value) => double.IsFinite(value) && value >= 0;

        private static bool ReadFlag(NetworkReader reader)
        {
            Require(reader.Remaining >= 1);
            byte flag = reader.ReadByte();
            Require(flag <= 1);
            return flag == 1;
        }

        private static void WriteText(NetworkWriter writer, string value, int maxBytes)
        {
            Require(value != null);
            int count = Utf8.GetByteCount(value);
            Require(count <= maxBytes);
            writer.WriteUShort((ushort)count);
            byte[] bytes = Utf8.GetBytes(value);
            writer.WriteBytes(bytes, 0, bytes.Length);
        }

        private static string ReadText(NetworkReader reader, int maxBytes)
        {
            Require(reader.Remaining >= 2);
            int count = reader.ReadUShort();
            Require(count <= maxBytes && reader.Remaining >= count);
            var bytes = reader.ReadBytesSegment(count);
            try { return Utf8.GetString(bytes.Array, bytes.Offset, bytes.Count); }
            catch (DecoderFallbackException) { throw new InvalidDataException("Invalid network UTF-8."); }
        }

        private static uint ReadAuthEpoch(NetworkReader reader)
        {
            Require(reader.Remaining >= 4);
            return reader.ReadUInt();
        }

        private static void WriteToken(NetworkWriter writer, string token)
        {
            writer.WriteBool(token != null);
            if (token != null) WriteText(writer, token, 32);
        }

        private static string ReadToken(NetworkReader reader) => ReadFlag(reader) ? ReadText(reader, 32) : null;

        private static void Validate(PlayerInfo value)
        {
            Require(value.SessionPlayerId != 0 && (value.Team == Team.A || value.Team == Team.B));
            Require(NicknamePolicy.TryNormalize(value.Nickname, out string name) && name == value.Nickname);
            Require(!value.Ready || value.Connected);
        }

        private static void Validate(AuthResponse value)
        {
            Require(value.Code <= AuthCode.ReconnectInUse);
            if (value.Code == AuthCode.Accepted)
            {
                Require(value.SessionPlayerId != 0 && value.SessionEpoch != 0);
                Require(NicknamePolicy.TryNormalize(value.Nickname, out string name) && name == value.Nickname);
            }
            else
                Require(value.SessionPlayerId == 0 && value.SessionEpoch == 0 && string.IsNullOrEmpty(value.Nickname)
                    && value.ReconnectToken == null);
        }

        private static void Validate(MovementFrame value) => Require(value.Sequence != 0
            && value.SessionEpoch != 0 && (!value.HasMoveTarget || Finite(value.Target)));

        private static void Validate(MovementSnapshot value) => Require(value.SessionEpoch != 0
            && TimeValid(value.ServerTime) && Finite(value.Pos) && Finite(value.MoveTarget)
            && float.IsFinite(value.MoveSpeed) && value.MoveSpeed >= 0);
    }
}
