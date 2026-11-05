using System;
using Game.Core.Geometry;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode
{
    public sealed class Rect2Tests
    {
        [Test]
        public void Constructor_MinExceedsMax_ThrowsArgumentException()
        {
            // Arrange.
            float2 min = new(2f, 0f);
            float2 max = float2.zero;

            // Act and Assert.
            Assert.That(() => new Rect2(min, max), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_MaxIsNotFinite_ThrowsArgumentException()
        {
            // Arrange.
            float2 max = new(float.PositiveInfinity);

            // Act and Assert.
            Assert.That(() => new Rect2(float2.zero, max), Throws.TypeOf<ArgumentException>());
        }
    }
}
