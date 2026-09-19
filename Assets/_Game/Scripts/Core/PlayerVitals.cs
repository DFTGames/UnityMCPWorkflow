using System;

namespace YASS.Core
{
    /// <summary>Lives, health and post-respawn invulnerability for one player.</summary>
    public sealed class PlayerVitals
    {
        readonly float _damageMultiplier;
        readonly float _invulnerabilityDuration;

        public int Lives { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; }
        public float InvulnerabilityRemaining { get; private set; }

        public bool IsInvulnerable => InvulnerabilityRemaining > 0f;
        public bool IsGameOver => Lives <= 0;
        public float HealthFraction => Health / MaxHealth;

        internal PlayerVitals(int startingLives, float maxHealth, float damageMultiplier, float invulnerabilityDuration)
        {
            if (startingLives < 1) throw new ArgumentOutOfRangeException(nameof(startingLives));
            if (maxHealth <= 0f) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (damageMultiplier < 0f) throw new ArgumentOutOfRangeException(nameof(damageMultiplier));
            if (invulnerabilityDuration < 0f) throw new ArgumentOutOfRangeException(nameof(invulnerabilityDuration));

            Lives = startingLives;
            MaxHealth = maxHealth;
            Health = maxHealth;
            _damageMultiplier = damageMultiplier;
            _invulnerabilityDuration = invulnerabilityDuration;
        }

        internal static PlayerVitals For(DifficultySettings settings) =>
            new PlayerVitals(settings.StartingLives, settings.HealthPerLife, settings.DamageTakenMultiplier,
                GameTuning.RespawnInvulnerabilitySeconds);

        /// <summary>Applies raw damage, scaled by the difficulty multiplier. Never returns Absorbed.</summary>
        internal HitOutcome ApplyDamage(float rawDamage)
        {
            if (IsGameOver || IsInvulnerable || rawDamage <= 0f)
                return HitOutcome.Ignored;

            Health -= rawDamage * _damageMultiplier;
            if (Health > 0f)
                return HitOutcome.Damaged;

            Lives--;
            if (Lives <= 0)
            {
                Health = 0f;
                return HitOutcome.GameOver;
            }

            Health = MaxHealth;
            InvulnerabilityRemaining = _invulnerabilityDuration;
            return HitOutcome.LifeLost;
        }

        internal void Heal(float amount)
        {
            if (IsGameOver || amount <= 0f) return;
            Health = Math.Min(MaxHealth, Health + amount);
        }

        internal void AddLife()
        {
            if (IsGameOver) return;
            Lives++;
        }

        internal void Tick(float deltaTime)
        {
            if (InvulnerabilityRemaining > 0f)
                InvulnerabilityRemaining = Math.Max(0f, InvulnerabilityRemaining - deltaTime);
        }
    }
}
