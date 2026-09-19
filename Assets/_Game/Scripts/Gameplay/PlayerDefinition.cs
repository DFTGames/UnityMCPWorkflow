using UnityEngine;

namespace YASS.Gameplay
{
    /// <summary>Provisional player ship tuning (GDD "Mechanics", Provisional player values).</summary>
    [CreateAssetMenu(menuName = "YASS/Player Definition", fileName = "PlayerDefinition")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        [SerializeField, Min(0f)] float speed = 8f;
        [SerializeField, Min(0f), Tooltip("How far inside the screen edges the ship is kept.")]
        float edgeMargin = 0.5f;
        [SerializeField, Min(0f)] float projectileSpeed = 18f;
        [SerializeField, Min(0f)] float projectileDamage = 1f;

        public float Speed => speed;
        public float EdgeMargin => edgeMargin;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileDamage => projectileDamage;
    }
}
