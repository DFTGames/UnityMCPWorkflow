using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// Tuning data for one enemy type. Base values before difficulty scaling; the GDD page "Enemies and Hazards"
    /// is authoritative for them.
    /// </summary>
    [CreateAssetMenu(menuName = "YASS/Enemy Definition", fileName = "EnemyDefinition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] EnemySize size = EnemySize.Small;
        [SerializeField, Min(0.01f)] float maxHealth = 1f;
        [SerializeField, Min(0f)] float speed = 5f;
        [SerializeField, Min(0f)] float contactDamage = 20f;

        [Header("Motion")]
        [SerializeField] MotionPattern pattern = MotionPattern.Straight;
        [SerializeField, Min(0f)] float waveAmplitude = 1.5f;
        [SerializeField, Min(0f)] float waveFrequency = 0.4f;

        [SerializeField, Range(0f, 1f), Tooltip("Hold Position: how far in from the right edge it stops.")]
        float stationFromRight = 1f / 3f;

        [Header("Dive (GDD \"Enemies and Hazards\": enter, lock on, charge)")]
        [SerializeField, Min(0f)] float diveEntrySeconds = 0.8f;
        [SerializeField, Min(0.05f)] float diveLockSeconds = 0.6f;
        [SerializeField, Min(0.1f)] float diveChargeSpeed = 12f;

        [Header("Weapon")]
        [SerializeField] bool fires;
        [SerializeField, Min(1), Tooltip("More than one fires them as a burst (Gunship).")]
        int shotsPerBurst = 1;
        [SerializeField, Min(0.02f), Tooltip("Gap between the shots of one burst.")]
        float burstShotInterval = 0.15f;
        [SerializeField, Min(0.05f)] float fireInterval = 2f;
        [SerializeField, Min(0f)] float firstShotDelay = 0.8f;
        [SerializeField, Min(0f)] float bulletSpeed = 6f;
        [SerializeField, Min(0f)] float bulletDamage = 10f;

        public EnemySize Size => size;
        public float MaxHealth => maxHealth;
        public float Speed => speed;
        public float ContactDamage => contactDamage;
        public MotionPattern Pattern => pattern;
        public float WaveAmplitude => waveAmplitude;
        public float WaveFrequency => waveFrequency;
        public bool Fires => fires;
        public int ShotsPerBurst => shotsPerBurst;
        public float BurstShotInterval => burstShotInterval;
        public float StationFromRight => stationFromRight;
        public float DiveEntrySeconds => diveEntrySeconds;
        public float DiveLockSeconds => diveLockSeconds;
        public float DiveChargeSpeed => diveChargeSpeed;

        [Header("Front shield (Frigate)")]
        [SerializeField, Tooltip("Shots into its nose are turned away; it must be hit from the side or behind.")]
        bool hasFrontShield;

        public bool HasFrontShield => hasFrontShield;

        [Header("Mines (Mine Layer)")]
        [SerializeField, Tooltip("Drops proximity mines as it flies.")] bool laysMines;
        [SerializeField, Min(0.2f)] float mineInterval = 1.6f;

        public bool LaysMines => laysMines;
        public float MineInterval => mineInterval;

        [Header("Beam (Sniper)")]
        [SerializeField, Tooltip("Warns along a line, then fires a beam down it.")] bool firesBeam;
        [SerializeField, Min(0.1f)] float beamWarningSeconds = 1.2f;
        [SerializeField, Min(0.05f)] float beamSeconds = 0.25f;
        [SerializeField, Min(0f)] float beamRecoverySeconds = 1.5f;
        [SerializeField, Min(0f)] float beamDamage = 20f;
        [SerializeField, Min(0.01f)] float beamHalfWidth = 0.15f;
        [SerializeField, Min(1f)] float beamLength = 40f;

        public bool FiresBeam => firesBeam;
        public float BeamWarningSeconds => beamWarningSeconds;
        public float BeamSeconds => beamSeconds;
        public float BeamRecoverySeconds => beamRecoverySeconds;
        public float BeamDamage => beamDamage;
        public float BeamHalfWidth => beamHalfWidth;
        public float BeamLength => beamLength;
        public float FireInterval => fireInterval;
        public float FirstShotDelay => firstShotDelay;
        public float BulletSpeed => bulletSpeed;
        public float BulletDamage => bulletDamage;
    }
}
