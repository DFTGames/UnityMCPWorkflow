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

        [Header("Weapon")]
        [SerializeField] bool fires;
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
        public float FireInterval => fireInterval;
        public float FirstShotDelay => firstShotDelay;
        public float BulletSpeed => bulletSpeed;
        public float BulletDamage => bulletDamage;
    }
}
