using System;
using Game.Core.Configs;
using Game.Core.Geometry;
using Game.Core.Match;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class Geo2DTests
    {
        [Test]
        public void Sweep_CenterHit_ReturnsFirstContact()
        {
            Assert.That(Geo2D.SweptCircle(float2.zero, new float2(10, 0), 0.5f,
                new float2(5, 0), 0.5f, out float t), Is.True);
            Assert.That(t, Is.EqualTo(0.4f).Within(1e-6f));
        }

        [Test]
        public void Sweep_Tangent_IsHit()
        {
            Assert.That(Geo2D.SweptCircle(float2.zero, new float2(10, 0), 0.5f,
                new float2(5, 1), 0.5f, out float t), Is.True);
            Assert.That(t, Is.EqualTo(0.5f).Within(1e-6f));
        }

        [TestCase(5f, 1.01f)]
        [TestCase(-2f, 0f)]
        [TestCase(12f, 0f)]
        public void Sweep_Miss_ReturnsFalseAndZero(float x, float y)
        {
            Assert.That(Geo2D.SweptCircle(float2.zero, new float2(10, 0), 0.5f,
                new float2(x, y), 0.5f, out float t), Is.False);
            Assert.That(t, Is.Zero);
        }

        [TestCase(0f, true)]
        [TestCase(1f, true)]
        [TestCase(1.01f, false)]
        public void Sweep_StationaryCircle_OnlyHitsInitialOverlap(float x, bool expected)
        {
            Assert.That(Geo2D.SweptCircle(float2.zero, float2.zero, 0.5f,
                new float2(x, 0), 0.5f, out float t), Is.EqualTo(expected));
            Assert.That(t, Is.Zero);
        }

        [Test]
        public void Sweep_InitiallyOverlappingAndMovingAway_ReturnsZero()
        {
            Assert.That(Geo2D.SweptCircle(float2.zero, new float2(-10, 0), 0.5f,
                new float2(0.5f, 0), 0.5f, out float t), Is.True);
            Assert.That(t, Is.Zero);
        }

        [Test]
        public void Sweep_ContactAtEnd_IsHit()
        {
            Assert.That(Geo2D.SweptCircle(float2.zero, new float2(4, 0), 0.5f,
                new float2(5, 0), 0.5f, out float t), Is.True);
            Assert.That(t, Is.EqualTo(1f));
        }

        [Test]
        public void Sweep_FastHook_DoesNotTunnelThroughHero()
        {
            float distance = 60f * (1f / 30f);
            Assert.That(Geo2D.SweptCircle(float2.zero, new float2(distance, 0), 0.35f,
                new float2(1, 0), 0.5f, out float t), Is.True);
            Assert.That(t, Is.EqualTo(0.075f).Within(1e-6f));
        }

        [Test]
        public void Sweep_DifferentRadii_OrdersContactsRatherThanCenters()
        {
            Geo2D.SweptCircle(float2.zero, new float2(10, 0), 0,
                new float2(4, 0), 0.5f, out float small);
            Geo2D.SweptCircle(float2.zero, new float2(10, 0), 0,
                new float2(5, 0), 2f, out float large);
            Assert.That(large, Is.LessThan(small));
        }

        [Test]
        public void Sweep_Diagonal_ReturnsContact()
        {
            Assert.That(Geo2D.SweptCircle(new float2(-2, -2), new float2(2, 2), 0,
                float2.zero, math.sqrt(2f), out float t), Is.True);
            Assert.That(t, Is.EqualTo(0.25f).Within(1e-6f));
        }

        [TestCase(1f)]
        [TestCase(1e-10f)]
        [TestCase(1e20f)]
        public void Sweep_IsConsistentAcrossScales(float scale)
        {
            Assert.That(Geo2D.SweptCircle(float2.zero, new float2(10 * scale, 0), scale,
                new float2(5 * scale, 0), scale, out float t), Is.True);
            Assert.That(t, Is.EqualTo(0.3f).Within(1e-6f));
        }

        [TestCase(0f, true)]
        [TestCase(2f, true)]
        [TestCase(2.01f, false)]
        public void Circles_IncludeOverlapAndTangency(float distance, bool expected)
        {
            Assert.That(Geo2D.CircleCircle(float2.zero, 1, new float2(distance, 0), 1),
                Is.EqualTo(expected));
        }

        [Test]
        public void Clamp_ClampsBothAxesAndPreservesInterior()
        {
            var rect = new Rect2(new float2(-2, -3), new float2(4, 5));
            Assert.That(Geo2D.ClampToRect(new float2(-9, 9), rect), Is.EqualTo(new float2(-2, 5)));
            Assert.That(Geo2D.ClampToRect(new float2(9, -9), rect), Is.EqualTo(new float2(4, -3)));
            Assert.That(Geo2D.ClampToRect(float2.zero, rect), Is.EqualTo(float2.zero));
        }

        [Test]
        public void Bounds_AccountForRiverAndHeroRadius()
        {
            var arena = Arena(new float2(24, 14), 3.5f);
            Rect2 a = ArenaBounds.ForTeam(arena, Team.A, 0.5f);
            Rect2 b = ArenaBounds.ForTeam(arena, Team.B, 0.5f);
            Assert.That(a.Min, Is.EqualTo(new float2(-11.5f, -6.5f)));
            Assert.That(a.Max, Is.EqualTo(new float2(-2.25f, 6.5f)));
            Assert.That(b.Min, Is.EqualTo(new float2(2.25f, -6.5f)));
            Assert.That(b.Max, Is.EqualTo(new float2(11.5f, 6.5f)));
        }

        [Test]
        public void Bounds_InvalidArenaOrHero_Throws()
        {
            var arena = Arena(new float2(24, 14), 3.5f);
            Assert.Throws<ArgumentException>(() => ArenaBounds.ForTeam(arena, Team.A, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArenaBounds.ForTeam(arena, Team.A, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArenaBounds.ForTeam(arena, (Team)9, 0));
            Assert.Throws<ArgumentException>(() => ArenaBounds.ForTeam(Arena(new float2(0, 14), 0), Team.A, 0));
            Assert.Throws<ArgumentException>(() => ArenaBounds.ForTeam(Arena(new float2(24, 14), 24), Team.A, 0));
        }

        [Test]
        public void Geometry_InvalidNumbersOrBounds_Throw()
        {
            Assert.Throws<ArgumentException>(() => new Rect2(new float2(2, 0), float2.zero));
            Assert.Throws<ArgumentException>(() => new Rect2(float2.zero, new float2(float.PositiveInfinity)));
            Assert.Throws<ArgumentException>(() => Geo2D.ClampToRect(new float2(float.NaN), default));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Geo2D.CircleCircle(float2.zero, -1, float2.zero, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Geo2D.SweptCircle(float2.zero, float2.zero, 0, float2.zero, float.NaN, out _));
            Assert.Throws<ArgumentException>(() =>
                Geo2D.SweptCircle(new float2(float.NaN), float2.zero, 0, float2.zero, 0, out _));
        }

        private static ArenaConfigData Arena(float2 size, float river)
        {
            return new ArenaConfigData(size, river, Array.Empty<float2>(), Array.Empty<float2>());
        }
    }
}
