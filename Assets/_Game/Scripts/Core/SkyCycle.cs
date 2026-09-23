using System;

namespace YASS.Core
{
    /// <summary>
    /// The sky of an Endless run (GDD "Core Loop", Endless mode): a ring of skies, each held for a while and
    /// then dissolved into the next, for ever.
    /// </summary>
    /// <remarks>
    /// It runs on its own slow clock, deliberately unrelated to the cycles, the waves and the boss. Tying the
    /// sky to the cycle is what this replaces: the backdrop changed the instant a boss died, which put a hard
    /// cut at the busiest moment of the run and announced that the mode is a loop. A sky that drifts on its own
    /// never lines up with anything the player is watching, so the run reads as one unbroken journey.
    ///
    /// The last sky dissolves into the first, so the ring has no seam and a long run cannot reach the end of
    /// the set. A cut is not an option the caller can choose: a fade of zero is rejected.
    /// </remarks>
    public sealed class SkyCycle
    {
        readonly float _hold;
        readonly float _fade;

        float _elapsed;

        /// <param name="skyCount">How many skies are in the ring.</param>
        /// <param name="holdSeconds">How long a sky is held at full strength before it starts to go.</param>
        /// <param name="fadeSeconds">How long one sky takes to dissolve into the next. Must be above zero.</param>
        /// <param name="startIndex">Which sky the run opens on, so two runs do not always begin the same way.</param>
        public SkyCycle(int skyCount, float holdSeconds, float fadeSeconds, int startIndex = 0)
        {
            if (skyCount < 1) throw new ArgumentOutOfRangeException(nameof(skyCount));
            if (holdSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(holdSeconds));
            if (fadeSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fadeSeconds), "A sky has to dissolve, never cut.");

            SkyCount = skyCount;
            _hold = holdSeconds;
            _fade = fadeSeconds;
            Showing = ((startIndex % skyCount) + skyCount) % skyCount;
        }

        public int SkyCount { get; }

        /// <summary>The sky being left: the whole sky while <see cref="Blend"/> is zero.</summary>
        public int Showing { get; private set; }

        /// <summary>The sky coming in, which is the first one again once the ring is round.</summary>
        public int Arriving => (Showing + 1) % SkyCount;

        /// <summary>How far the dissolve has got: 0 is all <see cref="Showing"/>, 1 is all <see cref="Arriving"/>.</summary>
        public float Blend { get; private set; }

        /// <summary>
        /// How long until the next dissolve begins, and zero once it has. The caller uses this to fetch the
        /// sky that is coming before it is needed, so nothing has to be loaded at the moment it appears.
        /// </summary>
        public float SecondsUntilDissolve => MathF.Max(0f, _hold - _elapsed);

        /// <summary>Moves the sky on. Time that runs backwards, or not at all, changes nothing.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds)) return;

            var period = _hold + _fade;
            _elapsed += deltaSeconds;

            if (_elapsed >= period)
            {
                // Modulo rather than a loop: a stall of any length lands where it should, in one step and
                // without spinning.
                var steps = (int)(_elapsed / period);
                _elapsed -= steps * period;
                Showing = (Showing + steps % SkyCount) % SkyCount;
            }

            Blend = _elapsed <= _hold ? 0f : MathF.Min(1f, (_elapsed - _hold) / _fade);
        }
    }
}
