using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using Game.Core;
using Game.Core.Match;
using Mirror;

namespace Game.Net
{
    public enum AuthCode : byte
    {
        Accepted, IncompatibleVersion, InvalidNickname, NicknameTaken, ServerFull, InvalidReconnectToken, ReconnectInUse
    }

    public struct AuthRequest : NetworkMessage
    {
        public int ProtocolVersion;
        public string Nickname;
        public string ReconnectToken;
    }

    public struct AuthResponse : NetworkMessage
    {
        public int ProtocolVersion;
        public AuthCode Code;
        public uint SessionPlayerId;
        public string Nickname;
        public uint SessionEpoch;
        public string ReconnectToken;
    }

    public struct PlayerInfo
    {
        public uint SessionPlayerId;
        public string Nickname;
        public Team Team;
        public bool Ready;
        public bool Connected;
    }

    public static class NicknamePolicy
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static bool TryNormalize(string input, out string nickname)
        {
            nickname = string.Empty;
            if (input == null || input.Length > 64)
            {
                return false;
            }

            foreach (char character in input)
            {
                if (char.IsControl(character))
                {
                    return false;
                }
            }

            string trimmed = input.Trim();
            if (trimmed.Length < 2 || trimmed.Length > 16)
            {
                return false;
            }

            try
            {
                StrictUtf8.GetByteCount(trimmed);
            }
            catch (EncoderFallbackException) { return false; }
            nickname = trimmed;
            return true;
        }
    }

    public sealed class SessionPlayer
    {
        public PlayerInfo Info
        {
            get; internal set;
        }
        public int Slot
        {
            get;
        }
        public bool HasHero
        {
            get; internal set;
        }
        internal string Token
        {
            get; set;
        }
        internal string PreviousToken
        {
            get; set;
        }
        internal NetworkConnectionToClient Connection
        {
            get; set;
        }
        public uint Epoch { get; internal set; } = 1;
        public double Deadline
        {
            get; internal set;
        }

        internal SessionPlayer(uint id, string nickname, int slot)
        {
            Slot = slot;
            Info = new PlayerInfo
            {
                SessionPlayerId = id,
                Nickname = nickname,
                Team = slot % 2 == 0 ? Team.A : Team.B,
                Connected = true
            };
        }
    }

    public sealed class SessionRoster
    {
        private readonly SessionPlayer[] _slots = new SessionPlayer[4];
        private readonly List<PlayerInfo> _players = new(4);
        private uint _nextId = 1;
        public IReadOnlyList<PlayerInfo> Players => _players;
        public event Action Changed;

        public bool SetTeam(uint id, Team team)
        {
            if (team != Team.A && team != Team.B)
            {
                return false;
            }

            SessionPlayer player = Array.Find(_slots, item => item != null && item.Info.SessionPlayerId == id);
            if (player == null || !player.Info.Connected)
            {
                return false;
            }

            int count = 0;
            foreach (var item in _slots)
            {
                if (item != null && item.Info.Team == team)
                {
                    count++;
                }
            }

            if (player.Info.Team != team && count >= 2)
            {
                return false;
            }

            var info = player.Info;
            info.Team = team;
            info.Ready = false;
            player.Info = info;
            Publish();
            return true;
        }

        public bool SetReady(uint id, bool ready)
        {
            SessionPlayer player = Array.Find(_slots, item => item != null && item.Info.SessionPlayerId == id);
            if (player == null || !player.Info.Connected || !player.HasHero)
            {
                return false;
            }

            var info = player.Info;
            info.Ready = ready;
            player.Info = info;
            Publish();
            return true;
        }

        public void ClearReady()
        {
            foreach (var player in _slots)
            {
                if (player == null)
                {
                    continue;
                }

                var info = player.Info;
                info.Ready = false;
                player.Info = info;
            }
            Publish();
        }

        public uint AdvanceEpoch(uint id)
        {
            SessionPlayer player = Array.Find(_slots, item => item != null && item.Info.SessionPlayerId == id);
            if (player == null || player.Epoch == uint.MaxValue)
            {
                return 0;
            }

            return ++player.Epoch;
        }

        public AuthCode TryJoin(AuthRequest request, out SessionPlayer player)
        {
            player = null;
            if (request.ProtocolVersion != NetworkProtocol.ProtocolVersion)
            {
                return AuthCode.IncompatibleVersion;
            }

            if (!NicknamePolicy.TryNormalize(request.Nickname, out string nickname))
            {
                return AuthCode.InvalidNickname;
            }

            if (request.ReconnectToken != null)
            {
                foreach (var reserved in _slots)
                {
                    if (reserved == null || (reserved.Token != request.ReconnectToken && reserved.PreviousToken != request.ReconnectToken)
                        || !reserved.HasHero || reserved.Epoch == uint.MaxValue
                        || !string.Equals(reserved.Info.Nickname, nickname, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (reserved.Info.Connected)
                    {
                        return AuthCode.ReconnectInUse;
                    }

                    if (NetworkTime.time >= reserved.Deadline)
                    {
                        return AuthCode.InvalidReconnectToken;
                    }

                    player = reserved;
                    var info = player.Info;
                    info.Connected = true;
                    info.Ready = false;
                    player.Info = info;
                    player.Epoch++;
                    player.PreviousToken = request.ReconnectToken;
                    player.Token = NewToken();
                    Publish();
                    return AuthCode.Accepted;
                }
                return AuthCode.InvalidReconnectToken;
            }
            foreach (var occupied in _slots)
            {
                if (occupied != null && string.Equals(occupied.Info.Nickname, nickname, StringComparison.OrdinalIgnoreCase))
                {
                    return AuthCode.NicknameTaken;
                }
            }

            int slot = Array.FindIndex(_slots, value => value == null);
            if (slot < 0 || _nextId == 0)
            {
                return AuthCode.ServerFull;
            }

            player = new SessionPlayer(_nextId++, nickname, slot);
            int teamA = 0, teamB = 0;
            foreach (var item in _slots)
            {
                if (item != null)
                {
                    if (item.Info.Team == Team.A)
                    {
                        teamA++;
                    }
                    else
                    {
                        teamB++;
                    }
                }
            }

            var joinedInfo = player.Info;
            joinedInfo.Team = teamA <= teamB ? Team.A : Team.B;
            player.Info = joinedInfo;
            player.Token = NewToken();
            _slots[slot] = player;
            Publish();
            return AuthCode.Accepted;
        }

        public bool Contains(SessionPlayer player) => player != null && player.Slot >= 0
            && player.Slot < _slots.Length && ReferenceEquals(_slots[player.Slot], player);

        public void MarkSpawned(SessionPlayer player)
        {
            if (!Contains(player))
            {
                throw new InvalidOperationException("Player has no authenticated session slot.");
            }

            player.HasHero = true;
        }

        public void CompleteRestore(SessionPlayer player)
        {
            if (!Contains(player) || !player.HasHero || !player.Info.Connected)
            {
                throw new InvalidOperationException("Only an authenticated reserved hero can complete restoration.");
            }

            player.PreviousToken = null;
        }

        public void Disconnect(SessionPlayer player, bool reserve)
        {
            if (!Contains(player))
            {
                return;
            }

            if (!reserve || !player.HasHero)
            {
                Remove(player.Info.SessionPlayerId);
                return;
            }
            var info = player.Info;
            if (info.Connected)
            {
                player.Deadline = NetworkTime.time + ReconnectRegistry.GracePeriod;
            }

            info.Connected = false;
            info.Ready = false;
            player.Info = info;
            Publish();
        }

        public void Remove(uint id)
        {
            int slot = Array.FindIndex(_slots, player => player != null && player.Info.SessionPlayerId == id);
            if (slot < 0)
            {
                return;
            }

            _slots[slot] = null;
            Publish();
        }

        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            _nextId = 1;
            Publish();
        }

        private void Publish()
        {
            _players.Clear();
            foreach (var player in _slots)
            {
                if (player != null)
                {
                    _players.Add(player.Info);
                }
            }

            Changed?.Invoke();
        }

        private static string NewToken()
        {
            byte[] bytes = new byte[16];
            using var random = RandomNumberGenerator.Create();
            random.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
