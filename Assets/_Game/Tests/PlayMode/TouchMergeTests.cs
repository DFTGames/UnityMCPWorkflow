using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.Gameplay;

using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// Touch alongside a keyboard or a pad, through the scene's own wiring (GDD "Controls": a device with both
    /// a touchscreen and a mouse keeps working with either, whichever the player reaches for).
    /// </summary>
    /// <remarks>
    /// This merge had no test of any kind, and it was wrong: the whole command was taken from touch the moment
    /// either stick was held, so a right thumb firing zeroed the keyboard's steering, a left thumb steering
    /// stopped the mouse firing, and a palm resting on the glass locked every other device out for as long as
    /// it stayed there. Each half of the command now comes from touch only while that stick is held.
    /// </remarks>
    public class TouchMergeTests : LevelSceneFixture
    {
        PlayerInputReader _reader;
        TouchSticks _sticks;
        Touchscreen _screen;
        Keyboard _keyboard;

        static float Left => Screen.width * 0.25f;
        static float Right => Screen.width * 0.75f;
        static float Middle => Screen.height * 0.5f;

        [UnitySetUp]
        public IEnumerator FindTheReader()
        {
            _reader = Object.FindAnyObjectByType<PlayerInputReader>(FindObjectsInactive.Include);
            _sticks = Object.FindAnyObjectByType<TouchSticks>(FindObjectsInactive.Include);

            Assert.That(_reader, Is.Not.Null, "the level has no input reader");
            Assert.That(_sticks, Is.Not.Null, "the level has no touch sticks");

            // The editor has real devices of its own, so these are the second keyboard and touchscreen in
            // the system. Whichever one is "current" is the one an action samples: if a leftover from an
            // earlier fixture is current instead of ours, Hold below drives one keyboard while the reader
            // reads another, and this suite has been seen to fail exactly that way without any code change.
            _screen = InputSystem.AddDevice<Touchscreen>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            EnhancedTouchSupport.Enable();
            yield return null;

        }

        [TearDown]
        public void RemoveTheDevices()
        {
            EnhancedTouchSupport.Disable();
            if (_screen != null && _screen.added) InputSystem.RemoveDevice(_screen);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        }

        void Touch(int id, TouchPhase phase, Vector2 at)
        {
            InputSystem.QueueStateEvent(_screen, new TouchState { touchId = id, phase = phase, position = at });
            InputSystem.Update();
        }

        void Hold(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        PlayerCommand Read() => _reader.ReadCommand(default, Camera.main);

        [UnityTest]
        public IEnumerator ARightThumbFiring_DoesNotStopTheKeyboardSteering()
        {
            Hold(Key.D);
            yield return null;
            Assert.That(Read().Move.X, Is.GreaterThan(0.5f), "the keyboard should be steering to begin with");

            Touch(1, TouchPhase.Began, new Vector2(Right, Middle)); // right thumb: the aim stick only
            yield return null;

            Assert.That(_sticks.Aim.IsHeld, Is.True, "the right half is the aim stick");
            Assert.That(_sticks.Move.IsHeld, Is.False, "and no thumb is on the move stick");
            Assert.That(Read().Move.X, Is.GreaterThan(0.5f),
                "a thumb on the aim stick must not zero the keyboard's steering");
        }

        [UnityTest]
        public IEnumerator ALeftThumbSteering_TakesTheMoveHalfFromTouch()
        {
            Hold(Key.D);
            yield return null;

            var at = new Vector2(Left, Middle);
            Touch(1, TouchPhase.Began, at);
            yield return null;
            Touch(1, TouchPhase.Moved, at - new Vector2(_sticks.Move.Radius, 0f)); // pushed hard left
            yield return null;

            Assert.That(_sticks.Move.IsHeld, Is.True);
            Assert.That(Read().Move.X, Is.LessThan(-0.5f),
                "the thumb owns the move half while it is down, so it beats the held key");
        }

        /// <summary>A stray contact used to kill every other device for as long as it rested there.</summary>
        [UnityTest]
        public IEnumerator AThumbThatIsNotOnAStick_LeavesEveryOtherDeviceAlone()
        {
            Touch(1, TouchPhase.Began, new Vector2(Left, Middle));  // takes the move stick
            Touch(2, TouchPhase.Began, new Vector2(Left + 20f, Middle)); // ignored: that stick is taken
            yield return null;

            Assert.That(_sticks.Aim.IsHeld, Is.False);
            Assert.That(Read().Fire, Is.False, "an ignored finger is not aiming, so nothing is firing");
        }
    }
}
