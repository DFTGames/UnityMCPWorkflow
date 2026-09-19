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
    }
}
