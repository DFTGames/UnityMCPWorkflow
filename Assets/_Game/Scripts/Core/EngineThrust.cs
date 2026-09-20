using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// Engine exhaust throttle (GDD "Art Direction", Engines): idle when still, stronger the faster the ship moves,
    /// eased so the flame does not flicker with every change of direction.
    /// </summary>
    public static class EngineThrust
    {
        /// <summary>
        /// Target throttle in [idle, 1] for <paramref name="move"/>, the ship's movement as a fraction of its top speed:
        /// <paramref name="idle"/> at rest, 1 at full speed or more.
        /// </summary>
        public static float Target(Vector2 move, float idle)
        {
            if (!(idle >= 0f && idle <= 1f)) throw new ArgumentOutOfRangeException(nameof(idle)); // also rejects NaN

            var amount = Math.Min(1f, move.Length());
            return idle + (1f - idle) * amount;
        }

        /// <summary>
        /// Moves <paramref name="current"/> towards <paramref name="target"/> with exponential easing, independent of
        /// frame rate. <paramref name="responsiveness"/> is the rate per second; 0 never moves.
        /// </summary>
        public static float Smooth(float current, float target, float responsiveness, float deltaTime) =>
            Easing.Exponential(current, target, responsiveness, deltaTime);
    }
}
