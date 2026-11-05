using System;
using Game.Core.Configs;
using Game.Core.Geometry;
using Game.Core.Match;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class ArenaBoundsTests
    {
        [TestCase(Team.A, -11.5f, -2.25f)]
        [TestCase(Team.B, 2.25f, 11.5f)]
        public void ForTeam_DefaultArena_ReturnsBoundsInsetByHeroRadius(Team team, float minX, float maxX)
        {
            // Arrange.
            ArenaConfigData arena = Setup.Arena();

            // Act.
            Rect2 bounds = ArenaBounds.ForTeam(arena, team, heroRadius: 0.5f);

            // Assert.
            Assert.That(bounds.Min, Is.EqualTo(new float2(minX, -6.5f)));
            Assert.That(bounds.Max, Is.EqualTo(new float2(maxX, 6.5f)));
        }

        [Test]
        public void ForTeam_HeroDoesNotFit_ThrowsArgumentException()
        {
            // Arrange.
            ArenaConfigData arena = Setup.Arena();

            // Act and Assert.
            Assert.That(() => ArenaBounds.ForTeam(arena, Team.A, heroRadius: 6f),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ForTeam_HeroRadiusIsNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange.
            ArenaConfigData arena = Setup.Arena();

            // Act and Assert.
            Assert.That(() => ArenaBounds.ForTeam(arena, Team.A, heroRadius: -1f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ForTeam_TeamIsUnknown_ThrowsArgumentOutOfRangeException()
        {
            // Arrange.
            ArenaConfigData arena = Setup.Arena();
            Team team = (Team)9;

            // Act and Assert.
            Assert.That(() => ArenaBounds.ForTeam(arena, team, heroRadius: 0f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0f, 14f, 0f)]
        [TestCase(24f, 14f, 24f)]
        public void ForTeam_ArenaDimensionsAreInvalid_ThrowsArgumentException(float width, float height, float riverWidth)
        {
            // Arrange.
            ArenaConfigData arena = Setup.Arena(new float2(width, height), riverWidth);

            // Act and Assert.
            Assert.That(() => ArenaBounds.ForTeam(arena, Team.A, heroRadius: 0f),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
