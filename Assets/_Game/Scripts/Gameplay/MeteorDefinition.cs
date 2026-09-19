using UnityEngine;
using YASS.Core;

namespace YASS.Gameplay
{
    /// <summary>
    /// Tuning data for one meteor type and size. Splitting meteors chain to the definition of their fragments.
    /// The GDD page "Enemies and Hazards" is authoritative for these values.
    /// </summary>
    [CreateAssetMenu(menuName = "YASS/Meteor Definition", fileName = "MeteorDefinition")]
    public sealed class MeteorDefinition : ScriptableObject
    {
        [SerializeField] bool splitting = true;
        [SerializeField] MeteorSize size = MeteorSize.Large;
        [SerializeField] MeteorDefinition fragment;
        [SerializeField, Min(0.01f)] float maxHealth = 4f;
        [SerializeField, Min(0f)] float contactDamage = 25f;

        [Header("Motion")]
        [SerializeField, Min(0f)] float minSpeed = 1.5f;
        [SerializeField, Min(0f)] float maxSpeed = 2.5f;
        [SerializeField, Min(0f)] float maxVerticalSpeed = 0.5f;
        [SerializeField, Min(0f)] float maxSpinDegrees = 60f;

        [Header("Look")]
        [SerializeField] Sprite sprite;
        [SerializeField, Min(0.01f)] float scale = 1f;

        public bool Splitting => splitting;
        public MeteorSize Size => size;
        public MeteorDefinition Fragment => fragment;
        public float MaxHealth => maxHealth;
        public float ContactDamage => contactDamage;
        public float MinSpeed => minSpeed;
        public float MaxSpeed => maxSpeed;
        public float MaxVerticalSpeed => maxVerticalSpeed;
        public float MaxSpinDegrees => maxSpinDegrees;
        public Sprite Sprite => sprite;
        public float Scale => scale;

        void OnValidate()
        {
            if (maxSpeed < minSpeed) maxSpeed = minSpeed;
            if (!splitting || fragment == null) return;

            if (!MeteorRules.TrySplit(size, out var expected))
                Debug.LogWarning($"{name}: a {size} meteor does not split, so its fragment is never used.", this);
            else if (fragment.size != expected || !fragment.splitting)
                Debug.LogWarning($"{name}: fragment should be a splitting {expected} meteor.", this);
        }
    }
}
