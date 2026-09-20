using System;

namespace YASS.Core
{
    /// <summary>Shared easing maths for presentation rules.</summary>
    public static class Easing
    {
        /// <summary>
        /// Moves <paramref name="current"/> towards <paramref name="target"/> with exponential easing, independent of
        /// frame rate. <paramref name="responsiveness"/> is the rate per second; 0 never moves.
        /// </summary>
        public static float Exponential(float current, float target, float responsiveness, float deltaTime)
        {
            if (responsiveness < 0f) throw new ArgumentOutOfRangeException(nameof(responsiveness));
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            var blend = 1f - MathF.Exp(-responsiveness * deltaTime);
            return current + (target - current) * blend;
        }
    }
}
