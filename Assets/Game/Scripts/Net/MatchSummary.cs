using Game.Core.Match;

namespace Game.Net
{
    public struct MatchSummary
    {
        public GameMode Mode;
        public uint WinnerPlayerId;
        public MatchPhase Phase;
        public double CountdownEnd;
        public double MatchEnd;
        public int ScoreA;
        public int ScoreB;
        public MatchWinner Winner;
    }

    public struct PlayerMatchStats
    {
        public uint PlayerId;
        public string Nickname;
        public Team Team;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int HookHits;
    }
}
