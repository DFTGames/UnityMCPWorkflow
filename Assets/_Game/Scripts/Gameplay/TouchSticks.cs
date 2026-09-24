using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;
using YASS.Core;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace YASS.Gameplay
{
    /// <summary>
    /// The two on-screen sticks (GDD "Controls", Touch): the left thumb moves, the right aims and fires while
    /// held. Each stick floats, centring wherever its thumb lands.
    /// </summary>
    /// <remarks>
    /// The rules are <see cref="VirtualStick"/> and <see cref="TouchControls"/> in Core; this only reads the
    /// screen and keeps track of which finger owns which stick. A finger keeps the stick it started on until
    /// it is lifted, even if it slides across the middle of the screen, because a thumb that drifts should not
    /// suddenly start steering with the aim.
    ///
    /// **Enhanced touch, reconciled against state rather than phases.** Each frame this asks which fingers
    /// are on the glass, drives those, and lets go of any stick whose finger is not among them. Reading
    /// phases off the raw <c>Touchscreen</c> was the original mistake and it broke the game twice over: the
    /// device overwrites a finished touch the moment another finger lands (its own source warns about exactly
    /// this), so a lift and a re-plant between two frames lost the end of the first touch and left a stick
    /// held for ever with the ship flying into the wall; and touch is sampled faster than the game runs, so a
    /// thumb that presses and slides arrives as one batch whose last phase is <c>Moved</c>, and that finger
    /// never started at all. Enhanced touch is the tier the package points at for precisely these two cases,
    /// and reading who is present cannot miss an event because it reads no events. It also heals itself,
    /// after a rebuild or a spell in the background.
    ///
    /// Anything let go is let go **before** anyone new is taken on, so a finger replaced in the same frame
    /// takes the stick that same frame rather than a frame later.
    ///
    /// A thumb is taken up at its own start position rather than where it is now, so one that pressed and
    /// slid inside a single input update still centres its stick where it landed. That only holds for a touch
    /// seen from its first frame: one adopted later, because its half was busy or because a pause or a rebuild
    /// dropped ownership under it, is taken up where it is now, since its landing point is by then stale and
    /// the gap between the two would read as a push nobody made.
    ///
    /// **A stick is owned by a touch id, never by a finger index.** Enhanced touch hands the freed finger slot
    /// straight to the next touch, so a thumb lifted and replanted elsewhere arrives on the same finger: the
    /// stick then looked still held, dragged to the new spot instead of recentring, and kept steering from the
    /// old centre. The touch id belongs to the one touch and is what tells the two apart.
    ///
    /// Present on desktop too: a laptop with a touchscreen is an ordinary thing, and nothing here interferes
    /// with a mouse or a pad, since a stick that no finger is on reports nothing at all.
    /// </remarks>
    public sealed class TouchSticks : MonoBehaviour
    {
        /// <summary>No touch owns this stick. Touch ids start at 1, so this cannot collide with one.</summary>
        const int Nobody = -1;

        VirtualStick _move;
        VirtualStick _aim;
        int _moveTouch = Nobody;
        int _aimTouch = Nobody;

        /// <summary>Who was on the glass last frame. A touchscreen reports at most ten fingers.</summary>
        readonly int[] _seen = new int[10];
        int _seenCount;

        int _screenWidth;
        int _screenHeight;

        /// <summary>Ownership was dropped under the thumbs this frame, so every one of them is up for adoption.</summary>
        bool _rebuilt;

        /// <summary>The movement stick, for whatever draws it.</summary>
        public VirtualStick Move => _move;

        /// <summary>The aiming stick, for whatever draws it.</summary>
        public VirtualStick Aim => _aim;

        /// <summary>True while either thumb is down, which is how the game knows it is being played by touch.</summary>
        public bool InUse => _move.IsHeld || _aim.IsHeld;

        void Awake() => Rebuild();

        void OnEnable()
        {
            // Reference counted by the package, so this is safe even if something else wants it too.
            EnhancedTouchSupport.Enable();
            Rebuild();
        }

        void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            LetGo();
        }

        void Update()
        {
            // No touchscreen to read, so nothing can still be on it. Letting go rather than leaving the sticks
            // as they were: a device removed mid-touch (a digitiser disconnecting, a reset, another component
            // switching enhanced touch off) would otherwise leave a stick held at its last deflection for ever,
            // steering the ship into the wall and locking the keyboard and pad out behind it.
            if (!EnhancedTouchSupport.enabled || Touchscreen.current == null)
            {
                LetGo();
                _seenCount = 0;
                return;
            }

            var touches = Touch.activeTouches;

            // A menu covering the level is the one time the game is frozen (MenuRouter owns timeScale), and a
            // thumb resting on Resume must not still be steering when it starts again. Who is on the glass is
            // still recorded, so a thumb that rested through the pause is taken up where it now is rather than
            // where it first landed, which would resume at whatever deflection the gap between the two makes.
            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                LetGo();
                Remember(touches);
                return;
            }

            // Measured against the screen itself, not against the reach it produces: the reach comes from the
            // shorter side, so a 90 degree rotation leaves it identical while every touch coordinate and the
            // halfway line have moved. Checking the radius looked right and could never fire on a rotation.
            if (_screenWidth != Screen.width || _screenHeight != Screen.height) Rebuild();

            // First, let go of a stick whose finger has left, however its end was reported or whether it was
            // reported at all. Before taking anyone new on, so that a finger replaced within one frame takes
            // the stick in that same frame instead of the screen going dead for one.
            var movePresent = false;
            var aimPresent = false;
            foreach (var touch in touches)
            {
                if (touch.touchId == _moveTouch) movePresent = true;
                else if (touch.touchId == _aimTouch) aimPresent = true;
            }

            if (!movePresent) ReleaseMove();
            if (!aimPresent) ReleaseAim();

            foreach (var touch in touches)
            {
                var id = touch.touchId;
                var at = touch.screenPosition;

                if (id == _moveTouch)
                {
                    _move.Drag(at.ToNumerics());
                    continue;
                }

                if (id == _aimTouch)
                {
                    _aim.Drag(at.ToNumerics());
                    continue;
                }

                var landed = touch.startScreenPosition;

                // Which half this thumb belongs to is settled where it landed and nowhere else, for its whole
                // life, so one that came down on a busy half and then wandered across the middle is ignored
                // rather than handed the other stick (which started the ship firing with nothing near the aim
                // side). The exception is a rebuild: a rotation moves the halfway line and every coordinate at
                // once, so a landing point from the old orientation says nothing about the new one.
                var sideAt = _rebuilt ? at : landed;

                // Where to centre it. A thumb seen from its first frame is taken where it landed, so one that
                // pressed and slid inside a single input update still centres where it went down. One adopted
                // later, after a pause or a rebuild let go of it, is taken where it is now: its landing point
                // is by then stale, and the gap between the two would read as a push nobody made.
                var pressAt = _rebuilt || WasSeen(id) ? at : landed;

                Touching(id, sideAt, pressAt);

                // Only once it has a stick is it driven to where it actually is, which is what lets a thumb
                // that pressed and slid in one update steer on its very first frame.
                if (id == _moveTouch) _move.Drag(at.ToNumerics());
                else if (id == _aimTouch) _aim.Drag(at.ToNumerics());
            }

            Remember(touches);
            _rebuilt = false;
        }

        /// <summary>
        /// Records who is on the glass this frame, so the next one can tell a thumb that has just landed from
        /// one that has been down a while. Fixed buffer: a touchscreen reports at most ten fingers, and this
        /// runs every frame, so it must not allocate.
        /// </summary>
        void Remember(ReadOnlyArray<Touch> touches)
        {
            _seenCount = 0;
            foreach (var touch in touches)
            {
                if (_seenCount == _seen.Length) return;
                _seen[_seenCount++] = touch.touchId;
            }
        }

        bool WasSeen(int touchId)
        {
            for (var i = 0; i < _seenCount; i++)
                if (_seen[i] == touchId) return true;

            return false;
        }

        /// <summary>What the two sticks mean, as the rest of the game understands input.</summary>
        public PlayerCommand ReadCommand()
        {
            var firing = TouchControls.ResolveAim(_aim.Deflection, _aim.IsHeld, GameTuning.GamepadAimDeadZone,
                out var direction);

            return new PlayerCommand(_move.Deflection, firing, direction);
        }

        /// <summary>
        /// A finger is on the glass at this point: it takes the stick for its side if that stick is free, and
        /// otherwise drives the one it already owns. Test seam, and what <see cref="Update"/> itself calls.
        /// </summary>
        internal void Touching(int touch, Vector2 at) => Touching(touch, at, at);

        /// <summary>
        /// As above, but with the half decided from one point and the stick centred on another. They are the
        /// same point for a thumb seen from the moment it landed, and differ only for one adopted later.
        /// </summary>
        void Touching(int touch, Vector2 sideAt, Vector2 pressAt)
        {
            if (touch == _moveTouch)
            {
                _move.Drag(pressAt.ToNumerics());
                return;
            }

            if (touch == _aimTouch)
            {
                _aim.Drag(pressAt.ToNumerics());
                return;
            }

            if (TouchControls.IsMoveSide(sideAt.x, Screen.width))
            {
                if (_moveTouch != Nobody) return; // one thumb per stick; a second finger is ignored

                _moveTouch = touch;
                _move.Press(pressAt.ToNumerics());
            }
            else
            {
                if (_aimTouch != Nobody) return;

                _aimTouch = touch;
                _aim.Press(pressAt.ToNumerics());
            }
        }

        /// <summary>Test seam: a finger goes down.</summary>
        internal void Begin(int touch, Vector2 at) => Touching(touch, at);

        /// <summary>Test seam: a finger moves.</summary>
        internal void Drag(int touch, Vector2 to) => Touching(touch, to);

        /// <summary>
        /// Test seam: what a rotation or a resized window does, which a test cannot ask the screen for.
        /// </summary>
        internal void RebuildNow() => Rebuild();

        /// <summary>Test seam: a finger lifts.</summary>
        internal void End(int touch)
        {
            if (touch == _moveTouch) ReleaseMove();
            else if (touch == _aimTouch) ReleaseAim();
        }

        void ReleaseMove()
        {
            if (_moveTouch == Nobody && !_move.IsHeld) return;

            _moveTouch = Nobody;
            _move.Release();
        }

        void ReleaseAim()
        {
            if (_aimTouch == Nobody && !_aim.IsHeld) return;

            _aimTouch = Nobody;
            _aim.Release();
        }

        void LetGo()
        {
            ReleaseMove();
            ReleaseAim();
        }

        void Rebuild()
        {
            // Rebuilt rather than resized, because the radius is what a stick is made with.
            _move = new VirtualStick(CurrentRadius());
            _aim = new VirtualStick(CurrentRadius());
            _moveTouch = Nobody;
            _aimTouch = Nobody;
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _rebuilt = true;
        }

        static float CurrentRadius() => TouchControls.RadiusFor(Screen.width, Screen.height);
    }
}
