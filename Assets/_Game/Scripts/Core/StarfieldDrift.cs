using System;

namespace YASS.Core
{
    /// <summary>One star in the menu's drifting field, in view-relative coordinates.</summary>
    /// <remarks>
    /// Positions are fractions of the view (0 to 1) rather than pixels, so the same field fits any screen
    /// and any aspect without being regenerated: the presentation multiplies by whatever rectangle it has.
    /// </remarks>
    public struct Star
    {
        /// <summary>Across the view, 0 at the left edge and 1 at the right.</summary>
        public float X;

        /// <summary>Up the view, 0 at the bottom and 1 at the top.</summary>
        public float Y;

        /// <summary>How big to draw it, 0 to 1, against whatever the presentation calls full size.</summary>
        public float Size;

        /// <summary>How fast it drifts, 0 to 1. The spread across stars is what makes the field have depth.</summary>
        public float Speed;

        /// <summary>How bright to draw it, 0 to 1. Dimmer stars read as further away.</summary>
        public float Brightness;
    }

    /// <summary>
    /// The menu's starfield: a fixed set of stars drifting slowly across the view and wrapping round
    /// (GDD "Art Direction": star fields carried over from the original).
    /// </summary>
    /// <remarks>
    /// Kept here, away from the engine, because it is arithmetic and nothing else: the drawing is a few
    /// quads. The field is generated from a seed, so a given seed is always the same sky and a test can
    /// assert against it rather than against whatever the random number generator felt like.
    ///
    /// **Nothing here is a particle system.** The menu canvas is Screen Space Overlay, which draws over
    /// everything the camera renders, so a world-space particle system behind it can never be seen however
    /// large its particles are. Stars that belong on a canvas have to be drawn by the canvas.
    ///
    /// Slower stars are drawn smaller and dimmer, which is the whole of the parallax: the eye reads the
    /// spread of speed, size and brightness together as depth.
    /// </remarks>
    public sealed class StarfieldDrift
    {
        /// <summary>
        /// Fraction of the view a full-speed star crosses in a second. Slow, because the menu should feel
        /// alive rather than busy, but not so slow that it reads as a still image: at the first value tried
        /// a star took over a minute to cross and nobody could tell the field was moving at all.
        /// </summary>
        public const float CrossingsPerSecond = 0.035f;

        readonly Star[] _stars;

        public StarfieldDrift(int count, int seed)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "a field cannot have fewer than no stars");

            _stars = new Star[count];
            var random = new Random(seed);

            for (var i = 0; i < count; i++)
            {
                // Depth first, then everything else follows from it, so a star is never fast and faint or
                // slow and huge: those read as mistakes rather than as distance.
                //
                // Squared, which pushes most stars to the far end. Spread evenly they come out at much the
                // same brightness as each other and the field reads as falling snow; a real sky is mostly
                // faint with a few that stand out, and that is what gives it depth.
                var even = (float)random.NextDouble();
                var depth = even * even;

                _stars[i] = new Star
                {
                    X = (float)random.NextDouble(),
                    Y = (float)random.NextDouble(),
                    Speed = Lerp(0.30f, 1f, depth),

                    // The floors matter more than the ceilings. A canvas scales its units down on a smaller
                    // window, so a star that is a fraction of an already small size lands on less than a
                    // pixel and simply is not drawn. Nothing here should be able to vanish.
                    Size = Lerp(0.45f, 1f, depth),
                    Brightness = Lerp(0.45f, 1f, depth),
                };
            }
        }

        /// <summary>The stars as they are now. The array is the field's own: read it, do not keep it.</summary>
        public Star[] Stars => _stars;

        public int Count => _stars.Length;

        /// <summary>
        /// Moves the field on by this many seconds. Stars drift left and reappear on the right, keeping
        /// their row, so the field is endless without ever being regenerated.
        /// </summary>
        public void Advance(float seconds)
        {
            if (seconds <= 0f) return;

            for (var i = 0; i < _stars.Length; i++)
            {
                var x = _stars[i].X - _stars[i].Speed * CrossingsPerSecond * seconds;

                // A loop rather than one subtraction: a long first frame, or a step taken while the game
                // was in the background, can carry a star more than a whole width and one wrap would leave
                // it off the left of the view for good.
                while (x < 0f) x += 1f;
                while (x >= 1f) x -= 1f;

                _stars[i].X = x;
            }
        }

        static float Lerp(float from, float to, float t) => from + (to - from) * t;
    }
}
