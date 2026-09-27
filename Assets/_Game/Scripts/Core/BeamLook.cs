using System;

namespace YASS.Core
{
    /// <summary>
    /// How a sweeping beam looks from one moment to the next (GDD "Art Direction", the beam): how much it
    /// shimmers while it burns, and how its warning line builds before it fires.
    /// </summary>
    /// <remarks>
    /// Here rather than in the view because a beam that is drawn wrong is drawn wrong for the whole two
    /// seconds it is on screen, and the ways it goes wrong are arithmetic: a shimmer that reaches zero blinks
    /// the beam out, a warning that reaches full brightness early stops warning anything, and a pulse that
    /// drifts with frame rate looks like a stutter rather than a machine. None of that is comfortable to
    /// judge by eye at sixty frames a second, and all of it is one assertion here.
    /// </remarks>
    public static class BeamLook
    {
        /// <summary>
        /// The fastest wave anywhere in here, in cycles per second. **Kept well clear of 15 to 25 Hz**,
        /// which is the band most associated with photosensitive seizures: a bright beam is exactly the kind
        /// of thing that should not flicker there. An earlier version of this had brightness running at 18 Hz
        /// purely because the number looked lively.
        ///
        /// It also has to stay slow enough to be sampled honestly. The Sniper's beam is driven from a fixed
        /// step at 50 Hz, and anything approaching a third of that rate beats against it and reads as an
        /// irregular strobe rather than a shimmer.
        /// </summary>
        public const float FastestHertz = 41f / (2f * MathF.PI);

        /// <summary>How far the width swings either side of its nominal value, as a fraction.</summary>
        public const float WidthSwing = 0.08f;

        /// <summary>The dimmest the beam ever gets while firing, as a fraction of full brightness.</summary>
        public const float DimmestWhileFiring = 0.82f;

        /// <summary>
        /// The width multiplier at a moment in the shot. Two waves at unrelated rates, so the beam never
        /// settles into a rhythm the eye can follow and predict: one alone reads as a pulsing tube.
        /// </summary>
        public static float WidthAt(float seconds)
        {
            var shimmer = MathF.Sin(seconds * 17f) * 0.6f + MathF.Sin(seconds * 29f) * 0.4f;

            return 1f + shimmer * WidthSwing;
        }

        /// <summary>
        /// The brightness multiplier at a moment in the shot. Never reaches zero: a beam that blinks out,
        /// however briefly, reads as a bug rather than as a flicker, and this one is still burning the ship
        /// while it does it.
        /// </summary>
        public static float BrightnessAt(float seconds)
        {
            var flicker = MathF.Sin(seconds * 23f) * 0.5f + MathF.Sin(seconds * 41f) * 0.5f;

            // Mapped into [DimmestWhileFiring, 1] rather than swinging around 1, so the beam is never
            // brighter than the colour it was given: that colour is what the art direction chose.
            return 1f - (1f - DimmestWhileFiring) * (0.5f - flicker * 0.5f);
        }

        /// <summary>
        /// How strongly the warning line shows, from nothing when the telegraph starts to full as it fires.
        /// </summary>
        /// <remarks>
        /// Squared rather than linear on purpose. The warning is the whole fight (GDD "Enemies and
        /// Hazards"), and a line that fades up evenly spends most of its time bright enough to ignore and
        /// gives no sense of the shot getting closer. This stays faint, then rushes, so the last quarter of
        /// the telegraph is unmistakably the last quarter.
        /// </remarks>
        public static float WarningAt(float progress)
        {
            var t = Clamp01(progress);

            return t * t;
        }

        /// <summary>
        /// How wide the warning line is against the beam that follows it, so the telegraph reads as a
        /// promise of the shot rather than as the shot itself arriving early.
        /// </summary>
        public static float WarningWidthAt(float progress)
        {
            var t = Clamp01(progress);

            // It thickens as it charges, but never to the full beam: the difference between "about to fire"
            // and "firing" has to be visible at a glance, because one of them is survivable.
            return 0.12f + 0.28f * t;
        }

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
