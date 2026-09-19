using System;
using System.Collections.Generic;

namespace YASS.Core
{
    public readonly struct RamResult
    {
        public readonly HitOutcome Outcome;
        public readonly bool EnemyDestroyed;

        public RamResult(HitOutcome outcome, bool enemyDestroyed)
        {
            Outcome = outcome;
            EnemyDestroyed = enemyDestroyed;
        }
    }

    /// <summary>Rules state for one player's ship: vitals, shield and weapon. Mutated only through GameSession.</summary>
    public sealed class PlayerShip
    {
        public int PlayerIndex { get; }
        public PlayerVitals Vitals { get; }
        public Shield Shield { get; }
        public Weapon Weapon { get; }

        public bool IsGameOver => Vitals.IsGameOver;

        internal PlayerShip(int playerIndex, DifficultySettings settings)
        {
            if (playerIndex < 0) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            PlayerIndex = playerIndex;
            Vitals = PlayerVitals.For(settings);
            Shield = new Shield();
            Weapon = new Weapon();
        }

        /// <summary>
        /// A hit from a projectile or hazard. Invulnerability ignores it without using the shield;
        /// otherwise the shield absorbs it, or health takes the damage. Losing a life drops the weapon one level.
        /// </summary>
        internal HitOutcome TakeHit(float damage)
        {
            if (Vitals.IsGameOver || Vitals.IsInvulnerable || damage <= 0f)
                return HitOutcome.Ignored;

            if (Shield.TryAbsorbHit())
                return HitOutcome.Absorbed;

            var outcome = Vitals.ApplyDamage(damage);
            if (outcome == HitOutcome.LifeLost || outcome == HitOutcome.GameOver)
                Weapon.Downgrade();
            return outcome;
        }

        /// <summary>
        /// Collision with an enemy body. While invulnerable (or game over) the ship passes through harmlessly.
        /// A shielded ship destroys non-boss enemies without using a shield hit; a boss collision is a normal hit
        /// (the shield absorbs it if active). Unshielded, the ship takes the contact damage and a non-boss enemy
        /// is destroyed.
        /// </summary>
        internal RamResult Ram(bool isBoss, float contactDamage)
        {
            if (Vitals.IsGameOver || Vitals.IsInvulnerable)
                return new RamResult(HitOutcome.Ignored, false);

            if (!isBoss && Shield.IsActive)
                return new RamResult(HitOutcome.Absorbed, true);

            return new RamResult(TakeHit(contactDamage), !isBoss);
        }

        /// <summary>Applies a collected pickup. Returns true when a max-level weapon upgrade earns its points bonus.</summary>
        internal bool Collect(PickupType pickup)
        {
            if (Vitals.IsGameOver) return false;

            switch (pickup)
            {
                case PickupType.Health:
                    Vitals.Heal(GameTuning.HealthPickupAmount);
                    return false;
                case PickupType.WeaponUpgrade:
                    return !Weapon.Upgrade();
                case PickupType.Shield:
                    Shield.Activate();
                    return false;
                case PickupType.ExtraLife:
                    Vitals.AddLife();
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(pickup), pickup, null);
            }
        }

        internal void Tick(float deltaTime, in PlayerCommand command, List<ShotSpec> shots)
        {
            Vitals.Tick(deltaTime);
            Shield.Tick(deltaTime);

            var firing = !IsGameOver && command.Fire;
            Weapon.Tick(deltaTime, firing, command.AimDirection, PlayerIndex, shots);
        }
    }
}
