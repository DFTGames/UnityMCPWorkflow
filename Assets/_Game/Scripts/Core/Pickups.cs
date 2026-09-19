using System;
using System.Collections.Generic;

namespace YASS.Core
{
    public enum PickupType
    {
        None = 0,
        Health = 1,
        WeaponUpgrade = 2,
        Shield = 3,
        ExtraLife = 4
    }

    public enum DropSource
    {
        Enemy = 0,
        SolidMeteor = 1,
        SplittingMeteor = 2
    }

    /// <summary>Decides pickup drops (GDD page "Items and Pickups").</summary>
    public sealed class PickupDropper
    {
        public const float EnemyDropChance = 0.08f;
        public const float SolidMeteorDropChance = 0.15f;

        public const int HealthWeight = 40;
        public const int WeaponUpgradeWeight = 35;
        public const int ShieldWeight = 20;
        public const int ExtraLifeWeight = 5;
        const int TotalWeight = HealthWeight + WeaponUpgradeWeight + ShieldWeight + ExtraLifeWeight;

        static readonly PickupType[] BossDropsArray = { PickupType.WeaponUpgrade, PickupType.Health };

        /// <summary>Bosses always drop these.</summary>
        public static IReadOnlyList<PickupType> BossDrops => BossDropsArray;

        readonly IRandomSource _random;
        readonly float _dropChanceMultiplier;

        public float TimeSinceWeaponUpgrade { get; private set; }

        internal PickupDropper(IRandomSource random, float dropChanceMultiplier)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (dropChanceMultiplier < 0f) throw new ArgumentOutOfRangeException(nameof(dropChanceMultiplier));
            _dropChanceMultiplier = dropChanceMultiplier;
        }

        public static float BaseDropChance(DropSource source)
        {
            switch (source)
            {
                case DropSource.Enemy: return EnemyDropChance;
                case DropSource.SolidMeteor: return SolidMeteorDropChance;
                case DropSource.SplittingMeteor: return 0f;
                default: throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        public float DropChance(DropSource source) => Math.Min(1f, BaseDropChance(source) * _dropChanceMultiplier);

        public bool IsPityDue(int currentWeaponLevel) =>
            currentWeaponLevel < GameTuning.WeaponPityBelowLevel &&
            TimeSinceWeaponUpgrade >= GameTuning.WeaponPityDelaySeconds;

        internal void Tick(float deltaTime) => TimeSinceWeaponUpgrade += deltaTime;

        internal void NotifyWeaponUpgradeCollected() => TimeSinceWeaponUpgrade = 0f;

        /// <summary>
        /// Rolls for a drop from a destroyed non-boss source. When the pity rule is due, the drop (if the chance
        /// roll succeeds) is a weapon upgrade and the pity timer restarts, so only the next drop is forced.
        /// </summary>
        internal bool TryRollDrop(DropSource source, int currentWeaponLevel, out PickupType pickup)
        {
            pickup = PickupType.None;

            var chance = DropChance(source);
            if (chance <= 0f || _random.NextFloat() >= chance)
                return false;

            if (IsPityDue(currentWeaponLevel))
            {
                TimeSinceWeaponUpgrade = 0f;
                pickup = PickupType.WeaponUpgrade;
                return true;
            }

            pickup = PickWeighted(_random.NextFloat());
            return true;
        }

        static PickupType PickWeighted(float roll)
        {
            var value = roll * TotalWeight;
            if (value < HealthWeight) return PickupType.Health;
            value -= HealthWeight;
            if (value < WeaponUpgradeWeight) return PickupType.WeaponUpgrade;
            value -= WeaponUpgradeWeight;
            if (value < ShieldWeight) return PickupType.Shield;
            return PickupType.ExtraLife;
        }
    }
}
