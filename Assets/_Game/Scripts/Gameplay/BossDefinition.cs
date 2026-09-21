using System;
using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// One boss, as data (GDD "Levels"). Every campaign boss is an instance of this: its rules go to
    /// <see cref="BossBrain"/> as a <see cref="BossSpec"/>, and the rest tunes how <see cref="BossView"/> carries
    /// the actions out. A boss only needs the fields its own attacks use.
    /// </summary>
    [CreateAssetMenu(menuName = "YASS/Boss Definition", fileName = "BossDefinition")]
    public sealed class BossDefinition : ScriptableObject
    {
        /// <summary>One row of the boss's attack table.</summary>
        [Serializable]
        public struct AttackRow
        {
            public BossActionType type;
            [Min(1)] public int count;
            public BossPhase when;
            [Min(0f), Tooltip("Seconds into each matching phase before it first happens.")]
            public float firstDelaySeconds;
            [Min(0f), Tooltip("How often it repeats within the phase; 0 means once per phase.")]
            public float intervalSeconds;
            [Range(0f, 1f), Tooltip("Active while health is above this fraction...")]
            public float activeAbove;
            [Range(0f, 1f), Tooltip("...and at or below this one. Use bands to make a boss escalate.")]
            public float activeAtOrBelow;
        }

        [Header("Identity")]
        [SerializeField] string displayName = "Boss";
        [SerializeField, Min(1)] int levelNumber = 1;

        [Header("Rules")]
        [SerializeField, Min(1f)] float maxHealth = 200f;
        [SerializeField, Range(0f, 1f), Tooltip("What a shot into the armoured hull is worth, as a fraction.")]
        float hullDamageMultiplier = 0.5f;
        [SerializeField, Range(0.1f, 4f), Tooltip("What a shot into the open core is worth, as a fraction.")]
        float coreDamageMultiplier = 1f;
        [SerializeField, Min(0.2f), Tooltip("One full closed-then-open cycle.")] float cycleSeconds = 8f;
        [SerializeField, Min(0.1f), Tooltip("How much of the cycle the core is open and damageable.")]
        float openSeconds = 4f;
        [SerializeField] AttackRow[] attacks = Array.Empty<AttackRow>();

        [Header("Movement")]
        [SerializeField, Min(0f)] float contactDamage = 40f;
        [SerializeField, Min(0.1f)] float entrySpeed = 2f;
        [SerializeField, Tooltip("How far in from the right edge it holds.")] float holdInset = 3f;
        [SerializeField, Min(0f)] float driftAmplitude = 2.2f;
        [SerializeField, Min(0.1f)] float driftPeriod = 6f;

        [Header("Bullets")]
        [SerializeField, Range(0f, 180f)] float spreadAngle = 60f;
        [SerializeField, Min(0f)] float bulletSpeed = 6f;
        [SerializeField, Min(0f)] float bulletDamage = 10f;

        [Header("Launched enemies")]
        [SerializeField, Tooltip("What LaunchDarts sends out.")] EnemyView minionPrefab;

        [Header("Hurled meteors")]
        [SerializeField] MeteorDefinition meteor;
        [SerializeField, Min(0.1f)] float meteorSpeed = 5f;

        [Header("Beam sweep")]
        [SerializeField, Tooltip("Degrees, measured as a direction: 180 is straight to the left.")]
        float sweepFromDegrees = 150f;
        [SerializeField] float sweepToDegrees = 210f;
        [SerializeField, Min(0.1f)] float sweepSeconds = 2.5f;
        [SerializeField, Min(0.05f), Tooltip("How often a beam resting on the ship may burn it.")]
        float beamDamageInterval = 0.5f;
        [SerializeField, Min(0f)] float beamDamage = 15f;
        [SerializeField, Min(0.05f)] float beamHalfWidth = 0.35f;
        [SerializeField, Min(1f)] float beamLength = 40f;

        [Header("Dash")]
        [SerializeField, Min(0f)] float dashWindUpSeconds = 0.7f;
        [SerializeField, Min(0.1f)] float dashSpeed = 22f;
        [SerializeField, Min(0.1f)] float dashReturnSpeed = 7f;
        [SerializeField, Tooltip("How far past the left edge the dash carries it.")] float dashOvershoot = 1f;

        [Header("Gravity")]
        [SerializeField, Min(0.1f)] float pullSeconds = 2f;
        [SerializeField, Min(0f), Tooltip("Units per second at the centre of the well.")] float pullStrength = 4f;
        [SerializeField, Min(0.1f)] float pullRadius = 14f;

        public string DisplayName => displayName;
        public int LevelNumber => levelNumber;
        public float MaxHealth => maxHealth;
        public float HullDamageMultiplier => hullDamageMultiplier;
        public float CoreDamageMultiplier => coreDamageMultiplier;
        public float ContactDamage => contactDamage;
        public float EntrySpeed => entrySpeed;
        public float HoldInset => holdInset;
        public float DriftAmplitude => driftAmplitude;
        public float DriftPeriod => driftPeriod;
        public float SpreadAngle => spreadAngle;
        public float BulletSpeed => bulletSpeed;
        public float BulletDamage => bulletDamage;
        public EnemyView MinionPrefab => minionPrefab;
        public MeteorDefinition Meteor => meteor;
        public float MeteorSpeed => meteorSpeed;
        public float SweepFromDegrees => sweepFromDegrees;
        public float SweepToDegrees => sweepToDegrees;
        public float SweepSeconds => sweepSeconds;
        public float BeamDamageInterval => beamDamageInterval;
        public float BeamDamage => beamDamage;
        public float BeamHalfWidth => beamHalfWidth;
        public float BeamLength => beamLength;
        public float DashWindUpSeconds => dashWindUpSeconds;
        public float DashSpeed => dashSpeed;
        public float DashReturnSpeed => dashReturnSpeed;
        public float DashOvershoot => dashOvershoot;
        public float PullSeconds => pullSeconds;
        public float PullStrength => pullStrength;
        public float PullRadius => pullRadius;
        public AttackRow[] Attacks => attacks;

        /// <summary>
        /// Whether an action's count means anything. Spreads, meteors and launches come in numbers; a dash, a
        /// sweep, a pull and a turret volley are one event whose shape is tuned by the fields above.
        /// </summary>
        public static bool CountsFor(BossActionType type) =>
            type == BossActionType.FireSpread || type == BossActionType.HurlMeteor ||
            type == BossActionType.LaunchDarts;

        /// <summary>The rules-layer spec. Throws if the data is unusable, which <see cref="Validate"/> reports first.</summary>
        public BossSpec ToSpec()
        {
            var list = new BossAttack[attacks.Length];
            for (var i = 0; i < attacks.Length; i++)
            {
                var row = attacks[i];
                list[i] = new BossAttack(row.type, row.count, row.when, row.firstDelaySeconds, row.intervalSeconds,
                    row.activeAbove, row.activeAtOrBelow);
            }

            return new BossSpec(displayName, maxHealth, cycleSeconds, openSeconds, list, hullDamageMultiplier,
                coreDamageMultiplier);
        }

        /// <summary>Returns a description of the first problem found, or null when the boss is usable.</summary>
        public string Validate()
        {
            if (openSeconds >= cycleSeconds) return "the core is open for the whole cycle, so it is never armoured";
            if (attacks.Length == 0) return "no attacks";
            if (hullDamageMultiplier > coreDamageMultiplier)
                return "its hull is a better target than its core, so there is no reason to wait for the core";

            for (var i = 0; i < attacks.Length; i++)
            {
                var row = attacks[i];
                var label = $"attack {i + 1} ({row.type})";
                if (row.count < 1) return $"{label} has a count below 1";
                if (row.activeAbove >= row.activeAtOrBelow) return $"{label} has an empty health band";
                if (row.type == BossActionType.LaunchDarts && minionPrefab == null)
                    return $"{label} needs a minion prefab";
                if (row.type == BossActionType.HurlMeteor && meteor == null)
                    return $"{label} needs a meteor definition";

                // A dash, a sweep, a pull and a volley are single events: a count above one would read as
                // "do it this many times" and quietly do it once.
                if (row.count > 1 && !CountsFor(row.type))
                    return $"{label} has a count of {row.count}, but a {row.type} happens once";

                // An attack scheduled past the end of its own phase never happens at all.
                var phase = row.when == BossPhase.Vulnerable ? openSeconds : cycleSeconds - openSeconds;
                if (row.firstDelaySeconds >= phase)
                    return $"{label} starts {row.firstDelaySeconds} s into a phase that lasts {phase} s";

                if (row.type == BossActionType.BeamSweep && row.firstDelaySeconds + sweepSeconds > phase)
                    return $"{label} would still be sweeping when its phase ends";
            }

            return null;
        }
    }
}
