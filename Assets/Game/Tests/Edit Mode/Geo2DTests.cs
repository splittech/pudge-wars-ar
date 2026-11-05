using System;
using Game.Core.Geometry;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class Geo2DTests
    {
        private const float ContactTolerance = 1e-6f;

        [TestCase(5f, 0f, 0.4f)]
        [TestCase(5f, 1f, 0.5f)]
        [TestCase(11f, 0f, 1f)]
        public void SweptCircle_PathTouchesTarget_ReturnsFirstContact(float targetX, float targetY, float expectedContact)
        {
            // Arrange.
            float2 start = float2.zero;
            float2 end = new(10f, 0f);
            float2 target = new(targetX, targetY);

            // Act.
            bool hit = Geo2D.SweptCircle(start, end, movingRadius: 0.5f, target, targetRadius: 0.5f, out float contact);

            // Assert.
            Assert.That(hit, Is.True);
            Assert.That(contact, Is.EqualTo(expectedContact).Within(ContactTolerance));
        }

        [TestCase(5f, 1.01f)]
        [TestCase(-2f, 0f)]
        [TestCase(12f, 0f)]
        public void SweptCircle_TargetOutsidePath_ReturnsFalseAndZeroContact(float targetX, float targetY)
        {
            // Arrange.
            float2 target = new(targetX, targetY);

            // Act.
            bool hit = Geo2D.SweptCircle(float2.zero, new float2(10f, 0f), 0.5f, target, 0.5f, out float contact);

            // Assert.
            Assert.That(hit, Is.False);
            Assert.That(contact, Is.Zero);
        }

        [TestCase(0f, true)]
        [TestCase(1f, true)]
        [TestCase(1.01f, false)]
        public void SweptCircle_NoMovement_HitsOnlyInitiallyOverlappingTarget(float targetX, bool expectedHit)
        {
            // Arrange.
            float2 position = float2.zero;
            float2 target = new(targetX, 0f);

            // Act.
            bool hit = Geo2D.SweptCircle(position, position, 0.5f, target, 0.5f, out float contact);

            // Assert.
            Assert.That(hit, Is.EqualTo(expectedHit));
            Assert.That(contact, Is.Zero);
        }

        [Test]
        public void SweptCircle_InitiallyOverlappingAndMovingAway_ReturnsZeroContact()
        {
            // Arrange.
            float2 end = new(-10f, 0f);
            float2 target = new(0.5f, 0f);

            // Act.
            bool hit = Geo2D.SweptCircle(float2.zero, end, 0.5f, target, 0.5f, out float contact);

            // Assert.
            Assert.That(hit, Is.True);
            Assert.That(contact, Is.Zero);
        }

        [Test]
        public void SweptCircle_FastHookCrossesHero_HitIsDetectedBetweenTickPositions()
        {
            // Arrange.
            const float hookSpeed = 60f;
            float2 end = new(hookSpeed * Setup.TickDeltaTime, 0f);
            float2 target = new(1f, 0f);

            // Act.
            bool hit = Geo2D.SweptCircle(float2.zero, end, 0.35f, target, 0.5f, out float contact);

            // Assert.
            Assert.That(hit, Is.True);
            Assert.That(contact, Is.EqualTo(0.075f).Within(ContactTolerance));
        }

        [Test]
        public void SweptCircle_FartherTargetHasLargerRadius_LargerTargetIsTouchedFirst()
        {
            // Arrange.
            float2 end = new(10f, 0f);
            float2 smallTarget = new(4f, 0f);
            float2 largeTarget = new(5f, 0f);

            // Act.
            bool smallHit = Geo2D.SweptCircle(float2.zero, end, 0f, smallTarget, 0.5f, out float smallContact);
            bool largeHit = Geo2D.SweptCircle(float2.zero, end, 0f, largeTarget, 2f, out float largeContact);

            // Assert.
            Assert.That(smallHit, Is.True);
            Assert.That(largeHit, Is.True);
            Assert.That(largeContact, Is.LessThan(smallContact));
        }

        [Test]
        public void SweptCircle_DiagonalPath_ReturnsFirstContact()
        {
            // Arrange.
            float2 start = new(-2f, -2f);
            float2 end = new(2f, 2f);
            float radius = math.sqrt(2f);

            // Act.
            bool hit = Geo2D.SweptCircle(start, end, 0f, float2.zero, radius, out float contact);

            // Assert.
            Assert.That(hit, Is.True);
            Assert.That(contact, Is.EqualTo(0.25f).Within(ContactTolerance));
        }

        [TestCase(1f)]
        [TestCase(1e-10f)]
        [TestCase(1e20f)]
        public void SweptCircle_ScaledGeometry_ReturnsSameContactFraction(float scale)
        {
            // Arrange.
            float2 end = new(10f * scale, 0f);
            float2 target = new(5f * scale, 0f);

            // Act.
            bool hit = Geo2D.SweptCircle(float2.zero, end, scale, target, scale, out float contact);

            // Assert.
            Assert.That(hit, Is.True);
            Assert.That(contact, Is.EqualTo(0.3f).Within(ContactTolerance));
        }

        [TestCase(0f, true)]
        [TestCase(2f, true)]
        [TestCase(2.01f, false)]
        public void CircleCircle_CentersHaveGivenDistance_ReturnsExpectedIntersection(float distance, bool expectedHit)
        {
            // Arrange.
            float2 secondCenter = new(distance, 0f);

            // Act.
            bool hit = Geo2D.CircleCircle(float2.zero, 1f, secondCenter, 1f);

            // Assert.
            Assert.That(hit, Is.EqualTo(expectedHit));
        }

        [TestCase(-9f, 9f, -2f, 5f)]
        [TestCase(9f, -9f, 4f, -3f)]
        [TestCase(0f, 0f, 0f, 0f)]
        public void ClampToRect_PointInsideOrOutsideBounds_ReturnsClampedPoint(float x, float y, float expectedX, float expectedY)
        {
            // Arrange.
            Rect2 bounds = new(new float2(-2f, -3f), new float2(4f, 5f));
            float2 point = new(x, y);

            // Act.
            float2 clamped = Geo2D.ClampToRect(point, bounds);

            // Assert.
            Assert.That(clamped, Is.EqualTo(new float2(expectedX, expectedY)));
        }

        [Test]
        public void ClampToRect_PointIsNotFinite_ThrowsArgumentException()
        {
            // Arrange.
            float2 point = new(float.NaN);

            // Act and Assert.
            Assert.That(() => Geo2D.ClampToRect(point, default), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void CircleCircle_RadiusIsNegative_ThrowsArgumentOutOfRangeException()
        {
            // Arrange.
            const float radius = -1f;

            // Act and Assert.
            Assert.That(() => Geo2D.CircleCircle(float2.zero, radius, float2.zero, 1f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SweptCircle_RadiusIsNotFinite_ThrowsArgumentOutOfRangeException()
        {
            // Arrange.
            float radius = float.NaN;

            // Act and Assert.
            Assert.That(() => Geo2D.SweptCircle(float2.zero, float2.zero, 0f, float2.zero, radius, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SweptCircle_StartIsNotFinite_ThrowsArgumentException()
        {
            // Arrange.
            float2 start = new(float.NaN);

            // Act and Assert.
            Assert.That(() => Geo2D.SweptCircle(start, float2.zero, 0f, float2.zero, 0f, out _),
                Throws.TypeOf<ArgumentException>());
        }
    }
}