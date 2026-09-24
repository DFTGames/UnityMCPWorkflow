using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.TestTools;
using YASS.Gameplay;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// The sticks against a real touchscreen device, rather than through their test seams.
    /// </summary>
    /// <remarks>
    /// This is where the bugs were. Everything else drives <c>Begin</c>/<c>Drag</c>/<c>End</c> directly, which
    /// proves the arithmetic and nothing about the lines that talk to the device: the first version polled
    /// <c>Touchscreen.touches</c> and its phases, which loses the end of a touch when another finger lands in
    /// the same frame, and loses the start of one that presses and slides inside a single input update. Both
    /// left a stick stuck or dead, and both passed every seam-driven test.
    ///
    /// Built on <see cref="InputTestFixture"/>, which is the package's own harness: it needs
    /// <c>com.unity.inputsystem</c> listed under <c>testables</c> in the manifest and a reference to
    /// <c>Unity.InputSystem.TestFramework</c>. Without that, a hand-rolled attempt at feeding the device
    /// silently produces no touches at all, which is easily misread as the component being wrong.
    /// </remarks>
    public class TouchDeviceTests : InputTestFixture
    {
        TouchSticks _sticks;
        EventSystem[] _silenced;

        static float Left => Screen.width * 0.25f;
        static float Right => Screen.width * 0.75f;
        static float Middle => Screen.height * 0.5f;

        public override void Setup()
        {
            base.Setup();

            // An EventSystem left in the scene by an earlier fixture keeps polling input, and the fixture has
            // just swapped the input system out from under it: uGUI then logs an error about its cached value
            // going stale, which fails the test. These tests drive a bare component with no UI at all, so the
            // EventSystem has nothing to do here. Silencing it removes the cause; LogAssert would only hide it,
            // along with any real error this component logged.
            // FindObjectsByType returns only enabled behaviours, so everything recorded here was on and has
            // to go back on. A blanket re-enable in teardown would switch on one a later fixture had off.
            _silenced = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var system in _silenced) system.enabled = false;

            InputSystem.AddDevice<Touchscreen>();
            EnhancedTouchSupport.Enable();
            _sticks = new GameObject(nameof(TouchSticks)).AddComponent<TouchSticks>();
        }

        public override void TearDown()
        {
            if (_sticks != null) Object.DestroyImmediate(_sticks.gameObject);

            if (_silenced != null) // a Setup that threw before this was filled must not fail twice
                foreach (var system in _silenced)
                    if (system != null) system.enabled = true;

            EnhancedTouchSupport.Disable();
            base.TearDown();
        }

        /// <summary>Lets the component's Update see what the fixture has just queued.</summary>
        static IEnumerator Settle()
        {
            yield return null;
        }

        [UnityTest]
        public IEnumerator AThumbOnTheGlass_DrivesItsStick()
        {
            BeginTouch(1, new Vector2(Left, Middle));
            yield return Settle();

            Assert.That(_sticks.Move.IsHeld, Is.True, "a finger on the left half should take the move stick");
        }

        /// <summary>
        /// The failure that made the first version unusable on a phone: touch is sampled faster than the game
        /// runs, so a thumb that presses and slides arrives as one batch whose last phase is Moved. Reading
        /// phases, the press was never seen and that finger stayed dead for its whole life.
        /// </summary>
        [UnityTest]
        public IEnumerator AThumbThatPressesAndSlidesInOneFrame_StillDrivesItsStick()
        {
            var at = new Vector2(Left, Middle);
            BeginTouch(1, at);
            MoveTouch(1, at + new Vector2(0f, 40f));
            MoveTouch(1, at + new Vector2(0f, 80f));
            yield return Settle();

            Assert.That(_sticks.Move.IsHeld, Is.True, "the press was lost, so the thumb never steered");
            Assert.That(_sticks.ReadCommand().Move.Y, Is.GreaterThan(0f), "and it should be pushing upwards");
        }

        /// <summary>
        /// The other one: the device overwrites a finished touch as soon as another finger lands, so a lift
        /// and a re-plant between two frames never reported the lift. The stick stayed held at its last
        /// deflection for ever and the ship flew into the edge of the screen.
        /// </summary>
        [UnityTest]
        public IEnumerator LiftingAndPressingAgainInOneFrame_DoesNotStickTheStick()
        {
            var first = new Vector2(Left, Middle);
            BeginTouch(1, first);
            yield return Settle();

            MoveTouch(1, first + new Vector2(0f, _sticks.Move.Radius)); // pushed hard
            yield return Settle();
            Assert.That(_sticks.ReadCommand().Move.Y, Is.GreaterThan(0.9f));

            var second = new Vector2(Left + 60f, Middle - 60f);
            EndTouch(1, first);
            BeginTouch(2, second);
            yield return Settle();

            Assert.That(_sticks.Move.IsHeld, Is.True, "the new thumb should have the stick");
            Assert.That(_sticks.Move.Origin.Y, Is.EqualTo(second.y).Within(2f),
                "and it should have recentred where that thumb landed, not kept the old push");
        }

        [UnityTest]
        public IEnumerator LiftingEveryFinger_LetsGoOfEverything()
        {
            BeginTouch(1, new Vector2(Left, Middle));
            BeginTouch(2, new Vector2(Right, Middle));
            yield return Settle();

            Assert.That(_sticks.InUse, Is.True);

            EndTouch(1, new Vector2(Left, Middle));
            EndTouch(2, new Vector2(Right, Middle));
            yield return Settle();

            Assert.That(_sticks.InUse, Is.False, "a stick left held would steer for ever");
        }

        /// <summary>
        /// Touches that vanish without an end, as they do when the app has been in the background: nothing is
        /// on the glass any more, so nothing should still be held. Deliberately not a lift: an ordinary
        /// <c>EndTouch</c> here proved nothing this file does not already prove, and would still pass against
        /// the phase-reading version this component exists to avoid.
        /// </summary>
        [UnityTest]
        public IEnumerator TouchesThatDisappear_DoNotLeaveAStickHeld()
        {
            BeginTouch(1, new Vector2(Left, Middle));
            yield return Settle();
            Assert.That(_sticks.Move.IsHeld, Is.True);

            InputSystem.ResetDevice(Touchscreen.current); // gone, with no end phase for anyone to read
            yield return Settle();

            Assert.That(_sticks.Move.IsHeld, Is.False, "a touch can stop being reported without ending");
        }

        /// <summary>
        /// The touchscreen itself going away, which a digitiser disconnecting or a device reset does. There is
        /// then nothing to reconcile against, and the tempting reading of that is to leave the sticks alone:
        /// that left one held at its last deflection for ever, steering the ship with nobody touching it and
        /// locking the keyboard and pad out behind it, because InUse stayed true.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTouchscreenGoingAway_DoesNotLeaveAStickHeld()
        {
            BeginTouch(1, new Vector2(Left, Middle));
            yield return Settle();
            Assert.That(_sticks.Move.IsHeld, Is.True);

            InputSystem.RemoveDevice(Touchscreen.current);
            yield return Settle();

            Assert.That(_sticks.InUse, Is.False, "a stick held with no touchscreen steers for ever");
        }

        /// <summary>
        /// A rotation or a resized window rebuilds the sticks, and the thumbs still down have to be taken up
        /// again where they now are. Taking them at the point they originally landed read the gap between the
        /// two as a push: on a real rotation every coordinate moves at once, so the ship shot off to a corner.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterARebuild_AThumbStillDown_RecentresWhereItIs()
        {
            var landed = new Vector2(Left, Middle);
            BeginTouch(1, landed);
            yield return Settle();

            var moved = landed + new Vector2(0f, _sticks.Move.Radius);
            MoveTouch(1, moved);
            yield return Settle();

            _sticks.RebuildNow();
            yield return Settle();

            Assert.That(_sticks.Move.IsHeld, Is.True, "the thumb is still on the glass");
            Assert.That(_sticks.Move.Origin.Y, Is.EqualTo(moved.y).Within(2f),
                "it should have been taken up where it is, not where it landed before the rebuild");
            Assert.That(_sticks.ReadCommand().Move.Length(), Is.LessThan(0.1f),
                "a motionless thumb is not pushing, however far it is from where it started");
        }

        /// <summary>
        /// The half a thumb lands in is the stick it drives, for its whole life. A finger that lands on a busy
        /// half and then wanders across the middle must not be handed the other stick: that started the ship
        /// firing with no thumb anywhere near the aim side.
        /// </summary>
        [UnityTest]
        public IEnumerator AFingerThatLandedOnABusyHalf_DoesNotTakeTheOtherStick()
        {
            BeginTouch(1, new Vector2(Left, Middle)); // takes the move stick
            yield return Settle();

            BeginTouch(2, new Vector2(Left + 20f, Middle)); // same half, ignored: one thumb per stick
            yield return Settle();
            Assert.That(_sticks.Aim.IsHeld, Is.False);

            MoveTouch(2, new Vector2(Right, Middle)); // wanders across the middle
            yield return Settle();

            Assert.That(_sticks.Aim.IsHeld, Is.False,
                "the side is decided where the thumb landed, not where it has drifted to");
            Assert.That(_sticks.ReadCommand().Fire, Is.False, "and so the ship is not firing");
        }

        /// <summary>A menu covering the level freezes the game, and a thumb resting on Resume must not steer.</summary>
        [UnityTest]
        public IEnumerator WhileTheGameIsFrozen_TheSticksLetGo()
        {
            BeginTouch(1, new Vector2(Left, Middle));
            yield return Settle();
            Assert.That(_sticks.Move.IsHeld, Is.True);

            var was = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;

                Assert.That(_sticks.InUse, Is.False, "a paused game is not being steered");

                // Resting thumbs drift while a menu is up. Letting go and then taking the thumb up again at
                // the point it originally landed turned that drift into a push the moment play resumed.
                MoveTouch(1, new Vector2(Left, Middle + 200f));
                yield return null;

                Time.timeScale = was;
                yield return null;

                Assert.That(_sticks.ReadCommand().Move.Length(), Is.LessThan(0.1f),
                    "a thumb that only moved while paused is not steering when play resumes");
            }
            finally
            {
                Time.timeScale = was;
            }
        }
    }
}
