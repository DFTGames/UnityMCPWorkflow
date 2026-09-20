using System;

namespace YASS.Core
{
    /// <summary>Small presentation rules kept in the rules layer so they can be unit tested.</summary>
    public static class VisualCues
    {
        /// <summary>
        /// Blinking visibility driven by a countdown (invulnerability or shield time remaining): visible for the
        /// second half of each cycle. Always visible when <paramref name="remaining"/> is zero or less.
        /// </summary>
        public static bool IsBlinkVisible(float remaining, float blinksPerSecond)
        {
            if (blinksPerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(blinksPerSecond));
            if (remaining <= 0f) return true;

            var phase = remaining * blinksPerSecond;
            return phase - MathF.Floor(phase) >= 0.5f;
        }

        /// <summary>How long a hit flash lasts (GDD "Art Direction", Visual effects).</summary>
        public const float HitFlashSeconds = 0.08f;

        /// <summary>
        /// How white a damaged sprite is drawn, from 1 at the moment of the hit down to 0 when the flash ends.
        /// Fades out linearly: a hit should read instantly and then get out of the way.
        /// </summary>
        public static float HitFlash(float elapsed, float duration = HitFlashSeconds)
        {
            if (duration <= 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            if (elapsed < 0f) return 0f;

            return elapsed >= duration ? 0f : 1f - elapsed / duration;
        }
    }
}
