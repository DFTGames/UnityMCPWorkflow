using NUnit.Framework;
using YASS.Core;
using NVector2 = System.Numerics.Vector2;

namespace YASS.Tests.Core
{
    /// <summary>
    /// The touch sticks (GDD "Controls", Touch: two virtual sticks, left thumb moves, right thumb aims and
    /// fires while held). Floating, so the stick is wherever the thumb landed; the rules are in pixels and
    /// engine-free, so a phone is not needed to know whether they are right.
    /// </summary>
    public class VirtualStickTests
    {
        const float Radius = 100f;

        static VirtualStick Held(float fromX, float fromY, float toX, float toY)
        {
            var stick = new VirtualStick(Radius);
            stick.Press(new NVector2(fromX, fromY));
            stick.Drag(new NVector2(toX, toY));
            return stick;
        }

        [Test]
        public void AStickThatNobodyIsTouching_ReportsNothing()
        {
            var stick = new VirtualStick(Radius);

            Assert.That(stick.IsHeld, Is.False);
            Assert.That(stick.Deflection, Is.EqualTo(NVector2.Zero));
        }

        [Test]
        public void AThumbThatHasNotMoved_ReportsNothing()
        {
            // The stick centres on the touch, so pressing alone is not a direction.
            var stick = Held(500f, 500f, 500f, 500f);

            Assert.That(stick.IsHeld, Is.True);
            Assert.That(stick.Deflection, Is.EqualTo(NVector2.Zero));
        }

        [Test]
        public void TheStick_CentresWhereTheThumbLanded()
        {
            var stick = Held(300f, 700f, 350f, 700f);

            Assert.That(stick.Origin, Is.EqualTo(new NVector2(300f, 700f)));
            Assert.That(stick.Deflection.X, Is.EqualTo(0.5f).Within(0.001f), "half a radius to the right");
            Assert.That(stick.Deflection.Y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void AThumbAtTheRadius_IsFullDeflection()
        {
            var stick = Held(0f, 0f, 0f, Radius);

            Assert.That(stick.Deflection.Length(), Is.EqualTo(1f).Within(0.001f));
        }

        /// <summary>
        /// A thumb that slides a long way must not become a stronger input than one that moved a sensible
        /// distance: past the radius the stick is simply at full deflection, pointing the same way.
        /// </summary>
        [Test]
        public void AThumbBeyondTheRadius_IsStillOnlyFullDeflection()
        {
            var stick = Held(0f, 0f, Radius * 10f, 0f);

            Assert.That(stick.Deflection.Length(), Is.EqualTo(1f).Within(0.001f));
            Assert.That(stick.Deflection.X, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void ReleasingTheStick_ForgetsWhereItWas()
        {
            var stick = Held(400f, 400f, 460f, 400f);
            stick.Release();

            Assert.That(stick.IsHeld, Is.False);
            Assert.That(stick.Deflection, Is.EqualTo(NVector2.Zero));
        }

        [Test]
        public void DraggingAStickNobodyIsHolding_DoesNothing()
        {
            var stick = new VirtualStick(Radius);
            stick.Drag(new NVector2(800f, 200f));

            Assert.That(stick.Deflection, Is.EqualTo(NVector2.Zero));
        }

        [Test]
        public void TheLeftHalfOfTheScreen_IsTheMovementStick()
        {
            Assert.That(TouchControls.IsMoveSide(10f, 1000f), Is.True);
            Assert.That(TouchControls.IsMoveSide(499f, 1000f), Is.True);
            Assert.That(TouchControls.IsMoveSide(501f, 1000f), Is.False);
            Assert.That(TouchControls.IsMoveSide(990f, 1000f), Is.False);
        }

        /// <summary>
        /// The reach is a fraction of the screen's shorter side, so a thumb travels the same proportion of the
        /// screen on a phone and on a tablet rather than a fixed number of pixels that suits neither.
        /// </summary>
        [Test]
        public void TheStickRadius_ScalesWithTheScreen()
        {
            var phone = TouchControls.RadiusFor(2340f, 1080f);
            var tablet = TouchControls.RadiusFor(2732f, 2048f);

            Assert.That(phone, Is.EqualTo(1080f * TouchControls.StickRadiusFraction).Within(0.01f),
                "the shorter side is what a thumb has to cross");
            Assert.That(tablet, Is.GreaterThan(phone));
            Assert.That(TouchControls.RadiusFor(0f, 0f), Is.GreaterThan(0f), "never a radius of nothing");
        }

        // ---- What the right thumb means ----

        [Test]
        public void ARightThumbThatIsNotDown_DoesNotFire()
        {
            var firing = TouchControls.ResolveAim(NVector2.Zero, false, GameTuning.GamepadAimDeadZone,
                out var direction);

            Assert.That(firing, Is.False);
            Assert.That(direction, Is.EqualTo(NVector2.Zero));
        }

        /// <summary>
        /// Touch has no separate trigger, so holding the thumb still is how a player shoots straight ahead,
        /// which is what the gamepad's fire-forward trigger is for.
        /// </summary>
        [Test]
        public void ARightThumbHeldStill_FiresForward()
        {
            var firing = TouchControls.ResolveAim(NVector2.Zero, true, GameTuning.GamepadAimDeadZone,
                out var direction);

            Assert.That(firing, Is.True);
            Assert.That(direction.X, Is.EqualTo(1f).Within(0.001f));
            Assert.That(direction.Y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ARightThumbInsideTheDeadZone_StillFiresForward()
        {
            var nudge = new NVector2(0f, GameTuning.GamepadAimDeadZone * 0.5f);

            var firing = TouchControls.ResolveAim(nudge, true, GameTuning.GamepadAimDeadZone, out var direction);

            Assert.That(firing, Is.True, "a thumb that has barely moved is still a held trigger");
            Assert.That(direction.X, Is.EqualTo(1f).Within(0.001f), "and the noise is not taken as an aim");
        }

        [Test]
        public void ARightThumbPushedPastTheDeadZone_AimsAlongThePush()
        {
            var push = new NVector2(0f, 0.9f);

            var firing = TouchControls.ResolveAim(push, true, GameTuning.GamepadAimDeadZone, out var direction);

            Assert.That(firing, Is.True);
            Assert.That(direction.Y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(direction.X, Is.EqualTo(0f).Within(0.001f));
        }

        /// <summary>
        /// The aim goes through the same rule the gamepad uses, so the firing arc and the dead zone are the
        /// game's single answer rather than a second one written for touch (GDD "Controls", firing arc).
        /// </summary>
        [Test]
        public void TouchAndGamepad_ResolveTheSameWay()
        {
            var push = new NVector2(-0.8f, 0.6f);

            var byTouch = TouchControls.ResolveAim(push, true, GameTuning.GamepadAimDeadZone, out var touchAim);
            var byPad = FireInput.ResolveGamepad(push, true, GameTuning.GamepadAimDeadZone, out var padAim);

            Assert.That(byTouch, Is.EqualTo(byPad));
            Assert.That(touchAim, Is.EqualTo(padAim));
        }
    }
}
