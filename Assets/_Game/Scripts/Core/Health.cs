using System;

namespace YASS.Core
{
    /// <summary>Hit points for an enemy or meteor.</summary>
    public sealed class Health
    {
        public float Max { get; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public Health(float max)
        {
            if (max <= 0f) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Current = max;
        }

        /// <summary>Returns true only for the hit that destroys it; later hits are ignored.</summary>
        public bool TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return false;

            Current -= amount;
            if (Current > 0f) return false;

            Current = 0f;
            return true;
        }
    }
}
