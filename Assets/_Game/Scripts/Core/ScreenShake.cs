using System;
using System.Numerics;

namespace YASS.Core
{
    /// <summary>
    /// Screen shake as a pool of "trauma" that events add to and time drains away (GDD "Art Direction", Visual
    /// effects). Trauma is squared to get the displacement, so a shake fades out softly rather than stopping
    /// dead, and several hits in a row build rather than restarting the same jolt.
    /// </summary>
    public static class ScreenShake
    {
        /// <summary>Trauma added by the events that shake the screen. 1 is the strongest shake.</summary>
        public const float PlayerHitTrauma = 0.45f;
        public const float LifeLostTrauma = 0.7f;
        public const float BossDefeatedTrauma = 1f;

        /// <summary>How much trauma drains per second.</summary>
        public const float DecayPerSecond = 1.6f;

        /// <summary>Largest offset in world units at full trauma.</summary>
        public const float MaxOffset = 0.45f;

        /// <summary>How fast the shake oscillates, in samples per second.</summary>
        public const float Frequency = 22f;

        public static float Add(float trauma, float amount)
        {
            if (amount < 0f) throw new ArgumentOutOfRangeException(nameof(amount));

            return Math.Clamp(trauma + amount, 0f, 1f);
        }

        public static float Decay(float trauma, float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            return Math.Max(0f, trauma - DecayPerSecond * deltaTime);
        }

        /// <summary>
        /// The camera offset for the current trauma. <paramref name="time"/> drives the oscillation and
        /// <paramref name="seed"/> separates the axes, so the shake is deterministic and repeatable in tests.
        /// </summary>
        public static Vector2 Offset(float trauma, float time, float seed = 0f)
        {
            if (trauma <= 0f) return Vector2.Zero;

            var strength = Math.Clamp(trauma, 0f, 1f);
            strength *= strength; // squared: the tail of a shake is gentle, the start is sharp

            var x = Wave(time * Frequency + seed);
            var y = Wave(time * Frequency + seed + 37.4f);
            return new Vector2(x, y) * (strength * MaxOffset);
        }

        /// <summary>
        /// A repeatable wave in -1..1 that does not look like a sine: two frequencies that do not divide into
        /// each other, so the motion never settles into an obvious rhythm.
        /// </summary>
        static float Wave(float t) =>
            (MathF.Sin(t) + MathF.Sin(t * 1.7f + 1.3f)) * 0.5f;
    }
}
