using UnityEngine;

namespace YASS.Gameplay
{
    /// <summary>
    /// The boss's weak core: a child collider that forwards player shots to <see cref="BossView.TakeCoreHit"/>,
    /// and shows whether it is open (bright, damageable) or closed (dim, armoured).
    /// </summary>
    public sealed class BossCoreView : MonoBehaviour, IDamageable
    {
        [SerializeField] SpriteRenderer coreRenderer;
        [SerializeField] Color openColour = Color.white;
        [SerializeField] Color closedColour = new Color(0.25f, 0.25f, 0.3f, 1f);

        BossView _boss;
        CircleCollider2D _circle;

        void Awake() => _circle = GetComponent<CircleCollider2D>();

        public bool BlocksPiercing => false;
        public bool IsAlive => _boss != null && _boss.IsAlive;
        public BossView Boss => _boss;

        /// <summary>
        /// Where the core is and how big it is, in world space, so the armour can work out whether a shot
        /// that landed on it would have reached the core with the hull out of the way.
        /// </summary>
        /// <remarks>
        /// Taken from the collider rather than the sprite, and scaled: the transform is sized to fit the art,
        /// which scales the collider with it, so the raw radius describes a hitbox that does not exist.
        /// </remarks>
        public Vector2 HitCentre =>
            _circle == null
                ? (Vector2)transform.position
                : (Vector2)transform.position + _circle.offset * transform.lossyScale.x;

        public float HitRadius => _circle == null ? 0f : _circle.radius * transform.lossyScale.x;

        public void Init(BossView boss) => _boss = boss;

        public void TakeHit(float damage, int playerIndex, Vector2 hitPoint)
        {
            if (_boss != null) _boss.TakeCoreHit(damage, playerIndex);
        }

        public void ShowOpen(bool open)
        {
            // Written every step rather than only on a change: the core's colour is the player's one tell, and
            // anything else that touches the renderer (a hit flash, say) must not be able to leave it wrong.
            if (coreRenderer != null) coreRenderer.color = open ? openColour : closedColour;
        }
    }
}
