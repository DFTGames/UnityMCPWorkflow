using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// The player ship's forward firing arc (GDD "Controls", Firing arc and aim tilt). This is a side-scroller: the
    /// ship shoots within an arc around straight ahead, never behind itself. Aiming outside the arc clamps to its
    /// edge, and aiming behind is mirrored onto the forward half.
    /// </summary>
    public static class FiringArc
    {
        /// <summary>
        /// The angle shots travel along for an aim direction, in degrees (positive = nose up), clamped to
        /// <paramref name="arcDegrees"/> either way. Straight back gives 0; up-and-back gives a positive angle.
        /// </summary>
        public static float Degrees(Vector2 aim, float arcDegrees)
        {
            if (arcDegrees < 0f) throw new ArgumentOutOfRangeException(nameof(arcDegrees));
            if (!float.IsFinite(aim.X) || !float.IsFinite(aim.Y) || aim.LengthSquared() < 1e-8f) return 0f;

            var degrees = MathF.Atan2(aim.Y, MathF.Abs(aim.X)) * (180f / MathF.PI);
            return Math.Clamp(degrees, -arcDegrees, arcDegrees);
        }

        /// <summary>
        /// The aim direction clamped into the arc: the direction shots actually travel, and the direction the ship
        /// points. Always a unit vector pointing forward; a degenerate aim fires straight ahead.
        /// </summary>
        public static Vector2 Clamp(Vector2 aim, float arcDegrees)
        {
            var radians = Degrees(aim, arcDegrees) * (MathF.PI / 180f);
            return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        }
    }
}
