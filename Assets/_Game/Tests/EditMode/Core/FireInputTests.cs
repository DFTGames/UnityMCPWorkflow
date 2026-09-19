using System.Numerics;
using NUnit.Framework;
using YASS.Core;
using static YASS.Tests.Core.TestUtil;

namespace YASS.Tests.Core
{
    public class FireInputTests
    {
        const float DeadZone = GameTuning.GamepadAimDeadZone;

        [Test]
        public void Gamepad_StickBeyondDeadZone_FiresAlongNormalisedStick()
        {
            Assert.That(FireInput.ResolveGamepad(new Vector2(0.4f, 0.4f), false, DeadZone, out var dir), Is.True);
            AssertVector(Vector2.Normalize(new Vector2(1f, 1f)), dir);
        }

        [Test]
        public void Gamepad_StickInsideDeadZone_DoesNotFire()
        {
            Assert.That(FireInput.ResolveGamepad(new Vector2(0.2f, 0.1f), false, DeadZone, out var dir), Is.False);
            AssertVector(Vector2.Zero, dir);
        }

        [Test]
        public void Gamepad_StickExactlyAtDeadZone_DoesNotFire()
        {
            Assert.That(FireInput.ResolveGamepad(new Vector2(0f, DeadZone), false, DeadZone, out _), Is.False);
        }

        [Test]
        public void Gamepad_TriggerOnly_FiresForward()
        {
            Assert.That(FireInput.ResolveGamepad(Vector2.Zero, true, DeadZone, out var dir), Is.True);
            AssertVector(Vector2.UnitX, dir);
        }

        [Test]
        public void Gamepad_StickAndTrigger_StickWins()
        {
            Assert.That(FireInput.ResolveGamepad(new Vector2(-1f, 0f), true, DeadZone, out var dir), Is.True);
            AssertVector(-Vector2.UnitX, dir);
        }

        [Test]
        public void NegativeDeadZone_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                FireInput.ResolveGamepad(Vector2.UnitX, false, -0.1f, out _));
        }

        [Test]
        public void Pointer_Held_FiresTowardsPointerNormalised()
        {
            Assert.That(FireInput.ResolvePointer(new Vector2(1f, 1f), new Vector2(4f, 5f), true, out var dir),
                Is.True);
            AssertVector(new Vector2(0.6f, 0.8f), dir);
        }

        [Test]
        public void Pointer_OnShip_FiresForward()
        {
            Assert.That(FireInput.ResolvePointer(Vector2.One, Vector2.One, true, out var dir), Is.True);
            AssertVector(Vector2.UnitX, dir);
        }

        [Test]
        public void Pointer_NotHeld_DoesNotFire()
        {
            Assert.That(FireInput.ResolvePointer(Vector2.Zero, Vector2.UnitY, false, out _), Is.False);
        }

        [Test]
        public void VirtualStick_HeldAndDeflected_FiresAlongStick()
        {
            Assert.That(FireInput.ResolveVirtualStick(new Vector2(0f, -0.9f), true, DeadZone, out var dir), Is.True);
            AssertVector(-Vector2.UnitY, dir);
        }

        [Test]
        public void VirtualStick_HeldButCentred_FiresForward()
        {
            Assert.That(FireInput.ResolveVirtualStick(new Vector2(0.1f, 0f), true, DeadZone, out var dir), Is.True);
            AssertVector(Vector2.UnitX, dir);
        }

        [Test]
        public void VirtualStick_NotHeld_DoesNotFire()
        {
            Assert.That(FireInput.ResolveVirtualStick(Vector2.UnitY, false, DeadZone, out _), Is.False);
        }
    }
}
