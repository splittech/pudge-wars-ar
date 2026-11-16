using Game.Core.Match;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class MatchRulesTests
    {
        [TestCase(false, true, 1, 1)]
        [TestCase(true, false, 1, 1)]
        [TestCase(true, true, 0, 1)]
        [TestCase(true, true, 1, 0)]
        public void Begin_InvalidLobby_Rejects(bool host, bool ready, int a, int b)
        {
            // Arrange.
            var rules = new MatchRules(Setup.MatchConfig());
            // Act.
            bool accepted = rules.Begin(10, host, ready, a, b);
            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(rules.Phase, Is.EqualTo(MatchPhase.Lobby));
        }

        [Test]
        public void Advance_CountdownDeadline_StartsWithAbsoluteMatchEnd()
        {
            // Arrange.
            var rules = new MatchRules(Setup.MatchConfig(duration: 60));
            rules.Begin(10, true, true, 1, 1);
            rules.ArenaPlaced(10);
            // Act.
            rules.Advance(13, 1, 1);
            // Assert.
            Assert.That(rules.Phase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(rules.CountdownEnd, Is.EqualTo(13));
            Assert.That(rules.MatchEnd, Is.EqualTo(73));
        }

        [TestCase(0, 0, MatchWinner.Draw)]
        [TestCase(1, 0, MatchWinner.A)]
        [TestCase(0, 1, MatchWinner.B)]
        public void Advance_TimeExpires_ResolvesScore(int a, int b, MatchWinner winner)
        {
            // Arrange.
            var rules = new MatchRules(Setup.MatchConfig(duration: 60));
            rules.Begin(0, true, true, 1, 1); rules.ArenaPlaced(0); rules.Advance(3, 1, 1);
            for (int i = 0; i < a; i++) rules.AddKill(Team.A);
            for (int i = 0; i < b; i++) rules.AddKill(Team.B);
            // Act.
            rules.Advance(63, 1, 1);
            // Assert.
            Assert.That(rules.Phase, Is.EqualTo(MatchPhase.Results));
            Assert.That(rules.Winner, Is.EqualTo(winner));
        }

        [Test]
        public void Advance_KillLimit_EndsBeforeTimer()
        {
            // Arrange.
            var rules = new MatchRules(Setup.MatchConfig(killsToWin: 1));
            rules.Begin(0, true, true, 1, 1); rules.ArenaPlaced(0); rules.Advance(3, 1, 1);
            rules.AddKill(Team.B);
            // Act.
            rules.Advance(4, 1, 1);
            // Assert.
            Assert.That(rules.Phase, Is.EqualTo(MatchPhase.Results));
            Assert.That(rules.Winner, Is.EqualTo(MatchWinner.B));
        }

        [Test]
        public void Advance_TeamReleased_EndsMatch()
        {
            // Arrange.
            var rules = new MatchRules(Setup.MatchConfig());
            rules.Begin(0, true, true, 1, 1); rules.ArenaPlaced(0); rules.Advance(3, 1, 1);
            // Act.
            rules.Advance(4, 0, 1);
            // Assert.
            Assert.That(rules.Winner, Is.EqualTo(MatchWinner.B));
            Assert.That(rules.Phase, Is.EqualTo(MatchPhase.Results));
        }

        [Test]
        public void Rematch_Results_ResetsScoreAndDeadline()
        {
            // Arrange.
            var rules = new MatchRules(Setup.MatchConfig(killsToWin: 1));
            rules.Begin(0, true, true, 1, 1); rules.ArenaPlaced(0); rules.Advance(3, 1, 1);
            rules.AddKill(Team.A); rules.Advance(4, 1, 1);
            // Act.
            bool accepted = rules.Rematch(10, true, 1, 1);
            // Assert.
            Assert.That(accepted, Is.True);
            Assert.That(rules.ScoreA, Is.Zero);
            Assert.That(rules.CountdownEnd, Is.EqualTo(13));
            Assert.That(rules.Phase, Is.EqualTo(MatchPhase.Countdown));
        }

        [Test]
        public void ReturnToLobby_NonHost_Rejects()
        {
            // Arrange.
            var rules = new MatchRules(Setup.MatchConfig());
            rules.Begin(0, true, true, 1, 1); rules.ArenaPlaced(0); rules.Advance(3, 1, 1); rules.Advance(4, 0, 1);
            // Act.
            bool accepted = rules.ReturnToLobby(false);
            // Assert.
            Assert.That(accepted, Is.False);
            Assert.That(rules.Phase, Is.EqualTo(MatchPhase.Results));
        }
    }
}
