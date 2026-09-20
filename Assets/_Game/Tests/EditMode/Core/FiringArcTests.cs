using System;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class FiringArcTests
    {
        const float Max = GameTuning.FiringArcDegrees;

        static Vector2 Direction(float degrees)
        {
            var radians = degrees * MathF.PI / 180f;
            return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        }

        [Test]
        public void Arc_MatchesTheGdd()
        {
            Assert.That(GameTuning.FiringArcDegrees, Is.EqualTo(35f));
        }

        [Test]
        public void Degrees_ForwardAimIsLevel()
        {
            Assert.That(FiringArc.Degrees(Vector2.UnitX, Max), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Degrees_ShallowAimKeepsItsAngle()
        {
            Assert.That(FiringArc.Degrees(Direction(20f), Max), Is.EqualTo(20f).Within(1e-3f));
            Assert.That(FiringArc.Degrees(Direction(-20f), Max), Is.EqualTo(-20f).Within(1e-3f));
        }

        [Test]
        public void Degrees_SteepAimStopsAtTheArcEdge()
        {
            Assert.That(FiringArc.Degrees(Vector2.UnitY, Max), Is.EqualTo(Max));
            Assert.That(FiringArc.Degrees(-Vector2.UnitY, Max), Is.EqualTo(-Max));
        }

        [Test]
        public void Degrees_BackwardAimIsMirroredOntoTheForwardHalf()
        {
            Assert.That(FiringArc.Degrees(-Vector2.UnitX, Max), Is.EqualTo(0f).Within(1e-4f), "straight back stays level");
            Assert.That(FiringArc.Degrees(Direction(160f), Max), Is.EqualTo(20f).Within(1e-3f), "up-and-back aims up-and-forward");
            Assert.That(FiringArc.Degrees(new Vector2(-1f, -1f), Max), Is.EqualTo(-Max), "down-and-back aims down-and-forward");
        }

        [Test]
        public void Clamp_ForwardAimIsUnchanged()
        {
            AssertVector(Vector2.UnitX, FiringArc.Clamp(Vector2.UnitX, Max));
        }

        [Test]
        public void Clamp_ShallowAimKeepsItsAngle()
        {
            var aim = Direction(20f);

            AssertVector(aim, FiringArc.Clamp(aim, Max), 1e-4f);
        }

        [Test]
        public void Clamp_SteepAimStopsAtTheArcEdge()
        {
            // The arc is 35 degrees either way: cos 35 = 0.81915, sin 35 = 0.57358.
            AssertVector(new Vector2(0.81915f, 0.57358f), FiringArc.Clamp(Vector2.UnitY, Max), 1e-4f);
            AssertVector(new Vector2(0.81915f, -0.57358f), FiringArc.Clamp(-Vector2.UnitY, Max), 1e-4f);
        }

        [Test]
        public void Clamp_NeverFiresBackwards()
        {
            foreach (var aim in new[] { -Vector2.UnitX, new Vector2(-1f, 1f), new Vector2(-1f, -1f), new Vector2(-3f, 0.2f) })
            {
                var shot = FiringArc.Clamp(aim, Max);
                Assert.That(shot.X, Is.GreaterThan(0f), $"aim {aim} fired backwards");
                Assert.That(shot.Length(), Is.EqualTo(1f).Within(1e-5f));
            }
        }

        [Test]
        public void Clamp_MirrorsBackwardAimOntoTheForwardHalf()
        {
            AssertVector(Vector2.UnitX, FiringArc.Clamp(-Vector2.UnitX, Max), 1e-4f);
            AssertVector(Direction(20f), FiringArc.Clamp(Direction(160f), Max), 1e-4f);
        }

        [Test]
        public void Clamp_MatchesTheTiltAngle()
        {
            foreach (var aim in new[] { Vector2.UnitY, Direction(10f), Direction(-50f), new Vector2(-1f, 2f) })
            {
                var tilt = ShipTilt.TargetDegrees(true, aim, Max);
                AssertVector(Direction(tilt), FiringArc.Clamp(aim, Max), 1e-4f,
                    "shots must travel along the ship's nose");
            }
        }

        [Test]
        public void Clamp_IgnoresMagnitude()
        {
            AssertVector(FiringArc.Clamp(Direction(20f), Max), FiringArc.Clamp(Direction(20f) * 400f, Max), 1e-4f);
            AssertVector(FiringArc.Clamp(Direction(20f), Max), FiringArc.Clamp(Direction(20f) * 0.001f, Max), 1e-4f);
        }

        [TestCase(float.NaN, 1f)]
        [TestCase(1f, float.PositiveInfinity)]
        public void Degrees_NonFiniteAimIsLevel(float x, float y)
        {
            Assert.That(FiringArc.Degrees(new Vector2(x, y), Max), Is.EqualTo(0f));
        }

        [Test]
        public void Clamp_NoAimFiresStraightAhead()
        {
            AssertVector(Vector2.UnitX, FiringArc.Clamp(Vector2.Zero, Max));
            AssertVector(Vector2.UnitX, FiringArc.Clamp(new Vector2(float.NaN, 1f), Max));
        }

        [Test]
        public void Degrees_NegativeArcThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FiringArc.Degrees(Vector2.UnitY, -1f));
        }
    }
}
