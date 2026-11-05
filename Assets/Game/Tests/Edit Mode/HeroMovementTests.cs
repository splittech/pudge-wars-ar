using Game.Core.Geometry;
using Game.Core.Hero;
using Game.Core.Match;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class HeroMovementTests
    {
        [TestCase(false, false)]
        [TestCase(true, true)]
        public void Step_HeroIsDeadOrPulled_PositionDoesNotChange(bool alive, bool pulled)
        {
            // Arrange.
            HeroState hero = Setup.Hero(alive: alive, isPulled: pulled);
            hero.MoveTarget = new float2(-3f, 0f);
            Rect2 bounds = ArenaBounds.ForTeam(Setup.Arena(), Team.A, hero.Radius);
            float2 initialPosition = hero.Pos;

            // Act.
            HeroMovement.Step(ref hero, speed: 3.2f, bounds, dt: Setup.TickDeltaTime);

            // Assert.
            Assert.That(hero.Pos, Is.EqualTo(initialPosition));
        }
    }
}
