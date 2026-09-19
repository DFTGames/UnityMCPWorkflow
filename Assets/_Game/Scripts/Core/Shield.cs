using System;

namespace YASS.Core
{
    /// <summary>Shield pickup state: absorbs a number of hits or expires after a duration, whichever comes first.</summary>
    public sealed class Shield
    {
        readonly int _maxHits;
        readonly float _duration;
        readonly float _flickerDuration;

        public int HitsRemaining { get; private set; }
        public float TimeRemaining { get; private set; }

        public bool IsActive => HitsRemaining > 0 && TimeRemaining > 0f;
        public bool IsFlickering => IsActive && TimeRemaining <= _flickerDuration;

        internal Shield(int maxHits = GameTuning.ShieldHits, float duration = GameTuning.ShieldDurationSeconds,
            float flickerDuration = GameTuning.ShieldFlickerSeconds)
        {
            if (maxHits < 1) throw new ArgumentOutOfRangeException(nameof(maxHits));
            if (duration <= 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            if (flickerDuration < 0f) throw new ArgumentOutOfRangeException(nameof(flickerDuration));

            _maxHits = maxHits;
            _duration = duration;
            _flickerDuration = flickerDuration;
        }

        /// <summary>Activates the shield, or refreshes it to full if already active (shields do not stack).</summary>
        internal void Activate()
        {
            HitsRemaining = _maxHits;
            TimeRemaining = _duration;
        }

        internal bool TryAbsorbHit()
        {
            if (!IsActive) return false;

            HitsRemaining--;
            if (HitsRemaining == 0) TimeRemaining = 0f;
            return true;
        }

        internal void Tick(float deltaTime)
        {
            if (!IsActive) return;

            TimeRemaining -= deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                HitsRemaining = 0;
            }
        }
    }
}
