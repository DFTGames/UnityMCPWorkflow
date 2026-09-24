using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// One floating on-screen stick (GDD "Controls", Touch): it centres wherever the thumb lands and reports
    /// how far and in which direction the thumb has moved from there.
    /// </summary>
    /// <remarks>
    /// Floating rather than drawn in a fixed corner, because a thumb cannot find a fixed spot without looking
    /// and screens differ in size and hand. The centre is the first touch; everything after is measured from
    /// it. The rules live here, in pixels, so they can be tested without a device: the Unity layer only has to
    /// tell this where a finger went down, moved and came up.
    /// </remarks>
    public struct VirtualStick
    {
        /// <summary>Where the thumb has to reach for full deflection, in screen pixels.</summary>
        public readonly float Radius;

        Vector2 _origin;
        Vector2 _current;

        public VirtualStick(float radius)
        {
            Radius = radius > 0f ? radius : 1f;
            _origin = Vector2.Zero;
            _current = Vector2.Zero;
            IsHeld = false;
        }

        /// <summary>True while a thumb is down on this stick.</summary>
        public bool IsHeld { get; private set; }

        /// <summary>Where the stick centred itself, which is where the thumb first touched.</summary>
        public Vector2 Origin => _origin;

        /// <summary>Where the thumb is now.</summary>
        public Vector2 Thumb => _current;

        public void Press(Vector2 at)
        {
            _origin = at;
            _current = at;
            IsHeld = true;
        }

        public void Drag(Vector2 to)
        {
            if (IsHeld) _current = to;
        }

        public void Release()
        {
            IsHeld = false;
            _origin = Vector2.Zero;
            _current = Vector2.Zero;
        }

        /// <summary>
        /// How far the thumb has been pushed, from nothing at the centre to 1 at the radius and no further.
        /// Beyond the radius the stick stays at full deflection rather than reaching further, so a thumb that
        /// slides across the screen does not become a stronger input than one that moved a sensible distance.
        /// </summary>
        public Vector2 Deflection
        {
            get
            {
                if (!IsHeld) return Vector2.Zero;

                var offset = _current - _origin;
                var distance = offset.Length();
                if (distance <= 0.0001f) return Vector2.Zero;

                var scale = distance > Radius ? Radius : distance;
                return offset / distance * (scale / Radius);
            }
        }
    }

    /// <summary>
    /// Which stick a touch belongs to, and what the pair of them mean (GDD "Controls", Touch: left thumb
    /// moves, right thumb aims and fires while held).
    /// </summary>
    public static class TouchControls
    {
        /// <summary>
        /// How far the thumb travels for full deflection, as a fraction of the screen's shorter side. Set
        /// against the screen rather than in pixels so the reach is the same on a phone and on a tablet.
        /// </summary>
        public const float StickRadiusFraction = 0.12f;

        /// <summary>
        /// How big the ring under the thumb is drawn, against the stick's reach. Deliberately smaller than
        /// the reach: the ring is there to say where the stick centred, not to fence the thumb in, and a
        /// circle as wide as the thumb can travel covers a quarter of a phone screen during a fight.
        /// </summary>
        public const float BaseRadiusFraction = 0.55f;

        /// <summary>The knob, drawn small enough that a thumb does not hide the fact that it moved.</summary>
        public const float KnobRadiusFraction = 0.22f;

        /// <summary>
        /// Where the knob is drawn, relative to the stick's centre. It rides the base ring rather than the
        /// thumb, so it stays visible when the thumb has slid past the reach: the thumb is over the screen,
        /// and the knob is what tells the player how much deflection they are actually giving.
        /// </summary>
        public static Vector2 KnobOffset(Vector2 deflection, float radius) =>
            deflection * (radius * BaseRadiusFraction);

        /// <summary>The radius a stick should use on a screen of this size, in pixels.</summary>
        public static float RadiusFor(float screenWidth, float screenHeight)
        {
            var shorter = screenWidth < screenHeight ? screenWidth : screenHeight;
            var radius = shorter * StickRadiusFraction;
            return radius > 1f ? radius : 1f;
        }

        /// <summary>
        /// Whether a touch at this point belongs to the movement stick. The screen is split down the middle:
        /// the half a thumb lands in is the stick it drives, and it keeps that stick until it is lifted, even
        /// if it slides across the divide.
        /// </summary>
        public static bool IsMoveSide(float x, float screenWidth) => x < screenWidth * 0.5f;

        /// <summary>
        /// What the right thumb means. Holding it still fires straight ahead, which is touch's answer to the
        /// gamepad's fire-forward trigger; pushing it past the dead zone aims along the push. Below the dead
        /// zone the aim would be noise, so it is not used.
        /// </summary>
        public static bool ResolveAim(Vector2 deflection, bool held, float deadZone, out Vector2 direction)
        {
            if (!held)
            {
                // Zero rather than forward, matching what the other resolvers report when nothing is fired.
                direction = Vector2.Zero;
                return false;
            }

            // The thumb being down is the trigger: past the dead zone it aims, and below it fires forward.
            return FireInput.ResolveGamepad(deflection, true, deadZone, out direction);
        }
    }
}
