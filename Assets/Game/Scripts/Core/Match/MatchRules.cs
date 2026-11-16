using System;
using Game.Core.Configs;

namespace Game.Core.Match
{
    public enum MatchPhase : byte { Lobby, Placement, Countdown, Playing, Results }
    public enum MatchWinner : byte { Draw, A, B }

    public sealed class MatchRules
    {
        private readonly MatchConfigData _config;
        public MatchPhase Phase { get; private set; }
        public double CountdownEnd { get; private set; }
        public double MatchEnd { get; private set; }
        public int ScoreA { get; private set; }
        public int ScoreB { get; private set; }
        public MatchWinner Winner { get; private set; }

        public MatchRules(MatchConfigData config) => _config = config;

        public bool Begin(double now, bool host, bool allReady, int teamA, int teamB)
        {
            if (!host || Phase != MatchPhase.Lobby || !allReady || teamA == 0 || teamB == 0)
                return false;
            Phase = MatchPhase.Placement;
            return true;
        }

        public bool ArenaPlaced(double now)
        {
            if (Phase != MatchPhase.Placement) return false;
            StartCountdown(now);
            return true;
        }

        public bool Rematch(double now, bool host, int teamA, int teamB)
        {
            if (!host || Phase != MatchPhase.Results || teamA == 0 || teamB == 0) return false;
            StartCountdown(now);
            return true;
        }

        public bool ReturnToLobby(bool host)
        {
            if (!host || Phase != MatchPhase.Results) return false;
            Phase = MatchPhase.Lobby;
            return true;
        }

        public void Advance(double now, int teamA, int teamB)
        {
            if (Phase == MatchPhase.Countdown && now >= CountdownEnd)
            {
                Phase = MatchPhase.Playing;
                MatchEnd = CountdownEnd + _config.MatchDuration;
            }
            if (Phase != MatchPhase.Playing) return;
            if (teamA == 0 || teamB == 0)
                Finish(teamA == teamB ? MatchWinner.Draw : teamA == 0 ? MatchWinner.B : MatchWinner.A);
            else if (ScoreA >= _config.KillsToWin || ScoreB >= _config.KillsToWin || now >= MatchEnd)
                Finish(ScoreA == ScoreB ? MatchWinner.Draw : ScoreA > ScoreB ? MatchWinner.A : MatchWinner.B);
        }

        public void AddKill(Team team)
        {
            if (Phase != MatchPhase.Playing) return;
            if (team == Team.A) ScoreA++; else ScoreB++;
        }

        private void StartCountdown(double now)
        {
            ScoreA = 0; ScoreB = 0; Winner = MatchWinner.Draw;
            CountdownEnd = now + _config.CountdownDuration;
            MatchEnd = 0;
            Phase = MatchPhase.Countdown;
        }

        private void Finish(MatchWinner winner) { Winner = winner; Phase = MatchPhase.Results; }
    }
}
