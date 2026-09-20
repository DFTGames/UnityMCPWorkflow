using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// How the player ship's visuals follow its aim (GDD "Controls", Firing arc and aim tilt): while firing, the
    /// ship points along the direction its shots take (see <see cref="FiringArc"/>); otherwise it eases back to level.
    /// </summary>
    public static class ShipTilt
    {
        /// <summary>Within this many degrees of the target, the tilt snaps to it (so an idle ship stops updating).</summary>
        public const float SnapDegrees = 0.01f;

        /// <summary>The tilt the ship should head for: the firing angle while firing, level otherwise.</summary>
        public static float TargetDegrees(bool firing, Vector2 aim, float arcDegrees)
        {
            var degrees = FiringArc.Degrees(aim, arcDegrees); // always evaluated, so a bad arc is rejected either way
            return firing ? degrees : 0f;
        }

        /// <summary>
        /// Eases <paramref name="current"/> towards <paramref name="target"/>, independent of frame rate, snapping
        /// once within <see cref="SnapDegrees"/>.
        /// </summary>
        public static float Step(float current, float target, float responsiveness, float deltaTime)
        {
            var next = Easing.Exponential(current, target, responsiveness, deltaTime);
            return Math.Abs(target - next) < SnapDegrees ? target : next;
        }
    }
}
