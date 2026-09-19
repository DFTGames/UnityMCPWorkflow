using System;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class PlayfieldTests
    {
        [Test]
        public void FromCamera_UsesOrthographicSizeAndAspect()
        {
            var field = Playfield.FromCamera(new Vector2(1f, 2f), 5f, 16f / 9f);

            Assert.That(field.Height, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(field.Width, Is.EqualTo(160f / 9f).Within(1e-4f));
            Assert.That(field.MinY, Is.EqualTo(-3f).Within(Tolerance));
            Assert.That(field.MaxX, Is.EqualTo(1f + 80f / 9f).Within(1e-4f));
        }

        [Test]
        public void FromCamera_FixedHeightAcrossAspects()
        {
            var narrow = Playfield.FromCamera(Vector2.Zero, 5f, 4f / 3f);
            var wide = Playfield.FromCamera(Vector2.Zero, 5f, 21f / 9f);

            Assert.That(narrow.Height, Is.EqualTo(wide.Height));
            Assert.That(wide.Width, Is.GreaterThan(narrow.Width));
        }

        [Test]
        public void FromCamera_RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Playfield.FromCamera(Vector2.Zero, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Playfield.FromCamera(Vector2.Zero, 5f, 0f));
        }

        [Test]
        public void Constructor_RejectsInvertedBounds()
        {
            Assert.Throws<ArgumentException>(() => new Playfield(1f, 0f, 0f, 1f));
            Assert.Throws<ArgumentException>(() => new Playfield(0f, 1f, 1f, 0f));
        }

        [Test]
        public void Inset_ShrinksAndNegativeGrows()
        {
            var field = new Playfield(-5f, 5f, -3f, 3f);

            var inner = field.Inset(1f);
            Assert.That(inner.MinX, Is.EqualTo(-4f));
            Assert.That(inner.MaxY, Is.EqualTo(2f));

            var outer = field.Inset(-1f);
            Assert.That(outer.MinX, Is.EqualTo(-6f));
            Assert.That(outer.MaxY, Is.EqualTo(4f));
        }

        [Test]
        public void Inset_TooLarge_CollapsesToCentreLine()
        {
            var inner = new Playfield(0f, 10f, 0f, 2f).Inset(3f);

            Assert.That(inner.MinY, Is.EqualTo(1f));
            Assert.That(inner.MaxY, Is.EqualTo(1f));
            Assert.That(inner.MinX, Is.EqualTo(3f));
            Assert.That(inner.MaxX, Is.EqualTo(7f));
        }

        [Test]
        public void Contains_IsInclusiveOfEdges()
        {
            var field = new Playfield(0f, 10f, 0f, 5f);

            Assert.That(field.Contains(new Vector2(0f, 5f)), Is.True);
            Assert.That(field.Contains(new Vector2(10.01f, 1f)), Is.False);
            Assert.That(field.Contains(new Vector2(1f, -0.01f)), Is.False);
        }

        [Test]
        public void Clamp_KeepsPointInside()
        {
            var field = new Playfield(0f, 10f, 0f, 5f);

            AssertVector(new Vector2(10f, 0f), field.Clamp(new Vector2(12f, -3f)));
            AssertVector(new Vector2(4f, 2f), field.Clamp(new Vector2(4f, 2f)));
        }

        [TestCase(0f, -3f)]
        [TestCase(0.5f, 0f)]
        [TestCase(1f, 3f)]
        [TestCase(2f, 3f)]
        [TestCase(-1f, -3f)]
        public void YAt_MapsNormalisedHeight(float normalised, float expected)
        {
            Assert.That(new Playfield(0f, 1f, -3f, 3f).YAt(normalised), Is.EqualTo(expected));
        }
    }

    public class ShipMotorTests
    {
        static readonly Playfield Field = new Playfield(-10f, 10f, -5f, 5f);

        [Test]
        public void Step_MovesAtSpeed()
        {
            var next = ShipMotor.Step(Vector2.Zero, Vector2.UnitX, 8f, 0.5f, Field);

            AssertVector(new Vector2(4f, 0f), next);
        }

        [Test]
        public void Step_DiagonalIsNotFaster()
        {
            var next = ShipMotor.Step(Vector2.Zero, new Vector2(1f, 1f), 8f, 0.5f, Field);

            Assert.That(next.Length(), Is.EqualTo(4f).Within(1e-4f));
        }

        [Test]
        public void Step_PartialStickDeflectionMovesSlower()
        {
            var next = ShipMotor.Step(Vector2.Zero, new Vector2(0.5f, 0f), 8f, 1f, Field);

            AssertVector(new Vector2(4f, 0f), next);
        }

        [Test]
        public void Step_ClampsToBounds()
        {
            var next = ShipMotor.Step(new Vector2(9f, 4f), new Vector2(1f, 1f), 100f, 1f, Field);

            AssertVector(new Vector2(10f, 5f), next);
        }

        [Test]
        public void Step_RejectsNegativeSpeedOrTime()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ShipMotor.Step(Vector2.Zero, Vector2.UnitX, -1f, 1f, Field));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShipMotor.Step(Vector2.Zero, Vector2.UnitX, 1f, -1f, Field));
        }
    }
}
