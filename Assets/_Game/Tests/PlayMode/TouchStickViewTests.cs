using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using YASS.Core;
using YASS.Gameplay;
using YASS.UI;

// Both UnityEngine and the Input System define one; the device speaks the Input System's.
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace YASS.Tests.Gameplay
{
    /// <summary>
    /// What the player actually sees of the touch sticks (GDD "Controls", Touch). Driven through the real
    /// scene and the real components, because the interesting part is the arithmetic between screen pixels,
    /// where a thumb is, and canvas units, where a rectangle is: reading the code proves nothing about that.
    /// </summary>
    public class TouchStickViewTests : LevelSceneFixture
    {
        TouchSticks _sticks;
        TouchStickView _view;
        RectTransform _ring;
        RectTransform _knob;

        Touchscreen _screen;

        [UnitySetUp]
        public IEnumerator FindTheSticks()
        {
            _sticks = Object.FindAnyObjectByType<TouchSticks>(FindObjectsInactive.Include);
            _view = Object.FindAnyObjectByType<TouchStickView>(FindObjectsInactive.Include);

            Assert.That(_sticks, Is.Not.Null, "the level has no touch sticks");
            Assert.That(_view, Is.Not.Null, "the level does not draw the touch sticks");

            _ring = Find("MoveRing");
            _knob = Find("MoveKnob");

            // A real device, because the sticks reconcile against who is on the glass every frame: driving
            // the seams instead would be undone by the very next Update.
            _screen = InputSystem.AddDevice<Touchscreen>();
            EnhancedTouchSupport.Enable();
            yield return null;
        }

        [TearDown]
        public void RemoveTheDevice()
        {
            EnhancedTouchSupport.Disable();
            if (_screen != null && _screen.added) InputSystem.RemoveDevice(_screen);
        }

        /// <summary>
        /// A finger on the glass, through the device rather than the seams. Queuing alone is not enough: the
        /// event sits until the input system is pumped, and the component reads state, not events.
        /// </summary>
        void Touch(int id, TouchPhase phase, Vector2 at)
        {
            InputSystem.QueueStateEvent(_screen, new TouchState { touchId = id, phase = phase, position = at });
            InputSystem.Update();
        }

        /// <summary>
        /// Long enough for the drawing to have happened. A plain <c>yield return null</c> resumes after every
        /// Update and before any LateUpdate, so the sticks have read the glass but the view has not yet drawn
        /// what they read: looking then catches the frame before the one under test.
        /// </summary>
        static IEnumerator Drawn()
        {
            yield return null;
            yield return null;
        }

        static RectTransform Find(string name)
        {
            foreach (var rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rect.name == name) return rect;

            Assert.Fail("No " + name + " in the level.");
            return null;
        }

        [UnityTest]
        public IEnumerator WithNoThumbDown_NothingIsDrawn()
        {
            yield return null;

            Assert.That(_ring.gameObject.activeSelf, Is.False, "a stick nobody is touching is not drawn");
            Assert.That(_knob.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator AThumbDown_DrawsTheStickWhereItLanded()
        {
            var at = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
            Touch(1, TouchPhase.Began, at);
            yield return Drawn();

            Assert.That(_ring.gameObject.activeSelf, Is.True, "the stick appears under the thumb");

            // Screen space, because that is what a thumb touches and what the stick was told.
            var drawn = RectTransformUtility.WorldToScreenPoint(null, _ring.position);
            Assert.That(drawn.x, Is.EqualTo(at.x).Within(2f), "the ring is not where the thumb landed");
            Assert.That(drawn.y, Is.EqualTo(at.y).Within(2f));
        }

        /// <summary>
        /// The knob is what tells the player how hard they are pushing, so it has to move off the centre and
        /// stay inside the ring: this is the arithmetic between pixels and canvas units.
        /// </summary>
        [UnityTest]
        public IEnumerator PushingTheThumb_MovesTheKnobButNotTheRing()
        {
            var at = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
            Touch(1, TouchPhase.Began, at);
            yield return Drawn();

            var ringBefore = _ring.position;

            Touch(1, TouchPhase.Moved, at + new Vector2(_sticks.Move.Radius, 0f)); // full deflection, right
            yield return Drawn();

            Assert.That(_ring.position, Is.EqualTo(ringBefore), "the ring stays where the thumb first landed");

            var ring = RectTransformUtility.WorldToScreenPoint(null, _ring.position);
            var knob = RectTransformUtility.WorldToScreenPoint(null, _knob.position);

            Assert.That(knob.x, Is.GreaterThan(ring.x), "the knob follows the push");
            Assert.That(knob.y, Is.EqualTo(ring.y).Within(2f), "and only along it");

            // At full deflection the knob sits on the ring, which is drawn at a fraction of the reach.
            var expected = _sticks.Move.Radius * TouchControls.BaseRadiusFraction;
            Assert.That(knob.x - ring.x, Is.EqualTo(expected).Within(2f),
                "the knob should ride the ring rather than the thumb");
        }

        /// <summary>
        /// The aim stick is drawn too, and by its own parts. Every other test here touches the left half and
        /// looks at MoveRing, so swapping the aim and move fields over in the scene, or leaving either aim
        /// field unassigned, passed the whole suite.
        /// </summary>
        [UnityTest]
        public IEnumerator AThumbOnTheRight_DrawsTheAimStick()
        {
            var aimRing = Find("AimRing");
            var at = new Vector2(Screen.width * 0.8f, Screen.height * 0.4f);

            Touch(1, TouchPhase.Began, at);
            yield return Drawn();

            Assert.That(aimRing.gameObject.activeSelf, Is.True, "the aim stick appears under the right thumb");
            Assert.That(_ring.gameObject.activeSelf, Is.False, "and the move stick is left alone");

            var drawn = RectTransformUtility.WorldToScreenPoint(null, aimRing.position);
            Assert.That(drawn.x, Is.EqualTo(at.x).Within(2f), "the aim ring is not where the thumb landed");
            Assert.That(drawn.y, Is.EqualTo(at.y).Within(2f));
        }

        [UnityTest]
        public IEnumerator LiftingTheThumb_TakesTheStickAway()
        {
            var at = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
            Touch(1, TouchPhase.Began, at);
            yield return Drawn();
            Assert.That(_ring.gameObject.activeSelf, Is.True);

            Touch(1, TouchPhase.Ended, at);
            yield return Drawn();

            Assert.That(_ring.gameObject.activeSelf, Is.False, "the stick goes when the thumb does");
            Assert.That(_knob.gameObject.activeSelf, Is.False);
        }

        /// <summary>
        /// Drawn smaller than the thumb's reach, on purpose: the sticks sit on top of the thing the player is
        /// trying to watch (GDD "Controls": there to say where the stick centred, not to cover the fight).
        /// </summary>
        [UnityTest]
        public IEnumerator TheStick_IsDrawnSmallerThanItsReach()
        {
            Touch(1, TouchPhase.Began, new Vector2(Screen.width * 0.2f, Screen.height * 0.4f));
            yield return Drawn();

            var reach = _sticks.Move.Radius * 2f;
            var ringAcross = _ring.rect.width * _ring.lossyScale.x;
            var knobAcross = _knob.rect.width * _knob.lossyScale.x;

            Assert.That(ringAcross, Is.LessThan(reach), "the ring is not a fence around the thumb");
            Assert.That(knobAcross, Is.LessThan(ringAcross), "and the knob is smaller still");
            Assert.That(ringAcross, Is.GreaterThan(0f));
        }
    }
}
