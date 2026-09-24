using NUnit.Framework;
using UnityEngine;
using YASS.Core;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Which finger drives which stick (GDD "Controls", Touch). The arithmetic is covered by
    /// <c>VirtualStickTests</c> in Core; what is here is the part that needs a screen to have a width, and
    /// the bookkeeping that goes wrong when two thumbs are on the glass at once.
    /// </summary>
    /// <remarks>
    /// Driven through the seams rather than by a real touchscreen: a device cannot be attached to a test run,
    /// and the thing worth proving is what the game does with the fingers, not that Unity reports them.
    /// </remarks>
    public class TouchSticksTests
    {
        TouchSticks _sticks;

        static float Left => Screen.width * 0.25f;
        static float Right => Screen.width * 0.75f;
        static float Middle => Screen.height * 0.5f;

        [SetUp]
        public void MakeTheSticks()
        {
            _sticks = new GameObject(nameof(TouchSticks)).AddComponent<TouchSticks>();
        }

        [TearDown]
        public void PutThemAway()
        {
            if (_sticks != null) Object.DestroyImmediate(_sticks.gameObject);
        }

        [Test]
        public void WithNoThumbsDown_NothingIsAsked()
        {
            Assert.That(_sticks.InUse, Is.False);

            var command = _sticks.ReadCommand();
            Assert.That(command.Move, Is.EqualTo(System.Numerics.Vector2.Zero));
            Assert.That(command.Fire, Is.False);
        }

        [Test]
        public void AThumbOnTheLeft_DrivesTheMovementStick()
        {
            _sticks.Begin(1, new Vector2(Left, Middle));
            _sticks.Drag(1, new Vector2(Left + _sticks.Move.Radius, Middle));

            Assert.That(_sticks.Move.IsHeld, Is.True);
            Assert.That(_sticks.Aim.IsHeld, Is.False, "the aim must not follow the moving thumb");
            Assert.That(_sticks.ReadCommand().Move.X, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void AThumbOnTheRight_FiresWhileItIsDown()
        {
            _sticks.Begin(1, new Vector2(Right, Middle));

            var command = _sticks.ReadCommand();
            Assert.That(command.Fire, Is.True, "a held thumb is the trigger");
            Assert.That(command.AimDirection.X, Is.EqualTo(1f).Within(0.001f), "and it fires forward until it is pushed");
        }

        [Test]
        public void BothThumbsAtOnce_DriveTheirOwnSticks()
        {
            _sticks.Begin(1, new Vector2(Left, Middle));
            _sticks.Begin(2, new Vector2(Right, Middle));

            _sticks.Drag(1, new Vector2(Left, Middle + _sticks.Move.Radius));   // move up
            _sticks.Drag(2, new Vector2(Right, Middle - _sticks.Aim.Radius));   // aim down

            var command = _sticks.ReadCommand();
            Assert.That(command.Move.Y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(command.AimDirection.Y, Is.EqualTo(-1f).Within(0.001f));
        }

        /// <summary>
        /// A thumb that drifts across the middle of the screen keeps the stick it started on. Handing it over
        /// would mean the ship suddenly steering with the aim, in the middle of a fight, because a thumb
        /// wandered a few pixels.
        /// </summary>
        [Test]
        public void AThumbThatSlidesAcrossTheMiddle_KeepsItsOwnStick()
        {
            _sticks.Begin(1, new Vector2(Left, Middle));
            _sticks.Drag(1, new Vector2(Right, Middle));

            Assert.That(_sticks.Move.IsHeld, Is.True);
            Assert.That(_sticks.Aim.IsHeld, Is.False);
        }

        [Test]
        public void LiftingAThumb_StopsThatStickOnly()
        {
            _sticks.Begin(1, new Vector2(Left, Middle));
            _sticks.Begin(2, new Vector2(Right, Middle));

            _sticks.End(1);

            Assert.That(_sticks.Move.IsHeld, Is.False);
            Assert.That(_sticks.Aim.IsHeld, Is.True);
            Assert.That(_sticks.ReadCommand().Fire, Is.True, "the right thumb is still firing");
        }

        /// <summary>A second finger on the same half is ignored rather than stealing the stick mid-flight.</summary>
        [Test]
        public void ASecondFingerOnTheSameSide_DoesNotStealTheStick()
        {
            _sticks.Begin(1, new Vector2(Left, Middle));
            var origin = _sticks.Move.Origin;

            _sticks.Begin(2, new Vector2(Left + 50f, Middle + 50f));

            Assert.That(_sticks.Move.Origin, Is.EqualTo(origin), "the stick stayed where the first thumb put it");

            _sticks.End(2);
            Assert.That(_sticks.Move.IsHeld, Is.True, "and lifting the intruder does not end the first thumb");
        }

        [Test]
        public void BeingSwitchedOff_LetsGoOfEverything()
        {
            _sticks.Begin(1, new Vector2(Left, Middle));
            _sticks.Begin(2, new Vector2(Right, Middle));

            _sticks.gameObject.SetActive(false);

            Assert.That(_sticks.InUse, Is.False, "a stick left held would steer for ever");
            Assert.That(_sticks.ReadCommand().Fire, Is.False);
        }

        [Test]
        public void TheSticks_ReachTheSameFractionOfAnyScreen()
        {
            Assert.That(_sticks.Move.Radius,
                Is.EqualTo(TouchControls.RadiusFor(Screen.width, Screen.height)).Within(0.01f));
        }
    }
}
