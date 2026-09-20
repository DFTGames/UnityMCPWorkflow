using System;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class ShipTiltTests
    {
        const float Max = GameTuning.FiringArcDegrees;

        [Test]
        public void NotFiring_IsLevel()
        {
            Assert.That(ShipTilt.TargetDegrees(false, Vector2.UnitY, Max), Is.EqualTo(0f));
        }

        [Test]
        public void FiringWithNoAim_IsLevel()
        {
            Assert.That(ShipTilt.TargetDegrees(true, Vector2.Zero, Max), Is.EqualTo(0f));
        }

        [Test]
        public void Firing_TiltsByTheFiringAngle()
        {
            foreach (var aim in new[] { Vector2.UnitX, Vector2.UnitY, new Vector2(2f, 1f), new Vector2(-1f, 1f) })
                Assert.That(ShipTilt.TargetDegrees(true, aim, Max), Is.EqualTo(FiringArc.Degrees(aim, Max)),
                    $"aim {aim}: the ship must point where its shots go");
        }

        [Test]
        public void SteepAim_IsClampedToTheArc()
        {
            Assert.That(ShipTilt.TargetDegrees(true, Vector2.UnitY, Max), Is.EqualTo(35f));
            Assert.That(ShipTilt.TargetDegrees(true, -Vector2.UnitY, Max), Is.EqualTo(-35f));
        }

        [Test]
        public void Step_EasesTowardsTheTarget()
        {
            var value = 0f;
            for (var i = 0; i < 60; i++) value = ShipTilt.Step(value, 30f, 12f, 1f / 60f);

            Assert.That(value, Is.EqualTo(30f).Within(0.01f));
            Assert.That(ShipTilt.Step(0f, 30f, 12f, 1f / 60f), Is.GreaterThan(0f).And.LessThan(30f));
        }

        [Test]
        public void Step_SnapsWhenCloseEnough()
        {
            Assert.That(ShipTilt.Step(29.999f, 30f, 12f, 1f / 60f), Is.EqualTo(30f));
            Assert.That(ShipTilt.Step(0f, 0f, 12f, 1f / 60f), Is.EqualTo(0f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NegativeArc_Throws(bool firing)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ShipTilt.TargetDegrees(firing, Vector2.UnitY, -1f));
        }
    }
}
