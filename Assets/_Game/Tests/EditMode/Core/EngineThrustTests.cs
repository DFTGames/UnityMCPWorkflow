using System;
using System.Numerics;
using NUnit.Framework;
using YASS.Core;

namespace YASS.Tests.Core
{
    public class EngineThrustTests
    {
        [Test]
        public void Target_IsIdleAtRest()
        {
            Assert.That(EngineThrust.Target(Vector2.Zero, 0.3f), Is.EqualTo(0.3f));
        }

        [Test]
        public void Target_IsFullAtFullDeflectionInAnyDirection()
        {
            Assert.That(EngineThrust.Target(Vector2.UnitX, 0.3f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(EngineThrust.Target(-Vector2.UnitY, 0.3f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(EngineThrust.Target(new Vector2(1f, 1f), 0.3f), Is.EqualTo(1f), "clamped above full deflection");
        }

        [Test]
        public void Target_ScalesWithPartialDeflection()
        {
            Assert.That(EngineThrust.Target(new Vector2(0.5f, 0f), 0.2f), Is.EqualTo(0.6f).Within(1e-6f));
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void Target_RejectsIdleOutsideUnitRange(float idle)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EngineThrust.Target(Vector2.Zero, idle));
        }

        [Test]
        public void Smooth_MovesPartWayAndConverges()
        {
            var once = EngineThrust.Smooth(0f, 1f, 8f, 0.1f);
            Assert.That(once, Is.EqualTo(1f - MathF.Exp(-0.8f)).Within(1e-6f));

            var value = 0f;
            for (var i = 0; i < 120; i++) value = EngineThrust.Smooth(value, 1f, 8f, 1f / 60f);
            Assert.That(value, Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void Smooth_IsFrameRateIndependent()
        {
            var coarse = EngineThrust.Smooth(0f, 1f, 5f, 0.2f);
            var fine = 0f;
            for (var i = 0; i < 4; i++) fine = EngineThrust.Smooth(fine, 1f, 5f, 0.05f);

            Assert.That(fine, Is.EqualTo(coarse).Within(1e-5f));
        }

        [Test]
        public void Smooth_EasesDownwardsWithoutOvershooting()
        {
            var value = EngineThrust.Smooth(1f, 0.3f, 8f, 0.1f);
            Assert.That(value, Is.LessThan(1f).And.GreaterThan(0.3f));

            Assert.That(EngineThrust.Smooth(1f, 0.3f, 8f, 100f), Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void Smooth_ZeroTimeOrResponsiveness_DoesNotMove()
        {
            Assert.That(EngineThrust.Smooth(0.4f, 1f, 8f, 0f), Is.EqualTo(0.4f));
            Assert.That(EngineThrust.Smooth(0.4f, 1f, 0f, 1f), Is.EqualTo(0.4f));
        }

        [Test]
        public void Smooth_RejectsNegativeArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EngineThrust.Smooth(0f, 1f, -1f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => EngineThrust.Smooth(0f, 1f, 1f, -0.1f));
        }
    }
}
